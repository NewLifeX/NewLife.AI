using System.Security.Cryptography;
using System.Text;
using NewLife.Collections;

namespace NewLife.AI.Clients.Bedrock;

/// <summary>AWS SigV4 签名工具。纯 .NET 实现，无需 AWSSDK 依赖</summary>
/// <remarks>
/// 实现 AWS Signature Version 4 签名算法，用于 Amazon Bedrock 等 AWS 服务的 HTTP 请求认证。
/// 签名流程：构建规范请求 → 构建待签名字符串 → 计算签名密钥 → 计算签名 → 生成 Authorization 头。
/// </remarks>
public static class AwsSigV4Signer
{
    private const String Algorithm = "AWS4-HMAC-SHA256";

    /// <summary>签名结果</summary>
    public class SignResult
    {
        /// <summary>Authorization 请求头值</summary>
        public String Authorization { get; set; } = "";

        /// <summary>ISO 8601 时间戳（yyyyMMddTHHmmssZ）</summary>
        public String Timestamp { get; set; } = "";

        /// <summary>请求体 SHA256 哈希（十六进制小写）</summary>
        public String ContentHash { get; set; } = "";
    }

    /// <summary>对 HTTP 请求进行 SigV4 签名</summary>
    /// <param name="method">HTTP 方法，如 POST</param>
    /// <param name="uri">完整请求 URI</param>
    /// <param name="headers">参与签名的请求头（host 必须包含）</param>
    /// <param name="payload">请求体字符串</param>
    /// <param name="accessKey">AWS Access Key ID</param>
    /// <param name="secretKey">AWS Secret Access Key</param>
    /// <param name="region">AWS 区域，如 us-east-1</param>
    /// <param name="service">AWS 服务名，如 bedrock</param>
    /// <param name="timestamp">可选指定签名时间，为空时使用 UTC 当前时间</param>
    /// <returns>签名结果，包含 Authorization、Timestamp、ContentHash</returns>
    public static SignResult Sign(
        String method,
        Uri uri,
        IDictionary<String, String> headers,
        String payload,
        String accessKey,
        String secretKey,
        String region,
        String service,
        DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");

        // Step 1: 计算请求体哈希
        var payloadHash = HashSha256Hex(payload ?? "");

        // Step 2: 构建规范请求
        var canonicalUri = uri.AbsolutePath;
        if (String.IsNullOrEmpty(canonicalUri)) canonicalUri = "/";

        var canonicalQueryString = BuildCanonicalQueryString(uri.Query);

        // 确保 host 在 headers 中
        var signHeaders = new SortedDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in headers)
        {
            signHeaders[kv.Key.ToLowerInvariant()] = kv.Value.Trim();
        }
        if (!signHeaders.ContainsKey("host"))
            signHeaders["host"] = uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port);

        var sb = Pool.StringBuilder.Get();
        var signedHeaderKeys = new List<String>();
        foreach (var kv in signHeaders)
        {
            sb.Append(kv.Key).Append(':').Append(kv.Value).Append('\n');
            signedHeaderKeys.Add(kv.Key);
        }
        var canonicalHeaders = sb.ToString();
        var signedHeaders = String.Join(";", signedHeaderKeys);
        sb.Clear();

        // 规范请求
        sb.Append(method).Append('\n');
        sb.Append(canonicalUri).Append('\n');
        sb.Append(canonicalQueryString).Append('\n');
        sb.Append(canonicalHeaders).Append('\n');
        sb.Append(signedHeaders).Append('\n');
        sb.Append(payloadHash);
        var canonicalRequest = sb.Return(true);

        // Step 3: 构建待签名字符串
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var canonicalRequestHash = HashSha256Hex(canonicalRequest);
        var stringToSign = $"{Algorithm}\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

        // Step 4: 计算签名密钥
        var signingKey = GetSigningKey(secretKey, dateStamp, region, service);

        // Step 5: 计算签名
        var signature = HmacSha256Hex(signingKey, stringToSign);

        // Step 6: 构建 Authorization
        var authorization = $"{Algorithm} Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        return new SignResult
        {
            Authorization = authorization,
            Timestamp = amzDate,
            ContentHash = payloadHash,
        };
    }

    /// <summary>构建规范查询字符串</summary>
    private static String BuildCanonicalQueryString(String query)
    {
        if (String.IsNullOrEmpty(query) || query == "?") return "";

        var q = query.TrimStart('?');
        var parts = q.Split('&');
        var sorted = new SortedDictionary<String, String>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (String.IsNullOrEmpty(part)) continue;
            var idx = part.IndexOf('=');
            if (idx < 0)
                sorted[Uri.EscapeDataString(part)] = "";
            else
                sorted[Uri.EscapeDataString(part.Substring(0, idx))] = Uri.EscapeDataString(part.Substring(idx + 1));
        }

        var sb = Pool.StringBuilder.Get();
        var first = true;
        foreach (var kv in sorted)
        {
            if (!first) sb.Append('&');
            sb.Append(kv.Key).Append('=').Append(kv.Value);
            first = false;
        }
        return sb.Return(true);
    }

    /// <summary>派生 SigV4 签名密钥。HMAC 链：Secret → Date → Region → Service → aws4_request</summary>
    private static Byte[] GetSigningKey(String secretKey, String dateStamp, String region, String service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    /// <summary>计算字符串的 SHA256 哈希（十六进制小写）</summary>
    internal static String HashSha256Hex(String data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ToHex(hash);
    }

    /// <summary>HMAC-SHA256 计算（返回字节数组）</summary>
    private static Byte[] HmacSha256(Byte[] key, String data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    /// <summary>HMAC-SHA256 计算（返回十六进制字符串）</summary>
    private static String HmacSha256Hex(Byte[] key, String data)
    {
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ToHex(hash);
    }

    /// <summary>字节数组转十六进制小写字符串</summary>
    private static String ToHex(Byte[] data)
    {
        var sb = new StringBuilder(data.Length * 2);
        for (var i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }
        return sb.ToString();
    }
}
