using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>ip-api.com IP 归属地查询实现。支持国际 IP，无需密钥，有速率限制</summary>
/// <remarks>初始化 ip-api.com IP 查询服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class IpLocationIpApiService(HttpClient? httpClient = null) : IIpLocationService
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
            var url = $"http://ip-api.com/json/{Uri.EscapeDataString(ip.Trim())}";

            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = json.ToJsonEntity<IpApiResponse>();

            if (data?.Status != "success") return null;

            return new IpLocationModel
            {
                Ip = data.Query,
                Country = data.Country,
                City = data.City,
                Region = data.RegionName,
                Isp = data.Isp,
            };
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class IpApiResponse
    {
        public String? Status { get; set; }
        public String? Message { get; set; }
        public String? Country { get; set; }
        public String? RegionName { get; set; }
        public String? City { get; set; }
        public String? Isp { get; set; }
        public String? Query { get; set; }
    }
    #endregion
}
