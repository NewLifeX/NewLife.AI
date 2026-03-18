using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>太平洋电脑网 IP 归属地查询实现。国内稳定，无需密钥，JSON 格式清晰</summary>
/// <remarks>初始化太平洋电脑网 IP 查询服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class IpLocationPconlineService(HttpClient? httpClient = null) : IIpLocationService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>查询 IP 归属地信息</summary>
    /// <param name="ip">要查询的 IP 地址；为 null 或空时查询本机公网 IP</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回归属地信息；失败返回 null</returns>
    public async Task<IpLocationModel?> GetLocationAsync(String? ip, CancellationToken cancellationToken = default)
    {
        try
        {
            ip = (ip + "").Trim();
            var url = $"https://whois.pconline.com.cn/ipJson.jsp?ip={Uri.EscapeDataString(ip)}&json=true";

            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = json.ToJsonEntity<PconlineResponse>();

            if (data == null || !String.IsNullOrEmpty(data.Err)) return null;

            return new IpLocationModel
            {
                Ip = data.Ip,
                Province = data.Pro,
                City = data.City,
                Region = data.Region,
                Address = data.Addr,
            };
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class PconlineResponse
    {
        public String? Ip { get; set; }
        public String? Pro { get; set; }
        public String? ProCode { get; set; }
        public String? City { get; set; }
        public String? CityCode { get; set; }
        public String? Region { get; set; }
        public String? Addr { get; set; }
        public String? Err { get; set; }
    }
    #endregion
}
