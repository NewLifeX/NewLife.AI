using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>远程天气查询实现。通过 HTTP 调用远端 API（默认 ai.newlifex.com），作为兜底方案</summary>
public class WeatherRemoteService : IWeatherService
{
    private readonly HttpClient _http;
    private readonly String _baseUrl;

    /// <summary>初始化远程天气查询服务</summary>
    /// <param name="baseUrl">远程服务基础 URL，默认 https://ai.newlifex.com</param>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public WeatherRemoteService(String baseUrl = "https://ai.newlifex.com", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    }

    /// <summary>获取指定城市的实时天气信息</summary>
    /// <param name="city">城市名称</param>
    /// <param name="unit">温度单位</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回天气信息；失败返回 null</returns>
    public async Task<WeatherModel?> GetWeatherAsync(String city, String unit = "C", CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/api/weather?city={Uri.EscapeDataString(city)}&unit={Uri.EscapeDataString(unit)}";
            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<WeatherModel>();
        }
        catch
        {
            return null;
        }
    }
}
