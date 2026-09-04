using System.Collections.Concurrent;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>中央气象台天气查询实现。国家权威数据，无需密钥，覆盖全国城市</summary>
/// <remarks>
/// 首次查询时并发拉取所有省份城市列表并缓存全量映射，后续查询直接命中缓存无额外 HTTP 请求。
/// 城市名自动去除后缀"市/区/县/省"以提高匹配精度
/// </remarks>
/// <remarks>初始化中央气象台天气查询服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
/// <param name="requestTimeout">单次 HTTP 请求超时；为 null 时默认 5 秒。省份扫描被个别慢端点拖死时按超时跳过该省</param>
public class WeatherNmcService(HttpClient? httpClient = null, TimeSpan? requestTimeout = null) : IWeatherService
{
    // 城市名 → 站点代码全量缓存（进程内持久，首次查询时并发扫描所有省份后填充）
    private static readonly ConcurrentDictionary<String, String> _stationCache = new(StringComparer.OrdinalIgnoreCase);

    // A-73：全量扫描只执行一次。静态信号量 + 双检，避免并发首个请求各自扫描（跨实例也共享）
    private static readonly SemaphoreSlim _loadLock = new(1, 1);
    private static Boolean _stationsLoaded;

    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>单次 HTTP 请求超时。省份扫描并发拉取全部省级城市表时，个别省份端点卡死会拖垮整轮
    /// （实测单省耗时可达 7s+）；每次请求超时后跳过该省，避免整扫描被单端点拖死</summary>
    private readonly TimeSpan _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);

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

            var resp = await GetWithTimeoutAsync($"https://www.nmc.cn/rest/weather?stationid={stationId}", cancellationToken).ConfigureAwait(false);
            if (resp == null) return null;
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
                Warning = real.Warn?.Alert,
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

        // 首次或未命中：信号量双检保证全量扫描只执行一次（A-73）
        if (!_stationsLoaded)
        {
            await _loadLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_stationsLoaded)
                {
                    await LoadAllStationsAsync(ct).ConfigureAwait(false);
                    _stationsLoaded = true;
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        if (_stationCache.TryGetValue(normalized, out var found)) return found;
        if (_stationCache.TryGetValue(city, out found)) return found;
        return null;
    }

    /// <summary>拉取全量省份→城市映射并填充缓存</summary>
    private async Task LoadAllStationsAsync(CancellationToken ct)
    {
        var pvResp = await GetWithTimeoutAsync("https://www.nmc.cn/rest/province", ct).ConfigureAwait(false);
        if (pvResp == null) return;
        var pvJson = await pvResp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var provinces = pvJson.ToJsonEntity<List<NmcProvince>>();
        if (provinces == null || provinces.Count == 0) return;

        var tasks = provinces
            .Where(p => !String.IsNullOrEmpty(p.Code))
            .Select(p => FetchCitiesAsync(p.Code!, ct))
            .ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var cities in results)
        {
            foreach (var c in cities)
            {
                if (c.Code.IsNullOrEmpty() || c.City.IsNullOrEmpty()) continue;
                _stationCache.TryAdd(c.City, c.Code);
                var key2 = c.City.TrimEnd('市', '区', '县', '省');
                if (key2 != c.City) _stationCache.TryAdd(key2, c.Code);
            }
        }
    }

    /// <summary>带超时保护的 GET。省份扫描/天气查询被个别慢端点卡死时，由超时中断返回 null，
    /// 调用方按失败跳过该省，避免整轮扫描被单端点拖死（默认超时见 <see cref="_requestTimeout"/>）</summary>
    /// <param name="url">请求地址</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>响应；超时或外部取消时返回 null</returns>
    private async Task<HttpResponseMessage?> GetWithTimeoutAsync(String url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_requestTimeout);
        try
        {
            return await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 单请求超时：按失败跳过，不抛出中断整轮扫描
            return null;
        }
    }

    internal async Task<List<NmcCity>> FetchCitiesAsync(String provinceCode, CancellationToken ct)
    {
        try
        {
            var resp = await GetWithTimeoutAsync($"https://www.nmc.cn/rest/province/{provinceCode}", ct).ConfigureAwait(false);
            if (resp == null) return [];
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
    internal class NmcCity { public String? Code { get; set; } public String? Province { get; set; } public String? City { get; set; } }
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
