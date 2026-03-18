using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>wttr.in 天气查询实现。全球 CDN，无需密钥，支持国际城市</summary>
public class WeatherWttrService : IWeatherService
{
    private readonly HttpClient _http;

    /// <summary>初始化 wttr.in 天气查询服务</summary>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public WeatherWttrService(HttpClient? httpClient = null) => _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>获取指定城市的实时天气信息</summary>
    /// <param name="city">城市名称，支持中英文</param>
    /// <param name="unit">温度单位：C（摄氏度）或 F（华氏度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回天气信息；失败返回 null</returns>
    public async Task<WeatherModel?> GetWeatherAsync(String city, String unit = "C", CancellationToken cancellationToken = default)
    {
        try
        {
            var encoded = Uri.EscapeDataString(city.Trim());
            var resp = await _http.GetAsync($"https://wttr.in/{encoded}?format=j1&lang=zh", cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = json.ToJsonEntity<WttrResponse>();
            var cur = data?.CurrentCondition?.FirstOrDefault();
            if (cur == null) return null;

            var useF = unit.EqualIgnoreCase("F");
            var area = data?.NearestArea?.FirstOrDefault();

            return new WeatherModel
            {
                City = area?.AreaName?.FirstOrDefault()?.Value ?? city,
                Country = area?.Country?.FirstOrDefault()?.Value,
                Description = cur.WeatherDesc?.FirstOrDefault()?.Value,
                Temp = useF ? $"{cur.TempF}°F" : $"{cur.TempC}°C",
                FeelsLike = useF ? $"{cur.FeelsLikeF}°F" : $"{cur.FeelsLikeC}°C",
                Humidity = $"{cur.Humidity}%",
                Wind = $"{cur.WindspeedKmph} km/h",
                Visibility = $"{cur.Visibility} km",
                UvIndex = cur.UvIndex,
                PublishTime = cur.ObservationTime,
            };
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class WttrNameValue { public String? Value { get; set; } }
    private class WttrCurrentCondition
    {
        public String? TempC { get; set; }
        public String? TempF { get; set; }
        public String? FeelsLikeC { get; set; }
        public String? FeelsLikeF { get; set; }
        public String? Humidity { get; set; }
        public String? WindspeedKmph { get; set; }
        public String? Visibility { get; set; }
        public String? UvIndex { get; set; }
        public String? ObservationTime { get; set; }
        public List<WttrNameValue>? WeatherDesc { get; set; }
    }
    private class WttrNearestArea
    {
        public List<WttrNameValue>? AreaName { get; set; }
        public List<WttrNameValue>? Country { get; set; }
    }
    private class WttrResponse
    {
        public List<WttrCurrentCondition>? CurrentCondition { get; set; }
        public List<WttrNearestArea>? NearestArea { get; set; }
    }
    #endregion
}
