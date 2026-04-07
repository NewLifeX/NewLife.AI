using System;
using System.ComponentModel;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>BuiltinToolService 内置工具单元测试</summary>
[DisplayName("内置工具服务测试")]
public class BuiltinToolTests
{
    private readonly BuiltinToolService _svc = new();

    // ── GetCurrentTime ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("GetCurrentTime—返回值包含所有必需字段")]
    public void GetCurrentTime_NoArgs_ContainsRequiredFields()
    {
        var result = _svc.GetCurrentTime();

        Assert.Contains("datetime:", result);
        Assert.Contains("date:", result);
        Assert.Contains("time:", result);
        Assert.Contains("dayOfWeek:", result);
        Assert.Contains("weekOfYear:", result);
        Assert.Contains("timezone:", result);
        Assert.Contains("utcOffset:", result);
        Assert.Contains("unixTimestamp:", result);
    }

    [Fact]
    [DisplayName("GetCurrentTime—null 参数不抛出异常")]
    public void GetCurrentTime_NullTimezone_NoException()
    {
        var result = _svc.GetCurrentTime();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    [DisplayName("GetCurrentTime—空字符串参数使用本地时区")]
    public void GetCurrentTime_EmptyTimezone_NoException()
    {
        var result = _svc.GetCurrentTime();
        Assert.Contains("timezone:", result);
    }

    [Fact]
    [DisplayName("GetCurrentTime—unixTimestamp 为正整数")]
    public void GetCurrentTime_UnixTimestamp_IsPositive()
    {
        var result = _svc.GetCurrentTime();

        // 从返回文本解析 unixTimestamp 值
        var prefix = "unixTimestamp: ";
        var idx = result.IndexOf(prefix);
        Assert.NotEqual(-1, idx);

        var valueStr = result.Substring(idx + prefix.Length).Trim();
        var parsed = Int64.TryParse(valueStr, out var ts);
        Assert.True(parsed, "unixTimestamp 应可解析为 Int64");
        Assert.True(ts > 0, "unixTimestamp 必须为正数");
    }

    [Fact]
    [DisplayName("GetCurrentTime—UTC 时区返回 UTC")]
    public void GetCurrentTime_UtcTimezone_ContainsUtc()
    {
        // "UTC" 是跨平台通用标识符
        var result = _svc.GetCurrentTime();
        Assert.Contains("UTC", result);
    }

    [Fact]
    [DisplayName("GetCurrentTime—无效时区回退不抛出异常")]
    public void GetCurrentTime_InvalidTimezone_FallbackGracefully()
    {
        // 应当回退到本地时区，不抛出异常
        var result = _svc.GetCurrentTime();
        Assert.Contains("datetime:", result);
    }

    [Fact]
    [DisplayName("GetCurrentTime—dayOfWeek 包含中文星期")]
    public void GetCurrentTime_DayOfWeek_ContainsChinese()
    {
        var result = _svc.GetCurrentTime();
        // 应含"星期"字样
        Assert.Contains("星期", result);
    }

    // ── Calculate ─────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("Calculate—2+2=4")]
    public void Calculate_SimpleAddition_ReturnsCorrectResult()
    {
        var result = _svc.Calculate("2+2");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("\"result\"", json);
        // 结果不含 error
        Assert.DoesNotContain("\"error\"", json);
        Assert.Contains("4", json);
    }

    [Fact]
    [DisplayName("Calculate—括号表达式 (10+5)*2=30")]
    public void Calculate_ParenthesesExpression_Returns30()
    {
        var result = _svc.Calculate("(10+5)*2");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("30", json);
        Assert.DoesNotContain("\"error\"", json);
    }

    [Fact]
    [DisplayName("Calculate—浮点数运算返回正确结果")]
    public void Calculate_FloatExpression_Works()
    {
        var result = _svc.Calculate("1.5 + 2.5");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("\"error\"", json);
        Assert.Contains("4", json);
    }

    [Fact]
    [DisplayName("Calculate—空字符串返回 error 字段")]
    public void Calculate_EmptyExpression_ReturnsError()
    {
        var result = _svc.Calculate(String.Empty);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("\"error\"", json);
    }

    [Fact]
    [DisplayName("Calculate—空白字符串返回 error 字段")]
    public void Calculate_WhitespaceExpression_ReturnsError()
    {
        var result = _svc.Calculate("   ");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("\"error\"", json);
    }

    [Fact]
    [DisplayName("Calculate—含非法字符返回 error 字段")]
    public void Calculate_IllegalCharacter_ReturnsError()
    {
        var result = _svc.Calculate("rm -rf /");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("\"error\"", json);
        Assert.Contains("invalid character", json);
    }

    [Fact]
    [DisplayName("Calculate—字母字符返回 invalid character 错误")]
    public void Calculate_LetterCharacter_ReturnsInvalidCharError()
    {
        var result = _svc.Calculate("2a+3");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("invalid character", json);
    }

    [Fact]
    [DisplayName("Calculate—除法运算结果正确")]
    public void Calculate_Division_ReturnsCorrectResult()
    {
        var result = _svc.Calculate("10 / 2");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("\"error\"", json);
        Assert.Contains("5", json);
    }

    [Fact]
    [DisplayName("Calculate—取模运算结果正确")]
    public void Calculate_Modulo_ReturnsCorrectResult()
    {
        var result = _svc.Calculate("10 % 3");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("\"error\"", json);
        Assert.Contains("1", json);
    }
}
