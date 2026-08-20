using System;
using System.ComponentModel;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolHelper.TryRepairJson JSON 修复助手测试</summary>
[DisplayName("JSON 修复助手测试")]
public class ToolHelperRepairTests
{
    [Theory]
    [DisplayName("TryRepairJson—去元素间多余引号")]
    [InlineData("""[{"a":"1"},"{"a":"2"}]""", """[{"a":"1"},{"a":"2"}]""")]
    [InlineData("""[{"a":"1"}, "{"a":"2"}]""", """[{"a":"1"},{"a":"2"}]""")]
    public void TryRepairJson_StrayQuote_Removed(String input, String expected)
    {
        Assert.True(ToolHelper.TryRepairJson(input, out var repaired));
        Assert.Equal(expected, repaired);
    }

    [Fact]
    [DisplayName("TryRepairJson—反转义二次转义引号")]
    public void TryRepairJson_DoubleEscaped_Unescaped()
    {
        var input = "[{\\\"a\\\":\\\"1\\\"}]";
        Assert.True(ToolHelper.TryRepairJson(input, out var repaired));
        Assert.Equal("""[{"a":"1"}]""", repaired);
    }

    [Fact]
    [DisplayName("TryRepairJson—剥掉首尾包裹引号并反转义")]
    public void TryRepairJson_Wrapped_Unwrapped()
    {
        var input = "\"{\\\"a\\\":\\\"1\\\"}\"";
        Assert.True(ToolHelper.TryRepairJson(input, out var repaired));
        Assert.Equal("""{"a":"1"}""", repaired);
    }

    [Fact]
    [DisplayName("TryRepairJson—合法 JSON 不应用修复返回 false")]
    public void TryRepairJson_ValidJson_NoRepair()
    {
        Assert.False(ToolHelper.TryRepairJson("""{"a":1,"b":[1,2]}""", out var repaired));
        Assert.Equal("""{"a":1,"b":[1,2]}""", repaired);
    }

    [Fact]
    [DisplayName("TryRepairJson—含空白的合法 JSON 不被误判为需修复")]
    public void TryRepairJson_WhitespaceJson_NoRepair()
    {
        Assert.False(ToolHelper.TryRepairJson("[1, 2, 3]", out var repaired));
        Assert.Equal("[1, 2, 3]", repaired);
        Assert.False(ToolHelper.TryRepairJson("""{"a": 1, "b": [2, 3]}""", out _));
    }

    [Fact]
    [DisplayName("TryRepairJson—空输入返回 false")]
    public void TryRepairJson_Empty_NoRepair()
    {
        Assert.False(ToolHelper.TryRepairJson("", out var repaired));
        Assert.Equal("", repaired);
    }
}
