using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>远程 IP 归属地查询实现。通过 HTTP 调用远端 API（默认 ai.newlifex.com），作为兜底方案</summary>
public class IpLocationRemoteService : IIpLocationService
{
    private readonly HttpClient _http;
    private readonly String _baseUrl;

    /// <summary>初始化远程 IP 查询服务</summary>
    /// <param name="baseUrl">远程服务基础 URL，默认 https://ai.newlifex.com</param>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public IpLocationRemoteService(String baseUrl = "https://ai.newlifex.com", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    }

    /// <summary>查询 IP 归属地信息</summary>
    /// <param name="ip">要查询的 IP 地址；为 null 或空时查询本机公网 IP</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回归属地信息；失败返回 null</returns>
    public async Task<IpLocationModel?> GetLocationAsync(String? ip, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = String.IsNullOrWhiteSpace(ip)
                ? $"{_baseUrl}/api/ip"
                : $"{_baseUrl}/api/ip?ip={Uri.EscapeDataString(ip.Trim())}";

            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<IpLocationModel>();
        }
        catch
        {
            return null;
        }
    }
}
