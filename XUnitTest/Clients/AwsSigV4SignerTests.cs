#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Bedrock;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>AwsSigV4Signer 单元测试（对照 AWS SigV4 签名算法验证，不需要网络）</summary>
public class AwsSigV4SignerTests
{
    // 使用固定时间戳和已知凭证进行确定性测试
    private static readonly DateTime FixedTimestamp = new(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [DisplayName("Sign_基本签名_Authorization格式正确")]
    public void Sign_BasicRequest_AuthorizationFormatCorrect()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-v2/converse");
        var headers = new Dictionary<String, String>
        {
            ["host"] = "bedrock-runtime.us-east-1.amazonaws.com",
            ["content-type"] = "application/json",
        };

        var result = AwsSigV4Signer.Sign(
            "POST", uri, headers,
            "{\"messages\":[]}",
            "AKIAIOSFODNN7EXAMPLE",
            "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
            "us-east-1", "bedrock",
            FixedTimestamp);

        Assert.NotNull(result);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20250701/us-east-1/bedrock/aws4_request", result.Authorization);
        Assert.Contains("SignedHeaders=", result.Authorization);
        Assert.Contains("Signature=", result.Authorization);
    }

    [Fact]
    [DisplayName("Sign_固定输入_Timestamp格式正确")]
    public void Sign_FixedInput_TimestampFormatCorrect()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var headers = new Dictionary<String, String> { ["host"] = "bedrock-runtime.us-east-1.amazonaws.com" };

        var result = AwsSigV4Signer.Sign(
            "POST", uri, headers, "{}",
            "AKID", "SECRET", "us-east-1", "bedrock",
            FixedTimestamp);

        Assert.Equal("20250701T120000Z", result.Timestamp);
    }

    [Fact]
    [DisplayName("Sign_空请求体_ContentHash为空字符串的SHA256")]
    public void Sign_EmptyPayload_ContentHashIsEmptySha256()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var headers = new Dictionary<String, String> { ["host"] = "bedrock-runtime.us-east-1.amazonaws.com" };

        var result = AwsSigV4Signer.Sign(
            "POST", uri, headers, "",
            "AKID", "SECRET", "us-east-1", "bedrock",
            FixedTimestamp);

        // SHA256("")  = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result.ContentHash);
    }

    [Fact]
    [DisplayName("Sign_相同输入_签名结果一致（确定性）")]
    public void Sign_SameInput_DeterministicResult()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var headers = new Dictionary<String, String>
        {
            ["host"] = "bedrock-runtime.us-east-1.amazonaws.com",
            ["content-type"] = "application/json",
        };
        var payload = "{\"messages\":[{\"role\":\"user\",\"content\":[{\"text\":\"hello\"}]}]}";

        var r1 = AwsSigV4Signer.Sign("POST", uri, headers, payload, "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);
        var r2 = AwsSigV4Signer.Sign("POST", uri, headers, payload, "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);

        Assert.Equal(r1.Authorization, r2.Authorization);
        Assert.Equal(r1.Timestamp, r2.Timestamp);
        Assert.Equal(r1.ContentHash, r2.ContentHash);
    }

    [Fact]
    [DisplayName("Sign_不同region_签名结果不同")]
    public void Sign_DifferentRegion_DifferentSignature()
    {
        var uri1 = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var uri2 = new Uri("https://bedrock-runtime.eu-west-1.amazonaws.com/model/test/converse");
        var headers1 = new Dictionary<String, String> { ["host"] = "bedrock-runtime.us-east-1.amazonaws.com" };
        var headers2 = new Dictionary<String, String> { ["host"] = "bedrock-runtime.eu-west-1.amazonaws.com" };

        var r1 = AwsSigV4Signer.Sign("POST", uri1, headers1, "{}", "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);
        var r2 = AwsSigV4Signer.Sign("POST", uri2, headers2, "{}", "AKID", "SECRET", "eu-west-1", "bedrock", FixedTimestamp);

        Assert.NotEqual(r1.Authorization, r2.Authorization);
    }

    [Fact]
    [DisplayName("Sign_不同payload_签名结果不同")]
    public void Sign_DifferentPayload_DifferentSignature()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var headers = new Dictionary<String, String> { ["host"] = "bedrock-runtime.us-east-1.amazonaws.com" };

        var r1 = AwsSigV4Signer.Sign("POST", uri, headers, "{\"a\":1}", "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);
        var r2 = AwsSigV4Signer.Sign("POST", uri, headers, "{\"b\":2}", "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);

        Assert.NotEqual(r1.ContentHash, r2.ContentHash);
        Assert.NotEqual(r1.Authorization, r2.Authorization);
    }

    [Fact]
    [DisplayName("HashSha256Hex_已知值_结果正确")]
    public void HashSha256Hex_KnownValue_CorrectResult()
    {
        // SHA256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        var hash = AwsSigV4Signer.HashSha256Hex("");
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);

        // SHA256("test") = 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08
        hash = AwsSigV4Signer.HashSha256Hex("test");
        Assert.Equal("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", hash);
    }

    [Fact]
    [DisplayName("Sign_SignedHeaders包含所有传入头_按字母排序")]
    public void Sign_SignedHeaders_ContainsAllHeaders_Sorted()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/test/converse");
        var headers = new Dictionary<String, String>
        {
            ["host"] = "bedrock-runtime.us-east-1.amazonaws.com",
            ["content-type"] = "application/json",
            ["x-custom"] = "value",
        };

        var result = AwsSigV4Signer.Sign("POST", uri, headers, "{}", "AKID", "SECRET", "us-east-1", "bedrock", FixedTimestamp);

        // SignedHeaders 应按字母序排列
        Assert.Contains("SignedHeaders=content-type;host;x-custom", result.Authorization);
    }
}
