using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>模型层补充测试——覆盖 DataContent、枚举、ChatTool、Rerank、ToolCall 等</summary>
[DisplayName("模型层补充测试")]
public class ModelExtendedTests
{
    // ── DataContent ───────────────────────────────────────────────────────

    [Fact]
    [DisplayName("DataContent—构造函数正确设置 Data 和 MediaType")]
    public void DataContent_Constructor_SetsProperties()
    {
        Byte[] data = [0x01, 0x02, 0x03];
        var content = new DataContent(data, "audio/wav");

        Assert.Same(data, content.Data);
        Assert.Equal("audio/wav", content.MediaType);
    }

    [Fact]
    [DisplayName("DataContent—继承自 AIContent")]
    public void DataContent_InheritsAIContent()
    {
        var content = new DataContent([], "application/pdf");
        Assert.IsAssignableFrom<AIContent>(content);
    }

    [Fact]
    [DisplayName("DataContent—属性可读写")]
    public void DataContent_Properties_ReadWrite()
    {
        Byte[] data1 = [1, 2];
        Byte[] data2 = [3, 4, 5];
        var content = new DataContent(data1, "image/png");

        content.Data = data2;
        content.MediaType = "image/jpeg";

        Assert.Same(data2, content.Data);
        Assert.Equal("image/jpeg", content.MediaType);
    }

    // ── ThinkingMode 枚举 ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("ThinkingMode—Auto=0, Think=1, Fast=2")]
    public void ThinkingMode_Values_Correct()
    {
        Assert.Equal(0, (Int32)ThinkingMode.Auto);
        Assert.Equal(1, (Int32)ThinkingMode.Think);
        Assert.Equal(2, (Int32)ThinkingMode.Fast);
    }

    [Fact]
    [DisplayName("ThinkingMode—共 3 个枚举值")]
    public void ThinkingMode_Count_Three()
    {
        var values = Enum.GetValues<ThinkingMode>();
        Assert.Equal(3, values.Length);
    }

    // ── FeedbackType 枚举 ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("FeedbackType—None=0, Like=1, Dislike=2")]
    public void FeedbackType_Values_Correct()
    {
        Assert.Equal(0, (Int32)FeedbackType.None);
        Assert.Equal(1, (Int32)FeedbackType.Like);
        Assert.Equal(2, (Int32)FeedbackType.Dislike);
    }

    // ── MessageStatus 枚举 ────────────────────────────────────────────────

    [Fact]
    [DisplayName("MessageStatus—Streaming=0, Done=1, Error=2")]
    public void MessageStatus_Values_Correct()
    {
        Assert.Equal(0, (Int32)MessageStatus.Streaming);
        Assert.Equal(1, (Int32)MessageStatus.Done);
        Assert.Equal(2, (Int32)MessageStatus.Error);
    }

    // ── ToolCallStatus 枚举 ───────────────────────────────────────────────

    [Fact]
    [DisplayName("ToolCallStatus—Calling=0, Done=1, Error=2")]
    public void ToolCallStatus_Values_Correct()
    {
        Assert.Equal(0, (Int32)ToolCallStatus.Calling);
        Assert.Equal(1, (Int32)ToolCallStatus.Done);
        Assert.Equal(2, (Int32)ToolCallStatus.Error);
    }

    // ── McpTransportType 枚举 ─────────────────────────────────────────────

    [Fact]
    [DisplayName("McpTransportType—Http=0, Sse=1, Stdio=2")]
    public void McpTransportType_Values_Correct()
    {
        Assert.Equal(0, (Int32)McpTransportType.Http);
        Assert.Equal(1, (Int32)McpTransportType.Sse);
        Assert.Equal(2, (Int32)McpTransportType.Stdio);
    }

    // ── ChatTool ──────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("ChatTool—默认 Type 为 function")]
    public void ChatTool_DefaultType_IsFunction()
    {
        var tool = new ChatTool();
        Assert.Equal("function", tool.Type);
    }

    [Fact]
    [DisplayName("ChatTool—Function 属性读写")]
    public void ChatTool_Function_ReadWrite()
    {
        var def = new FunctionDefinition { Name = "get_weather", Description = "获取天气" };
        var tool = new ChatTool { Function = def };

        Assert.Same(def, tool.Function);
        Assert.Equal("get_weather", tool.Function.Name);
        Assert.Equal("获取天气", tool.Function.Description);
    }

    [Fact]
    [DisplayName("ChatTool—Mcp 属性读写")]
    public void ChatTool_Mcp_ReadWrite()
    {
        var mcp = new McpToolConfig { ServerUrl = "https://mcp.example.com", ServerId = "srv-1" };
        var tool = new ChatTool { Type = "mcp", Mcp = mcp };

        Assert.Equal("mcp", tool.Type);
        Assert.Equal("https://mcp.example.com", tool.Mcp!.ServerUrl);
        Assert.Equal("srv-1", tool.Mcp.ServerId);
    }

    // ── FunctionDefinition ────────────────────────────────────────────────

    [Fact]
    [DisplayName("FunctionDefinition—属性读写正确")]
    public void FunctionDefinition_Properties_ReadWrite()
    {
        var def = new FunctionDefinition
        {
            Name = "calculator",
            Description = "计算数学表达式",
            Parameters = new Dictionary<String, Object> { ["type"] = "object" },
        };

        Assert.Equal("calculator", def.Name);
        Assert.Equal("计算数学表达式", def.Description);
        Assert.NotNull(def.Parameters);
    }

    // ── McpToolConfig ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("McpToolConfig—属性读写正确")]
    public void McpToolConfig_Properties_ReadWrite()
    {
        var config = new McpToolConfig
        {
            ServerUrl = "https://mcp.example.com",
            ServerId = "my-mcp-server",
            AllowedTools = ["tool_a", "tool_b"],
            Authorization = new McpAuthConfig { Type = "bearer", Token = "tok-123" },
        };

        Assert.Equal("https://mcp.example.com", config.ServerUrl);
        Assert.Equal("my-mcp-server", config.ServerId);
        Assert.Equal(2, config.AllowedTools!.Count);
        Assert.Equal("bearer", config.Authorization!.Type);
        Assert.Equal("tok-123", config.Authorization.Token);
    }

    [Fact]
    [DisplayName("McpToolConfig—默认 AllowedTools 为 null")]
    public void McpToolConfig_AllowedTools_DefaultNull()
    {
        var config = new McpToolConfig();
        Assert.Null(config.AllowedTools);
    }

    // ── McpAuthConfig ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("McpAuthConfig—Type 和 Token 属性读写")]
    public void McpAuthConfig_Properties_ReadWrite()
    {
        var auth = new McpAuthConfig { Type = "bearer", Token = "my-token" };
        Assert.Equal("bearer", auth.Type);
        Assert.Equal("my-token", auth.Token);
    }

    // ── ToolCall / FunctionCall ───────────────────────────────────────────

    [Fact]
    [DisplayName("ToolCall—默认 Type 为 function")]
    public void ToolCall_DefaultType_IsFunction()
    {
        var tc = new ToolCall();
        Assert.Equal("function", tc.Type);
    }

    [Fact]
    [DisplayName("ToolCall—Id 和 Function 属性读写")]
    public void ToolCall_Properties_ReadWrite()
    {
        var tc = new ToolCall
        {
            Id = "call_abc",
            Function = new FunctionCall { Name = "search", Arguments = "{\"query\":\"AI\"}" },
        };

        Assert.Equal("call_abc", tc.Id);
        Assert.Equal("search", tc.Function!.Name);
        Assert.Equal("{\"query\":\"AI\"}", tc.Function.Arguments);
    }

    [Fact]
    [DisplayName("FunctionCall—Name 和 Arguments 属性读写")]
    public void FunctionCall_Properties_ReadWrite()
    {
        var fc = new FunctionCall { Name = "translate", Arguments = "{\"text\":\"hello\"}" };

        Assert.Equal("translate", fc.Name);
        Assert.Equal("{\"text\":\"hello\"}", fc.Arguments);
    }

    // ── RerankRequest ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("RerankRequest—Query 和 Documents 属性读写")]
    public void RerankRequest_Properties_ReadWrite()
    {
        var req = new RerankRequest
        {
            Model = "gte-rerank",
            Query = "自然语言处理",
            Documents = ["文本A", "文本B", "文本C"],
            TopN = 2,
            ReturnDocuments = false,
        };

        Assert.Equal("gte-rerank", req.Model);
        Assert.Equal("自然语言处理", req.Query);
        Assert.Equal(3, req.Documents.Count);
        Assert.Equal(2, req.TopN);
        Assert.False(req.ReturnDocuments);
    }

    [Fact]
    [DisplayName("RerankRequest—ReturnDocuments 默认 true")]
    public void RerankRequest_ReturnDocuments_DefaultTrue()
    {
        var req = new RerankRequest { Query = "test", Documents = [] };
        Assert.True(req.ReturnDocuments);
    }

    // ── RerankResponse ────────────────────────────────────────────────────

    [Fact]
    [DisplayName("RerankResponse—Results 初始为空列表")]
    public void RerankResponse_Results_DefaultEmpty()
    {
        var resp = new RerankResponse();
        Assert.NotNull(resp.Results);
        Assert.Empty(resp.Results);
    }

    [Fact]
    [DisplayName("RerankResponse—Results 可正常添加")]
    public void RerankResponse_Results_CanAdd()
    {
        var resp = new RerankResponse
        {
            RequestId = "req-001",
            Results =
            [
                new RerankResult { Index = 0, RelevanceScore = 0.95, Document = "文本A" },
                new RerankResult { Index = 2, RelevanceScore = 0.72, Document = "文本C" },
            ],
            Usage = new RerankUsage { TotalTokens = 150 },
        };

        Assert.Equal("req-001", resp.RequestId);
        Assert.Equal(2, resp.Results.Count);
        Assert.Equal(0.95, resp.Results[0].RelevanceScore);
        Assert.Equal(150, resp.Usage!.TotalTokens);
    }

    // ── RerankResult ──────────────────────────────────────────────────────

    [Fact]
    [DisplayName("RerankResult—属性读写正确")]
    public void RerankResult_Properties_ReadWrite()
    {
        var result = new RerankResult
        {
            Index = 1,
            RelevanceScore = 0.88,
            Document = "这是一段示例文本",
        };

        Assert.Equal(1, result.Index);
        Assert.Equal(0.88, result.RelevanceScore);
        Assert.Equal("这是一段示例文本", result.Document);
    }

    // ── RerankUsage ───────────────────────────────────────────────────────

    [Fact]
    [DisplayName("RerankUsage—TotalTokens 默认为 0")]
    public void RerankUsage_TotalTokens_DefaultZero()
    {
        var usage = new RerankUsage();
        Assert.Equal(0, usage.TotalTokens);
    }

    [Fact]
    [DisplayName("RerankUsage—TotalTokens 可读写")]
    public void RerankUsage_TotalTokens_ReadWrite()
    {
        var usage = new RerankUsage { TotalTokens = 300 };
        Assert.Equal(300, usage.TotalTokens);
    }
}
