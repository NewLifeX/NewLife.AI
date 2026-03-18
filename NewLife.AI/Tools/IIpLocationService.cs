namespace NewLife.AI.Tools;

/// <summary>IP 归属地查询服务接口。支持多实现链式降级（太平洋电脑网 → ip-api → 远程兜底）</summary>
public interface IIpLocationService
{
    /// <summary>查询 IP 归属地信息</summary>
    /// <param name="ip">要查询的 IPv4/IPv6 地址；为 null 或空时查询本机当前公网 IP</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回归属地信息；失败或不可用返回 null，由调用方尝试下一个实现</returns>
    Task<IpLocationModel?> GetLocationAsync(String? ip, CancellationToken cancellationToken = default);
}

/// <summary>IP 归属地查询结果</summary>
public class IpLocationModel
{
    /// <summary>查询的 IP 地址</summary>
    public String? Ip { get; set; }

    /// <summary>国家</summary>
    public String? Country { get; set; }

    /// <summary>省份</summary>
    public String? Province { get; set; }

    /// <summary>城市</summary>
    public String? City { get; set; }

    /// <summary>区域/地区</summary>
    public String? Region { get; set; }

    /// <summary>完整地址描述（含运营商等附加信息）</summary>
    public String? Addr { get; set; }

    /// <summary>互联网服务提供商</summary>
    public String? Isp { get; set; }
}
