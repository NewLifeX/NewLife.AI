using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>中央气象台天气查询实现。国家权威数据，无需密钥，覆盖全国城市</summary>
/// <remarks>
/// 首次查询时并发拉取所有省份城市列表并缓存全量映射，后续查询直接命中缓存无额外 HTTP 请求。
/// 城市名自动去除后缀"市/区/县/省"以提高匹配精度
/// </remarks>
public class WeatherNmcService : IWeatherService
{
    // 城市名 → 站点代码全量缓存（进程内持久，首次查询时并发扫描所有省份后填充）
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<String, String>
        _stationCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _http;

    /// <summary>初始化中央气象台天气查询服务</summary>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public WeatherNmcService(HttpClient? httpClient = null) => _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>获取指定城市的实时天气信息</summary>
    /// <param name="city">城市名称，支持中文</param>
    /// <param name="unit">温度单位：C（摄氏度）或 F（华氏度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回天气信息；城市不在覆盖范围内或失败时返回 null</returns>
    public async Task<WeatherModel?> GetWeatherAsync(String city, String unit = "C", CancellationToken cancellationToken = default)
    {
        try
        {
            var stationId = await ResolveStationAsync(city.Trim(), cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrEmpty(stationId)) return null;

            var resp = await _http.GetAsync(
                $"https://www.nmc.cn/rest/weather?stationid={stationId}",
                cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = json.ToJsonEntity<NmcWeatherResponse>();

            var real = root?.Data?.Real;
            if (real?.Weather == null) return null;

            var w = real.Weather;
            var wind = real.Wind;

            var useF = unit.EqualIgnoreCase("F");
            var tempC = w.Temperature;
            var feelC = w.Feelst;

            return new WeatherModel
            {
                City = real.Station?.City ?? city,
                Province = real.Station?.Province,
                Description = w.Info,
                Temp = useF ? $"{tempC * 9 / 5 + 32:F1}°F" : $"{tempC}°C",
                FeelsLike = useF ? $"{feelC * 9 / 5 + 32:F1}°F" : $"{feelC}°C",
                Humidity = $"{w.Humidity}%",
                Rain = $"{w.Rain} mm",
                Wind = wind == null ? null : $"{wind.Direct} {wind.Power}（{wind.Speed} m/s）",
                PublishTime = real.PublishTime,
                Warning = String.IsNullOrEmpty(real.Warn?.Alert) ? null : real.Warn.Alert,
            };
        }
        catch
        {
            return null;
        }
    }

    #region 站点解析

    /// <summary>解析城市名到中央气象台站点代码</summary>
    private async Task<String?> ResolveStationAsync(String city, CancellationToken ct)
    {
        var normalized = city.TrimEnd('市', '区', '县', '省');

        if (_stationCache.TryGetValue(normalized, out var cached)) return cached;
        if (_stationCache.TryGetValue(city, out cached)) return cached;

        // 首次或未命中：拉取全量省份→城市映射
        var pvResp = await _http.GetAsync("https://www.nmc.cn/rest/province", ct).ConfigureAwait(false);
        var pvJson = await pvResp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var provinces = pvJson.ToJsonEntity<List<NmcProvince>>();
        if (provinces == null || provinces.Count == 0) return null;

        var tasks = provinces
            .Where(p => !String.IsNullOrEmpty(p.Code))
            .Select(p => FetchCitiesAsync(p.Code!, ct))
            .ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var cities in results)
        {
            foreach (var c in cities)
            {
                if (String.IsNullOrEmpty(c.Code) || String.IsNullOrEmpty(c.City)) continue;
                _stationCache.TryAdd(c.City, c.Code);
                var key2 = c.City.TrimEnd('市', '区', '县', '省');
                if (key2 != c.City) _stationCache.TryAdd(key2, c.Code);
            }
        }

        if (_stationCache.TryGetValue(normalized, out var found)) return found;
        if (_stationCache.TryGetValue(city, out found)) return found;
        return null;
    }

    private async Task<List<NmcCity>> FetchCitiesAsync(String provinceCode, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync($"https://www.nmc.cn/rest/province/{provinceCode}", ct).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<List<NmcCity>>() ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion

    #region 内部模型
    private class NmcProvince { public String? Code { get; set; } public String? Name { get; set; } }
    private class NmcCity { public String? Code { get; set; } public String? Province { get; set; } public String? City { get; set; } }
    private class NmcStation { public String? Code { get; set; } public String? Province { get; set; } public String? City { get; set; } }
    private class NmcWeatherInfo
    {
        public Double Temperature { get; set; }
        public Double Humidity { get; set; }
        public Double Rain { get; set; }
        public Double Feelst { get; set; }
        public String? Info { get; set; }
    }
    private class NmcWind { public String? Direct { get; set; } public String? Power { get; set; } public Double Speed { get; set; } }
    private class NmcWarn { public String? Alert { get; set; } }
    private class NmcReal
    {
        public NmcStation? Station { get; set; }
        public String? PublishTime { get; set; }
        public NmcWeatherInfo? Weather { get; set; }
        public NmcWind? Wind { get; set; }
        public NmcWarn? Warn { get; set; }
    }
    private class NmcData { public NmcReal? Real { get; set; } }
    private class NmcWeatherResponse { public String? Msg { get; set; } public Int32 Code { get; set; } public NmcData? Data { get; set; } }
    #endregion
}
