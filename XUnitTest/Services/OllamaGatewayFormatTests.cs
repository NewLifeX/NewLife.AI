using System;
using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Services;

/// <summary>Ollama 入站协议网关格式化单元测试。覆盖非流式响应、NDJSON 流式帧、入站请求解析</summary>
public class OllamaGatewayFormatTests
{
    #region 辅助
    /// <summary>构造内容增量块</summary>
    private static ChatResponse BuildContentChunk(String model, String content)
    {
        var chunk = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow,
        };
        chunk.AddDelta(content);
        return chunk;
    }

    /// <summary>构造思考增量块</summary>
    private static ChatResponse BuildThinkingChunk(String model, String thinking)
    {
        var chunk = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow,
        };
        chunk.AddDelta(null, thinking);
        return chunk;
    }

    /// <summary>构造工具调用增量块</summary>
    private static ChatResponse BuildToolCallChunk(String model)
    {
        var chunk = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow,
        };
        chunk.AddToolCallDelta("call_1", "get_weather", "{\"city\":\"北京\"}");
        return chunk;
    }

    /// <summary>构造结束块（携带 finish_reason 与用量）</summary>
    private static ChatResponse BuildDoneChunk(String model, Int32 input = 10, Int32 output = 5)
    {
        var chunk = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow,
        };
        chunk.AddDelta(null, finishReason: FinishReason.Stop);
        chunk.Usage = new UsageDetails
        {
            InputTokens = input,
            OutputTokens = output,
            TotalTokens = input + output,
        };
        return chunk;
    }

    /// <summary>解析 NDJSON 帧字符串为 JsonElement</summary>
    private static JsonElement ParseFrame(String line)
    {
        Assert.EndsWith("\n", line);
        using var doc = JsonDocument.Parse(line.TrimEnd('\n'));
        return doc.RootElement.Clone();
    }

    /// <summary>按 ASP.NET Core 默认行为反序列化 Ollama 请求（大小写不敏感 + camelCase 匹配）</summary>
    private static OllamaChatRequest DeserializeOllama(String json)
        => JsonSerializer.Deserialize<OllamaChatRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    #endregion

    #region 非流式响应
    [Fact]
    [DisplayName("FormatResponse_OllamaChat_输出Ollama协议message结构")]
    public void FormatResponse_OllamaChat_EmitsOllamaMessage()
    {
        var result = new ChatResponse
        {
            Model = "qwen3.6-flash",
            Created = DateTimeOffset.UtcNow,
        };
        result.Add("你好世界");
        result.Usage = new UsageDetails { InputTokens = 8, OutputTokens = 4, TotalTokens = 12 };

        var json = GatewayService.FormatResponse(result, GatewayProtocol.Ollama);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("qwen3.6-flash", root.GetProperty("model").GetString());
        Assert.EndsWith("Z", root.GetProperty("created_at").GetString());
        Assert.Equal("assistant", root.GetProperty("message").GetProperty("role").GetString());
        Assert.Equal("你好世界", root.GetProperty("message").GetProperty("content").GetString());
        Assert.True(root.GetProperty("done").GetBoolean());
        Assert.Equal("stop", root.GetProperty("done_reason").GetString());
        Assert.Equal(8, root.GetProperty("prompt_eval_count").GetInt32());
        Assert.Equal(4, root.GetProperty("eval_count").GetInt32());
    }

    [Fact]
    [DisplayName("FormatResponse_OllamaChat_工具调用arguments解析为对象")]
    public void FormatResponse_OllamaChat_ToolCallsArgumentsAreObject()
    {
        var result = new ChatResponse { Model = "qwen3.6-flash" };
        var choice = result.Add(null);
        choice.Message = new ChatMessage
        {
            Role = "assistant",
            Content = null,
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"北京\"}" },
                }
            ],
        };

        var json = GatewayService.FormatResponse(result, GatewayProtocol.Ollama);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tc = root.GetProperty("message").GetProperty("tool_calls")[0];
        Assert.Equal("get_weather", tc.GetProperty("function").GetProperty("name").GetString());
        // arguments 必须是 JSON 对象而非字符串
        Assert.Equal(JsonValueKind.Object, tc.GetProperty("function").GetProperty("arguments").ValueKind);
        Assert.Equal("北京", tc.GetProperty("function").GetProperty("arguments").GetProperty("city").GetString());
    }

    [Fact]
    [DisplayName("FormatResponse_OllamaGenerate_输出response顶级字段")]
    public void FormatResponse_OllamaGenerate_EmitsResponseField()
    {
        var result = new ChatResponse { Model = "qwen3.6-flash" };
        result.Add("生成结果");
        result.Usage = new UsageDetails { InputTokens = 5, OutputTokens = 3, TotalTokens = 8 };

        var json = GatewayService.FormatResponse(result, GatewayProtocol.OllamaGenerate);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("生成结果", root.GetProperty("response").GetString());
        Assert.True(root.GetProperty("done").GetBoolean());
        Assert.Equal(5, root.GetProperty("prompt_eval_count").GetInt32());
        Assert.Equal(3, root.GetProperty("eval_count").GetInt32());
    }
    #endregion

    #region 流式 NDJSON 帧
    [Fact]
    [DisplayName("FormatStreamEvents_Ollama_内容帧输出message.content")]
    public void FormatStreamEvents_Ollama_ContentFrame()
    {
        var chunk = BuildContentChunk("qwen3.6-flash", "你好");

        var events = GatewayService.FormatStreamEvents(chunk, GatewayProtocol.Ollama);
        var frame = ParseFrame(Assert.Single(events));

        Assert.Equal("qwen3.6-flash", frame.GetProperty("model").GetString());
        Assert.Equal("assistant", frame.GetProperty("message").GetProperty("role").GetString());
        Assert.Equal("你好", frame.GetProperty("message").GetProperty("content").GetString());
        Assert.False(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    [DisplayName("FormatStreamEvents_Ollama_思考帧输出message.thinking")]
    public void FormatStreamEvents_Ollama_ThinkingFrame()
    {
        var chunk = BuildThinkingChunk("qwen3.6-flash", "思考过程");

        var events = GatewayService.FormatStreamEvents(chunk, GatewayProtocol.Ollama);
        var frame = ParseFrame(Assert.Single(events));

        Assert.Equal("思考过程", frame.GetProperty("message").GetProperty("thinking").GetString());
        Assert.False(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    [DisplayName("FormatStreamEvents_Ollama_工具帧arguments为对象")]
    public void FormatStreamEvents_Ollama_ToolCallFrame()
    {
        var chunk = BuildToolCallChunk("qwen3.6-flash");

        var events = GatewayService.FormatStreamEvents(chunk, GatewayProtocol.Ollama);
        var frame = ParseFrame(Assert.Single(events));

        var tc = frame.GetProperty("message").GetProperty("tool_calls")[0];
        Assert.Equal("get_weather", tc.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Object, tc.GetProperty("function").GetProperty("arguments").ValueKind);
        Assert.False(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    [DisplayName("FormatStreamEvents_Ollama_结束帧输出done与usage")]
    public void FormatStreamEvents_Ollama_DoneFrame()
    {
        var chunk = BuildDoneChunk("qwen3.6-flash", 10, 5);

        var events = GatewayService.FormatStreamEvents(chunk, GatewayProtocol.Ollama);
        var frame = ParseFrame(Assert.Single(events));

        Assert.True(frame.GetProperty("done").GetBoolean());
        Assert.Equal("stop", frame.GetProperty("done_reason").GetString());
        Assert.Equal(10, frame.GetProperty("prompt_eval_count").GetInt32());
        Assert.Equal(5, frame.GetProperty("eval_count").GetInt32());
    }

    [Fact]
    [DisplayName("FormatStreamEvents_OllamaGenerate_内容帧输出response顶级字段")]
    public void FormatStreamEvents_OllamaGenerate_ResponseFrame()
    {
        var chunk = BuildContentChunk("qwen3.6-flash", "流式文本");

        var events = GatewayService.FormatStreamEvents(chunk, GatewayProtocol.OllamaGenerate);
        var frame = ParseFrame(Assert.Single(events));

        Assert.Equal("流式文本", frame.GetProperty("response").GetString());
        Assert.False(frame.GetProperty("done").GetBoolean());
        Assert.False(frame.TryGetProperty("message", out _));
    }

    [Fact]
    [DisplayName("FormatStreamEnd_Ollama_返回null由message_done帧收尾")]
    public void FormatStreamEnd_Ollama_ReturnsNull()
    {
        Assert.Null(GatewayService.FormatStreamEnd(GatewayProtocol.Ollama));
        Assert.Null(GatewayService.FormatStreamEnd(GatewayProtocol.OllamaGenerate));
    }

    [Fact]
    [DisplayName("FormatStreamStart_Ollama_无开始事件")]
    public void FormatStreamStart_Ollama_ReturnsEmpty()
    {
        Assert.Empty(GatewayService.FormatStreamStart("qwen3.6-flash", GatewayProtocol.Ollama));
    }
    #endregion

    #region 入站请求解析
    [Fact]
    [DisplayName("OllamaChatRequest_消息适配_JsonBody解析为IChatRequest")]
    public void OllamaChatRequest_Messages_AdaptsFromJsonBody()
    {
        var json = """{"model":"qwen3.6-flash","messages":[{"role":"system","content":"你是助手"},{"role":"user","content":"你好"}],"stream":false}""";
        var req = DeserializeOllama(json);

        Assert.Equal("qwen3.6-flash", req.Model);
        Assert.False(req.Stream);

        var messages = ((IChatRequest)req).Messages;
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        // Content 反序列化为 JsonElement（Object? 类型），需转字符串比较
        Assert.Equal("你是助手", messages[0].Content?.ToString());
        Assert.Equal("user", messages[1].Role);
    }

    [Fact]
    [DisplayName("OllamaChatRequest_Tools_原生tools数组解析为ChatTool")]
    public void OllamaChatRequest_Tools_ParsesNativeTools()
    {
        var json = """
            {"model":"qwen3.6-flash","tools":[{"type":"function","function":{"name":"get_weather","description":"查询天气","parameters":{"type":"object","properties":{"city":{"type":"string"}}}}}],"stream":true}
            """;
        var req = DeserializeOllama(json);

        var tools = ((IChatRequest)req).Tools;
        Assert.NotNull(tools);
        var tool = Assert.Single(tools);
        Assert.Equal("function", tool.Type);
        Assert.Equal("get_weather", tool.Function?.Name);
        Assert.Equal("查询天气", tool.Function?.Description);
        Assert.NotNull(tool.Function?.Parameters);
    }

    [Fact]
    [DisplayName("OllamaChatRequest_无Tools_返回null")]
    public void OllamaChatRequest_NoTools_ReturnsNull()
    {
        var json = """{"model":"qwen3.6-flash","messages":[{"role":"user","content":"hi"}]}""";
        var req = DeserializeOllama(json);

        Assert.Null(((IChatRequest)req).Tools);
    }
    #endregion

    #region OllamaChatResponse.From
    [Fact]
    [DisplayName("OllamaChatResponse_From_转换内容与用量")]
    public void OllamaChatResponse_From_ConvertsContentAndUsage()
    {
        var result = new ChatResponse { Model = "qwen3.6-flash" };
        result.Add("回复内容", "思考链路");
        result.Usage = new UsageDetails { InputTokens = 12, OutputTokens = 6, TotalTokens = 18 };

        var resp = OllamaChatResponse.From(result);

        Assert.Equal("qwen3.6-flash", resp.Model);
        Assert.True(resp.Done);
        Assert.Equal("回复内容", resp.Message?.Content);
        Assert.Equal("思考链路", resp.Message?.Thinking);
        Assert.Equal(12, resp.PromptEvalCount);
        Assert.Equal(6, resp.EvalCount);
    }

    [Fact]
    [DisplayName("OllamaChatResponse_From_转换工具调用")]
    public void OllamaChatResponse_From_ConvertsToolCalls()
    {
        var result = new ChatResponse { Model = "qwen3.6-flash" };
        var choice = result.Add(null);
        choice.Message = new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_9",
                    Function = new FunctionCall { Name = "search", Arguments = "{\"q\":\"AI\"}" },
                }
            ],
        };

        var resp = OllamaChatResponse.From(result);
        var tc = Assert.Single(resp.Message!.ToolCalls!);
        Assert.Equal("search", tc.Function?.Name);
        Assert.NotNull(tc.Function?.Arguments);
    }
    #endregion
}
