using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Filters;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>模型层（AIContent、ChatRequest/Response、AiClientOptions 等）单元测试</summary>
[DisplayName("模型层单元测试")]
public class ModelTests
{
    // ── AIContent 子类型 ─────────────────────────────────────────────────────

    #region TextContent

    [Fact]
    [DisplayName("TextContent—构造时文本属性正确赋值")]
    public void TextContent_Constructor_SetsText()
    {
        var tc = new TextContent("hello");
        Assert.Equal("hello", tc.Text);
    }

    [Fact]
    [DisplayName("TextContent—ToString 返回文本内容")]
    public void TextContent_ToString_ReturnsText()
    {
        var tc = new TextContent("world");
        Assert.Equal("world", tc.ToString());
    }

    [Fact]
    [DisplayName("TextContent—Text 属性可写")]
    public void TextContent_Text_CanBeChanged()
    {
        var tc = new TextContent("old");
        tc.Text = "new";
        Assert.Equal("new", tc.Text);
    }

    [Fact]
    [DisplayName("TextContent—继承自 AIContent")]
    public void TextContent_IsAIContent()
    {
        var tc = new TextContent("x") as AIContent;
        Assert.NotNull(tc);
    }

    #endregion

    #region ImageContent

    [Fact]
    [DisplayName("ImageContent—默认构造所有属性为 null")]
    public void ImageContent_DefaultValues_AllNull()
    {
        var ic = new ImageContent();
        Assert.Null(ic.Uri);
        Assert.Null(ic.Data);
        Assert.Null(ic.MediaType);
        Assert.Null(ic.Detail);
    }

    [Fact]
    [DisplayName("ImageContent—设置 Uri 属性")]
    public void ImageContent_SetUri()
    {
        var ic = new ImageContent { Uri = "https://example.com/img.png" };
        Assert.Equal("https://example.com/img.png", ic.Uri);
    }

    [Fact]
    [DisplayName("ImageContent—设置 Data、MediaType、Detail")]
    public void ImageContent_SetDataAndMeta()
    {
        var bytes = new Byte[] { 1, 2, 3 };
        var ic = new ImageContent
        {
            Data = bytes,
            MediaType = "image/jpeg",
            Detail = "high"
        };
        Assert.Equal(bytes, ic.Data);
        Assert.Equal("image/jpeg", ic.MediaType);
        Assert.Equal("high", ic.Detail);
    }

    [Fact]
    [DisplayName("ImageContent—继承自 AIContent")]
    public void ImageContent_IsAIContent()
    {
        var ic = new ImageContent() as AIContent;
        Assert.NotNull(ic);
    }

    #endregion

    #region FunctionCallContent

    [Fact]
    [DisplayName("FunctionCallContent—设置 CallId、Name、Arguments")]
    public void FunctionCallContent_Properties()
    {
        var fcc = new FunctionCallContent
        {
            CallId = "call_001",
            Name = "get_weather",
            Arguments = "{\"city\":\"Beijing\"}"
        };
        Assert.Equal("call_001", fcc.CallId);
        Assert.Equal("get_weather", fcc.Name);
        Assert.Equal("{\"city\":\"Beijing\"}", fcc.Arguments);
    }

    [Fact]
    [DisplayName("FunctionCallContent—Arguments 可为 null")]
    public void FunctionCallContent_NullArguments()
    {
        var fcc = new FunctionCallContent { CallId = "c1", Name = "foo", Arguments = null };
        Assert.Null(fcc.Arguments);
    }

    [Fact]
    [DisplayName("FunctionCallContent—继承自 AIContent")]
    public void FunctionCallContent_IsAIContent()
    {
        var fcc = new FunctionCallContent { CallId = "c1", Name = "f" } as AIContent;
        Assert.NotNull(fcc);
    }

    #endregion

    #region FunctionResultContent

    [Fact]
    [DisplayName("FunctionResultContent—设置 CallId、Name、Result")]
    public void FunctionResultContent_Properties()
    {
        var frc = new FunctionResultContent
        {
            CallId = "call_001",
            Name = "get_weather",
            Result = "晴，25℃"
        };
        Assert.Equal("call_001", frc.CallId);
        Assert.Equal("get_weather", frc.Name);
        Assert.Equal("晴，25℃", frc.Result);
    }

    [Fact]
    [DisplayName("FunctionResultContent—Result 可为 null")]
    public void FunctionResultContent_NullResult()
    {
        var frc = new FunctionResultContent { CallId = "c1", Result = null };
        Assert.Null(frc.Result);
    }

    [Fact]
    [DisplayName("FunctionResultContent—继承自 AIContent")]
    public void FunctionResultContent_IsAIContent()
    {
        var frc = new FunctionResultContent { CallId = "c1" } as AIContent;
        Assert.NotNull(frc);
    }

    #endregion

    // ── ChatMessage ─────────────────────────────────────────────────────────

    #region ChatMessage

    [Fact]
    [DisplayName("ChatMessage—Contents 列表可存放多个 AIContent 片段")]
    public void ChatMessage_Contents_CanHoldMultipleItems()
    {
        var msg = new ChatMessage();
        msg.Contents = [new TextContent("part1"), new TextContent("part2")];
        Assert.Equal(2, msg.Contents.Count);
        Assert.IsType<TextContent>(msg.Contents[0]);
    }

    [Fact]
    [DisplayName("ChatMessage—ToolCalls 列表属性设置与读取")]
    public void ChatMessage_ToolCalls_ReadWrite()
    {
        var call = new ToolCall { Id = "tc1", Type = "function", Function = new FunctionCall { Name = "fn" } };
        var msg = new ChatMessage { ToolCalls = [call] };
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("tc1", msg.ToolCalls![0].Id);
    }

    [Fact]
    [DisplayName("ChatMessage—ReasoningContent 属性读写")]
    public void ChatMessage_ReasoningContent_ReadWrite()
    {
        var msg = new ChatMessage { ReasoningContent = "This is my thinking..." };
        Assert.Equal("This is my thinking...", msg.ReasoningContent);
    }

    [Fact]
    [DisplayName("ChatMessage—默认 Contents、ToolCalls 为 null")]
    public void ChatMessage_DefaultsNull()
    {
        var msg = new ChatMessage();
        Assert.Null(msg.Contents);
        Assert.Null(msg.ToolCalls);
        Assert.Null(msg.ReasoningContent);
    }

    #endregion

    // ── ChatRequest.Create ───────────────────────────────────────────────────

    #region ChatRequest.Create

    [Fact]
    [DisplayName("ChatRequest.Create—仅传消息时 Options 为空，Stream 为 false")]
    public void ChatRequest_Create_NoOptions()
    {
        var msgs = new List<ChatMessage> { new() { Role = "user", Content = "Hi" } };
        var req = ChatRequest.Create(msgs);
        Assert.Same(msgs, req.Messages);
        Assert.False(req.Stream);
        Assert.Null(req.Model);
    }

    [Fact]
    [DisplayName("ChatRequest.Create—传入 Options 时字段正确复制")]
    public void ChatRequest_Create_WithOptions()
    {
        var msgs = new List<ChatMessage> { new() { Role = "user", Content = "Hi" } };
        var opts = new ChatOptions
        {
            Model = "gpt-4o",
            Temperature = 0.7,
            MaxTokens = 512,
            User = "tester"
        };
        var req = ChatRequest.Create(msgs, opts, stream: true);
        Assert.Equal("gpt-4o", req.Model);
        Assert.Equal(0.7, req.Temperature);
        Assert.Equal(512, req.MaxTokens);
        Assert.Equal("tester", req.User);
        Assert.True(req.Stream);
    }

    [Fact]
    [DisplayName("ChatRequest.Create—Options.Tools 被复制到 Request")]
    public void ChatRequest_Create_CopiesTools()
    {
        var msgs = new List<ChatMessage>();
        var tool = new ChatTool { Function = new FunctionDefinition { Name = "search" } };
        var opts = new ChatOptions { Tools = [tool] };
        var req = ChatRequest.Create(msgs, opts);
        Assert.NotNull(req.Tools);
        Assert.Single(req.Tools!);
        Assert.Equal("search", req.Tools![0].Function?.Name);
    }

    #endregion

    // ── ChatRequest.ToChatRequest ────────────────────────────────────────────

    #region ChatRequest.ToChatRequest

    [Fact]
    [DisplayName("ToChatRequest—从 ChatCompletionRequest 正确映射基础字段")]
    public void ChatRequest_ToChatRequest_MapsBasicFields()
    {
        var cr = new ChatCompletionRequest
        {
            Model = "deepseek-chat",
            Messages = [new ChatMessage { Role = "user", Content = "Hello" }],
            Stream = true,
            Temperature = 0.5,
            MaxTokens = 1024,
        };
        var req = cr.ToChatRequest();
        Assert.Equal("deepseek-chat", req.Model);
        Assert.Single(req.Messages);
        Assert.True(req.Stream);
        Assert.Equal(0.5, req.Temperature);
        Assert.Equal(1024, req.MaxTokens);
    }

    #endregion

    // ── ChatResponse.Add / AddDelta ──────────────────────────────────────────

    #region ChatResponse.Add / AddDelta

    [Fact]
    [DisplayName("ChatResponse.Add—返回包含 Message 的 ChatChoice，Index 递增")]
    public void ChatResponse_Add_SetsMessage()
    {
        var resp = new ChatResponse();
        var choice = resp.Add("hello");
        Assert.NotNull(choice.Message);
        Assert.Equal("hello", choice.Message!.Content?.ToString());
        Assert.Equal(0, choice.Index);
        // 再次添加，Index 应为 1
        var choice2 = resp.Add("world");
        Assert.Equal(1, choice2.Index);
    }

    [Fact]
    [DisplayName("ChatResponse.Add—设置 Reasoning 时 Message.ReasoningContent 正确")]
    public void ChatResponse_Add_WithReasoning()
    {
        var resp = new ChatResponse();
        var choice = resp.Add("answer", reasoning: "thinking...");
        Assert.Equal("thinking...", choice.Message!.ReasoningContent);
    }

    [Fact]
    [DisplayName("ChatResponse.Add—设置 FinishReason 时 ChatChoice.FinishReason 正确")]
    public void ChatResponse_Add_WithFinishReason()
    {
        var resp = new ChatResponse();
        var choice = resp.Add("done", finishReeason: "stop");
        Assert.Equal("stop", choice.FinishReason);
    }

    [Fact]
    [DisplayName("ChatResponse.AddDelta—返回包含 Delta 的 ChatChoice")]
    public void ChatResponse_AddDelta_SetsData()
    {
        var resp = new ChatResponse();
        var choice = resp.AddDelta("chunk");
        Assert.NotNull(choice.Delta);
        Assert.Equal("chunk", choice.Delta!.Content?.ToString());
        Assert.Null(choice.Message);
    }

    [Fact]
    [DisplayName("ChatResponse.Text—返回第一个 Message 的内容")]
    public void ChatResponse_Text_ReturnsFirstMessageContent()
    {
        var resp = new ChatResponse();
        resp.Add("first");
        resp.Add("second");
        Assert.Equal("first", resp.Text);
    }

    [Fact]
    [DisplayName("ChatResponse.Text—Messages 为 null 时返回 null")]
    public void ChatResponse_Text_NullWhenEmpty()
    {
        var resp = new ChatResponse();
        Assert.Null(resp.Text);
    }

    #endregion

    // ── AiClientOptions.GetEndpoint ──────────────────────────────────────────

    #region AiClientOptions.GetEndpoint

    [Fact]
    [DisplayName("GetEndpoint—Endpoint 为 null 时返回默认地址")]
    public void AiClientOptions_GetEndpoint_NullReturnsDefault()
    {
        var opts = new AiClientOptions { Endpoint = null };
        Assert.Equal("https://api.openai.com", opts.GetEndpoint("https://api.openai.com"));
    }

    [Fact]
    [DisplayName("GetEndpoint—Endpoint 为空字符串时返回默认地址")]
    public void AiClientOptions_GetEndpoint_EmptyReturnsDefault()
    {
        var opts = new AiClientOptions { Endpoint = "" };
        Assert.Equal("https://default.com", opts.GetEndpoint("https://default.com"));
    }

    [Fact]
    [DisplayName("GetEndpoint—Endpoint 为空白字符时返回默认地址")]
    public void AiClientOptions_GetEndpoint_WhitespaceReturnsDefault()
    {
        var opts = new AiClientOptions { Endpoint = "   " };
        Assert.Equal("https://default.com", opts.GetEndpoint("https://default.com"));
    }

    [Fact]
    [DisplayName("GetEndpoint—设置 Endpoint 时覆盖默认地址")]
    public void AiClientOptions_GetEndpoint_OverridesDefault()
    {
        var opts = new AiClientOptions { Endpoint = "https://custom.api.com" };
        Assert.Equal("https://custom.api.com", opts.GetEndpoint("https://default.com"));
    }

    #endregion

    // ── AiClientDescriptor ───────────────────────────────────────────────────

    #region AiClientDescriptor

    [Fact]
    [DisplayName("AiClientDescriptor—ToString 返回 Code (DisplayName) 格式")]
    public void AiClientDescriptor_ToString()
    {
        var desc = new AiClientDescriptor { Code = "OpenAI", DisplayName = "OpenAI" };
        Assert.Equal("OpenAI (OpenAI)", desc.ToString());
    }

    [Fact]
    [DisplayName("AiClientDescriptor—默认 Protocol 为 OpenAI")]
    public void AiClientDescriptor_DefaultProtocol()
    {
        var desc = new AiClientDescriptor();
        Assert.Equal("OpenAI", desc.Protocol);
    }

    [Fact]
    [DisplayName("AiClientDescriptor—Models 默认为空数组")]
    public void AiClientDescriptor_DefaultModelsEmpty()
    {
        var desc = new AiClientDescriptor();
        Assert.NotNull(desc.Models);
        Assert.Empty(desc.Models);
    }

    [Fact]
    [DisplayName("AiClientDescriptor—调用未配置 Factory 时抛出 InvalidOperationException")]
    public void AiClientDescriptor_DefaultFactory_Throws()
    {
        var desc = new AiClientDescriptor { Code = "Test" };
        var opts = new AiClientOptions();
        Assert.Throws<InvalidOperationException>(() => desc.Factory(opts));
    }

    #endregion

    // ── AiModelInfo / AiProviderCapabilities ────────────────────────────────

    #region AiModelInfo

    [Fact]
    [DisplayName("AiModelInfo—record 属性正确存储")]
    public void AiModelInfo_Record_Properties()
    {
        var caps = new AiProviderCapabilities(SupportThinking: true, SupportVision: false, SupportImageGeneration: false, SupportFunctionCalling: true);
        var info = new AiModelInfo("gpt-4o", "GPT-4o", caps);
        Assert.Equal("gpt-4o", info.Model);
        Assert.Equal("GPT-4o", info.DisplayName);
        Assert.True(info.Capabilities.SupportThinking);
        Assert.True(info.Capabilities.SupportFunctionCalling);
        Assert.False(info.Capabilities.SupportVision);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—默认所有能力为 false")]
    public void AiProviderCapabilities_DefaultAllFalse()
    {
        var caps = new AiProviderCapabilities();
        Assert.False(caps.SupportThinking);
        Assert.False(caps.SupportVision);
        Assert.False(caps.SupportImageGeneration);
        Assert.False(caps.SupportFunctionCalling);
    }

    #endregion

    // ── ChatFilterContext ────────────────────────────────────────────────────

    #region ChatFilterContext

    [Fact]
    [DisplayName("ChatFilterContext—Items 索引器读写正常")]
    public void ChatFilterContext_Indexer_ReadWrite()
    {
        var ctx = new ChatFilterContext();
        ctx["key1"] = "value1";
        Assert.Equal("value1", ctx["key1"]);
    }

    [Fact]
    [DisplayName("ChatFilterContext—Items 不含键时索引器返回 null")]
    public void ChatFilterContext_Indexer_MissingKeyReturnsNull()
    {
        var ctx = new ChatFilterContext();
        Assert.Null(ctx["nonExistentKey"]);
    }

    [Fact]
    [DisplayName("ChatFilterContext—IsStreaming、UserId、ConversationId 属性可设置")]
    public void ChatFilterContext_Properties_ReadWrite()
    {
        var ctx = new ChatFilterContext
        {
            IsStreaming = true,
            UserId = "user123",
            ConversationId = "conv456"
        };
        Assert.True(ctx.IsStreaming);
        Assert.Equal("user123", ctx.UserId);
        Assert.Equal("conv456", ctx.ConversationId);
    }

    [Fact]
    [DisplayName("ChatFilterContext—Request、Response 属性可设置")]
    public void ChatFilterContext_RequestResponse_ReadWrite()
    {
        var req = new ChatRequest { Messages = [] };
        var resp = new ChatResponse { Model = "gpt-4o" };
        var ctx = new ChatFilterContext { Request = req, Response = resp };
        Assert.Same(req, ctx.Request);
        Assert.Same(resp, ctx.Response);
    }

    #endregion

    // ── FunctionInvocationContext ────────────────────────────────────────────

    #region FunctionInvocationContext

    [Fact]
    [DisplayName("FunctionInvocationContext—属性读写正常")]
    public void FunctionInvocationContext_Properties()
    {
        var fic = new FunctionInvocationContext
        {
            FunctionName = "get_weather",
            Arguments = "{\"city\":\"SH\"}",
            Result = "晴"
        };
        Assert.Equal("get_weather", fic.FunctionName);
        Assert.Equal("{\"city\":\"SH\"}", fic.Arguments);
        Assert.Equal("晴", fic.Result);
    }

    [Fact]
    [DisplayName("FunctionInvocationContext—ExtraData 字典默认为空")]
    public void FunctionInvocationContext_ExtraData_DefaultEmpty()
    {
        var fic = new FunctionInvocationContext();
        Assert.NotNull(fic.ExtraData);
        Assert.Empty(fic.ExtraData);
    }

    [Fact]
    [DisplayName("FunctionInvocationContext—ExtraData 可读写自定义键值")]
    public void FunctionInvocationContext_ExtraData_ReadWrite()
    {
        var fic = new FunctionInvocationContext();
        fic.ExtraData["meta"] = 42;
        Assert.Equal(42, fic.ExtraData["meta"]);
    }

    #endregion

    // ── ChatClientBuilder ────────────────────────────────────────────────────

    #region ChatClientBuilder

    /// <summary>最简 FakeClient，仅用于 builder 测试</summary>
    private sealed class FakeClient : IChatClient
    {
        public String ReplyText { get; }
        public FakeClient(String reply = "ok") => ReplyText = reply;

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse();
            resp.Add(ReplyText);
            return Task.FromResult<IChatResponse>(resp);
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { }
    }

    [Fact]
    [DisplayName("ChatClientBuilder—带 innerClient 构造，Build 返回传入客户端")]
    public void ChatClientBuilder_WithInner_BuildReturnsSameClient()
    {
        var inner = new FakeClient();
        var built = new ChatClientBuilder(inner).Build();
        Assert.Same(inner, built);
    }

    [Fact]
    [DisplayName("ChatClientBuilder—null innerClient 构造时抛 ArgumentNullException")]
    public void ChatClientBuilder_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatClientBuilder(null!));
    }

    [Fact]
    [DisplayName("ChatClientBuilder—空构造不设置 innerClient，Build 抛 InvalidOperationException")]
    public void ChatClientBuilder_DefaultCtor_BuildWithoutInner_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ChatClientBuilder().Build());
    }

    [Fact]
    [DisplayName("ChatClientBuilder—Use 添加 null 中间件时抛 ArgumentNullException")]
    public void ChatClientBuilder_UseNull_Throws()
    {
        var inner = new FakeClient();
        Assert.Throws<ArgumentNullException>(() => new ChatClientBuilder(inner).Use(null!));
    }

    [Fact]
    [DisplayName("ChatClientBuilder—UseFilters 链式调用 Build 返回 FilteredChatClient")]
    public void ChatClientBuilder_UseFilters_BuildsFilteredClient()
    {
        var inner = new FakeClient();
        var built = new ChatClientBuilder(inner)
            .UseFilters()
            .Build();
        Assert.IsType<NewLife.AI.Clients.FilteredChatClient>(built);
    }

    [Fact]
    [DisplayName("ChatClientBuilder—UseTools 链式调用 Build 返回 ToolChatClient")]
    public void ChatClientBuilder_UseTools_BuildsToolClient()
    {
        var inner = new FakeClient();
        var built = new ChatClientBuilder(inner)
            .UseTools()
            .Build();
        Assert.IsType<NewLife.AI.Tools.ToolChatClient>(built);
    }

    #endregion

    // ── DelegatingChatClient ─────────────────────────────────────────────────

    #region DelegatingChatClient

    /// <summary>用于测试的最简 DelegatingChatClient 实现</summary>
    private sealed class PassThroughClient : NewLife.AI.Clients.DelegatingChatClient
    {
        public PassThroughClient(IChatClient inner) : base(inner) { }
    }

    [Fact]
    [DisplayName("DelegatingChatClient—null 内层客户端抛 ArgumentNullException")]
    public void DelegatingChatClient_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PassThroughClient(null!));
    }

    [Fact]
    [DisplayName("DelegatingChatClient—GetResponseAsync 转发给内层客户端")]
    public async Task DelegatingChatClient_ForwardsGetResponse()
    {
        var inner = new FakeClient("delegated");
        var client = new PassThroughClient(inner);
        var req = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "test" }] };
        var resp = await client.GetResponseAsync(req);
        Assert.Equal("delegated", resp.Text);
    }

    [Fact]
    [DisplayName("DelegatingChatClient—Dispose 不抛异常")]
    public void DelegatingChatClient_Dispose_DoesNotThrow()
    {
        var inner = new FakeClient();
        var client = new PassThroughClient(inner);
        client.Dispose(); // should not throw
    }

    #endregion
}
