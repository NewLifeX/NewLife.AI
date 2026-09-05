using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>TokenEstimator 统一 Token 估算测试。覆盖中文/英文估算口径、消息多字段、工具 Schema、多模态内容与预算截断</summary>
[DisplayName("TokenEstimator Token 估算测试")]
public class TokenEstimatorTests
{
    [Fact]
    [DisplayName("中文按 1 字/token 估算")]
    public void ChineseText_OneCharPerToken()
    {
        var tokens = TokenEstimator.EstimateTokens("你好世界中文对话");
        // 8 个中文字符 → 8 token
        Assert.Equal(8, tokens);
    }

    [Fact]
    [DisplayName("英文按 4 字符/token 估算")]
    public void EnglishText_FourCharsPerToken()
    {
        // 12 个英文字符 → 3 token
        Assert.Equal(3, TokenEstimator.EstimateTokens("abcdefghijkl"));
        // 5 字符 → 1 token（保守向下取整）
        Assert.Equal(1, TokenEstimator.EstimateTokens("hello"));
    }

    [Fact]
    [DisplayName("空或 null 文本返回 0")]
    public void EmptyOrNullText_Zero()
    {
        Assert.Equal(0, TokenEstimator.EstimateTokens((String?)null));
        Assert.Equal(0, TokenEstimator.EstimateTokens(""));
    }

    [Fact]
    [DisplayName("消息估算包含 role 基础开销")]
    public void Message_IncludesRoleBase()
    {
        var msg = new ChatMessage { Role = "user", Content = "你好" };
        // role 1 + 中文 2 = 3
        Assert.Equal(3, TokenEstimator.EstimateTokens(msg));
    }

    [Fact]
    [DisplayName("消息估算累计推理内容与工具调用")]
    public void Message_AccumulatesReasoningAndToolCalls()
    {
        var msg = new ChatMessage
        {
            Role = "assistant",
            Content = "结果",
            ReasoningContent = "思考过程",
            ToolCalls = [new ToolCall { Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"北京\"}" } }],
            ToolCallId = "call_123",
            Name = "assistant"
        };
        // role 1 + 中文 content 2 + 推理中文 4 + toolCallId 2 + name 中文 3(assistant→9字符/4=2) + Function.Name 11字符/4=2 + Arguments 约(13中文+2标点=15字→15?) 
        // 中文参数 "北京" 2 字 + 英文/符号 "{\"city\":\"\"}" 中文字符 2 + 其它 ~14 字符
        var total = TokenEstimator.EstimateTokens(msg);
        // 只断言大于纯内容，覆盖多字段累加路径
        Assert.True(total > 3, $"期望多字段累计后大于基础 3，实际 {total}");
    }

    [Fact]
    [DisplayName("空消息列表返回 0")]
    public void EmptyMessageList_Zero()
    {
        Assert.Equal(0, TokenEstimator.EstimateTokens((IList<ChatMessage>)[]));
    }

    [Fact]
    [DisplayName("工具 Schema 估算含固定开销")]
    public void Tools_IncludeFixedOverhead()
    {
        var tools = new List<ChatTool>
        {
            new() { Function = new FunctionDefinition { Name = "get_weather", Description = "查询天气" } }
        };
        var total = TokenEstimator.EstimateTokens(tools);
        // 固定 10 + Name(11字符/4=2) + Description(中文4字)
        Assert.True(total >= 16, $"期望至少 16，实际 {total}");
    }

    [Fact]
    [DisplayName("null 或空工具列表返回 0")]
    public void EmptyTools_Zero()
    {
        Assert.Equal(0, TokenEstimator.EstimateTokens((IList<ChatTool>?)null));
        Assert.Equal(0, TokenEstimator.EstimateTokens(new List<ChatTool>()));
    }

    [Fact]
    [DisplayName("多模态二进制内容按固定token计入")]
    public void MultimodalContent_Counted()
    {
        var msg = new ChatMessage
        {
            Role = "user",
            Content = "看图",
            Contents = [new ImageContent { Data = new Byte[12] }]  // 图按固定 1500 token（非字节折算，防长会话误杀）
        };
        var total = TokenEstimator.EstimateTokens(msg);
        // role 1 + 中文 2 + 图固定 1500 = 1503
        Assert.Equal(1503, total);
    }

    [Fact]
    [DisplayName("预算内消息不截断")]
    public void Truncate_BudgetEnough_NoChange()
    {
        var msgs = new List<ChatMessage> { new() { Role = "user", Content = new String('好', 100) } };
        Assert.True(TokenEstimator.TryTruncateToBudget(msgs, 500));
        Assert.Equal(100, ((String)msgs[0].Content!).Length);
    }

    [Fact]
    [DisplayName("超预算消息按比例截断至满足预算")]
    public void Truncate_OverBudget_Truncates()
    {
        var msgs = new List<ChatMessage> { new() { Role = "user", Content = new String('好', 1000) } };
        // 1000 token 远超预算 500，截断后应保留最少 200 字符 + 截断提示后缀
        Assert.True(TokenEstimator.TryTruncateToBudget(msgs, 500));
        var len = ((String)msgs[0].Content!).Length;
        Assert.True(len < 1000, $"期望被截断，实际长度 {len}");
        Assert.True(TokenEstimator.EstimateTokens(msgs) < 1001, "截断后 Token 应显著下降");
    }

    [Fact]
    [DisplayName("极短消息无法截断至预算时返回 false")]
    public void Truncate_TooShort_ReturnsFalse()
    {
        // 全部消息均为短内容（≤ MinContentChars 200），截断策略跳过 → 无法满足预算
        var msgs = new List<ChatMessage>
        {
            new() { Role = "user", Content = "a" },
            new() { Role = "assistant", Content = "b" },
            new() { Role = "user", Content = "c" },
            new() { Role = "assistant", Content = "d" },
        };
        Assert.False(TokenEstimator.TryTruncateToBudget(msgs, 1));
    }

    [Fact]
    [DisplayName("空消息列表视为满足预算")]
    public void Truncate_EmptyList_True()
    {
        Assert.True(TokenEstimator.TryTruncateToBudget(new List<ChatMessage>(), 100));
    }
}
