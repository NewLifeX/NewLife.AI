#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Gateway;

/// <summary>多协议响应 DTO 测试。验证 ChatCompletionResponse / AnthropicResponse / GeminiResponse 的转换与序列化</summary>
public class ProtocolResponseTests
{
    #region 辅助
    /// <summary>复现 GatewayController._snakeCaseOptions 的序列化配置（含 SystemJson.Apply 使 DataMember 生效）</summary>
    private static readonly JsonSerializerOptions _snakeCaseOptions;
    private static readonly JsonSerializerOptions _camelCaseOptions;

    static ProtocolResponseTests()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(options, true);
        _snakeCaseOptions = options;

        _camelCaseOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>构建标准测试用 ChatResponse（非流式）</summary>
    private static ChatResponse BuildNonStreamResponse(String content = "你好！", String model = "test-model")
    {
        var response = new ChatResponse
        {
            Id = "resp-001",
            Object = "chat.completion",
            Model = model,
            Created = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            Usage = new UsageDetails { InputTokens = 10, OutputTokens = 5, TotalTokens = 15 },
        };
        response.Add(content, finishReason: FinishReason.Stop);
        return response;
    }

    /// <summary>构建流式增量 ChatResponse（流式块）</summary>
    private static ChatResponse BuildStreamChunk(String content = "你", String? finishReason = null, String model = "test-model")
    {
        var chunk = new ChatResponse
        {
            Id = "resp-001",
            Object = "chat.completion.chunk",
            Model = model,
            Created = DateTimeOffset.FromUnixTimeSeconds(1700000000),
        };
        chunk.AddDelta(content, finishReason: FinishReasonHelper.Parse(finishReason));
        if (finishReason != null)
            chunk.Usage = new UsageDetails { InputTokens = 10, OutputTokens = 5, TotalTokens = 15 };
        return chunk;
    }
    #endregion

    #region OpenAI 非流式
    [Fact]
    [DisplayName("OpenAI 非流式：ChatCompletionResponse.From 产生 choices 字段")]
    public void OpenAI_NonStream_HasChoices()
    {
        var response = BuildNonStreamResponse();
        var result = ChatCompletionResponse.From(response);

        Assert.NotNull(result.Choices);
        Assert.Equal("resp-001", result.Id);
        Assert.Equal("chat.completion", result.Object);
        Assert.Equal("test-model", result.Model);
    }

    [Fact]
    [DisplayName("OpenAI 非流式：choices 内含 message（非 delta）")]
    public void OpenAI_NonStream_ChoicesContainMessage()
    {
        var response = BuildNonStreamResponse("Hello!");
        var result = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);

        Assert.NotNull(decoded);
        var choices = decoded["choices"] as IList<Object>;
        Assert.NotNull(choices);
        Assert.Single(choices);

        var choice = choices[0] as IDictionary<String, Object>;
        Assert.NotNull(choice);
        Assert.Equal("stop", choice["finish_reason"]);

        var message = choice["message"] as IDictionary<String, Object>;
        Assert.NotNull(message);
        Assert.Equal("Hello!", message["content"]);
    }

    [Fact]
    [DisplayName("OpenAI 非流式：usage 使用 prompt_tokens/completion_tokens 命名")]
    public void OpenAI_NonStream_UsageFieldNames()
    {
        var response = BuildNonStreamResponse();
        var result = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);

        Assert.Contains("\"prompt_tokens\"", json);
        Assert.Contains("\"completion_tokens\"", json);
        Assert.Contains("\"total_tokens\"", json);

        // 不应出现 input_tokens/output_tokens（这是 Anthropic 的命名）
        Assert.DoesNotContain("\"input_tokens\"", json);
        Assert.DoesNotContain("\"output_tokens\"", json);

        // 验证值
        var decoded = JsonParser.Decode(json);
        var usage = decoded?["usage"] as IDictionary<String, Object>;
        Assert.NotNull(usage);
        Assert.Equal(10, usage["prompt_tokens"].ToInt());
        Assert.Equal(5, usage["completion_tokens"].ToInt());
        Assert.Equal(15, usage["total_tokens"].ToInt());
    }

    [Fact]
    [DisplayName("OpenAI 非流式：客户端能正确解析 choices[0].message.content")]
    public void OpenAI_NonStream_ClientParsesText()
    {
        var response = BuildNonStreamResponse("世界你好");
        var result = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);

        var decoded = JsonParser.Decode(json);
        var choices = decoded?["choices"] as IList<Object>;
        Assert.NotNull(choices);
        var choice = choices[0] as IDictionary<String, Object>;
        var message = choice?["message"] as IDictionary<String, Object>;
        Assert.Equal("世界你好", message?["content"]);
    }
    #endregion

    #region OpenAI 流式
    [Fact]
    [DisplayName("OpenAI 流式：FromChunk 产生 object=chat.completion.chunk")]
    public void OpenAI_Stream_ChunkObject()
    {
        var chunk = BuildStreamChunk("Hello");
        var result = ChatCompletionResponse.FromChunk(chunk);
        Assert.Equal("chat.completion.chunk", result.Object);
    }

    [Fact]
    [DisplayName("OpenAI 流式：块包含 delta 字段（非 message）")]
    public void OpenAI_Stream_ChunkContainsDelta()
    {
        var chunk = BuildStreamChunk("Hi");
        var result = ChatCompletionResponse.FromChunk(chunk);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);
        var choices = decoded?["choices"] as IList<Object>;
        Assert.NotNull(choices);
        var choice = choices[0] as IDictionary<String, Object>;
        Assert.NotNull(choice);
        Assert.True(choice.ContainsKey("delta"));
        var delta = choice["delta"] as IDictionary<String, Object>;
        Assert.Equal("Hi", delta?["content"]);
    }
    #endregion

    #region Anthropic 非流式
    [Fact]
    [DisplayName("Anthropic 非流式：type 为 message，role 为 assistant")]
    public void Anthropic_NonStream_TypeIsMessage()
    {
        var response = BuildNonStreamResponse();
        var result = AnthropicResponse.From(response);
        Assert.Equal("message", result.Type);
        Assert.Equal("assistant", result.Role);
    }

    [Fact]
    [DisplayName("Anthropic 非流式：content 为数组格式 [{type:'text', text:'...'}]")]
    public void Anthropic_NonStream_ContentIsArray()
    {
        var response = BuildNonStreamResponse("Hello Anthropic");
        var result = AnthropicResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);
        Assert.NotNull(decoded);

        var content = decoded["content"] as IList<Object>;
        Assert.NotNull(content);
        Assert.Single(content);

        var block = content[0] as IDictionary<String, Object>;
        Assert.NotNull(block);
        Assert.Equal("text", block["type"]);
        Assert.Equal("Hello Anthropic", block["text"]);
    }

    [Fact]
    [DisplayName("Anthropic 非流式：usage 使用 input_tokens/output_tokens 命名")]
    public void Anthropic_NonStream_UsageFieldNames()
    {
        var response = BuildNonStreamResponse();
        var result = AnthropicResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);

        Assert.Contains("\"input_tokens\"", json);
        Assert.Contains("\"output_tokens\"", json);

        // 不应出现 prompt_tokens/completion_tokens
        Assert.DoesNotContain("\"prompt_tokens\"", json);
        Assert.DoesNotContain("\"completion_tokens\"", json);
    }

    [Fact]
    [DisplayName("Anthropic 非流式：stop_reason 映射为 end_turn")]
    public void Anthropic_NonStream_StopReason()
    {
        var response = BuildNonStreamResponse();
        var result = AnthropicResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);
        Assert.Equal("end_turn", decoded?["stop_reason"]);
    }

    [Fact]
    [DisplayName("Anthropic 非流式：序列化无 choices 字段")]
    public void Anthropic_NonStream_NoChoices()
    {
        var response = BuildNonStreamResponse();
        var result = AnthropicResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        Assert.DoesNotContain("\"choices\"", json);
    }

    [Fact]
    [DisplayName("Anthropic 非流式：包含 model 字段")]
    public void Anthropic_NonStream_HasModel()
    {
        var response = BuildNonStreamResponse("test", "claude-3-5-sonnet");
        var result = AnthropicResponse.From(response);
        Assert.Equal("claude-3-5-sonnet", result.Model);
    }
    #endregion

    #region Anthropic 流式
    [Fact]
    [DisplayName("Anthropic 流式：CreateStreamStart 返回 message_start + content_block_start")]
    public void Anthropic_Stream_StartMarker()
    {
        var events = AnthropicResponse.CreateStreamStart("claude-3");
        Assert.Equal(2, events.Count);
        Assert.Equal("message_start", events[0].EventName);
        Assert.Equal("message_start", events[0].Type);
        Assert.NotNull(events[0].Message);

        Assert.Equal("content_block_start", events[1].EventName);
        Assert.Equal("content_block_start", events[1].Type);
        Assert.NotNull(events[1].ContentBlock);
    }

    [Fact]
    [DisplayName("Anthropic 流式：CreateStreamDelta 返回 content_block_delta 事件")]
    public void Anthropic_Stream_ContentDelta()
    {
        var chunk = BuildStreamChunk("你好");
        var events = AnthropicResponse.CreateStreamDelta(chunk);

        Assert.NotEmpty(events);
        Assert.Equal("content_block_delta", events[0].EventName);
        Assert.Equal("content_block_delta", events[0].Type);
        Assert.NotNull(events[0].Delta);
        Assert.Equal("text_delta", events[0].Delta!.Type);
        Assert.Equal("你好", events[0].Delta!.Text);
    }

    [Fact]
    [DisplayName("Anthropic 流式：带 finishReason 的块产生 content_block_stop + message_delta")]
    public void Anthropic_Stream_FinishChunk()
    {
        var chunk = BuildStreamChunk("", finishReason: "stop");
        var events = AnthropicResponse.CreateStreamDelta(chunk);

        Assert.True(events.Count >= 2, $"结束块应至少产生 2 个事件，实际 {events.Count}");

        var hasStop = false;
        var hasMsgDelta = false;
        foreach (var evt in events)
        {
            if (evt.EventName == "content_block_stop") hasStop = true;
            if (evt.EventName == "message_delta") hasMsgDelta = true;
        }
        Assert.True(hasStop, "缺少 content_block_stop 事件");
        Assert.True(hasMsgDelta, "缺少 message_delta 事件");
    }

    [Fact]
    [DisplayName("Anthropic 流式：CreateStreamEnd 返回 message_stop")]
    public void Anthropic_Stream_DoneMarker()
    {
        var evt = AnthropicResponse.CreateStreamEnd();
        Assert.Equal("message_stop", evt.EventName);
        Assert.Equal("message_stop", evt.Type);
    }
    #endregion

    #region Gemini 非流式
    [Fact]
    [DisplayName("Gemini 非流式：GeminiResponse.From 产生 candidates")]
    public void Gemini_NonStream_HasCandidates()
    {
        var response = BuildNonStreamResponse();
        var result = GeminiResponse.From(response);
        Assert.NotNull(result.Candidates);
        Assert.NotEmpty(result.Candidates);
    }

    [Fact]
    [DisplayName("Gemini 非流式：candidates 结构包含 content.parts[].text")]
    public void Gemini_NonStream_CandidatesStructure()
    {
        var response = BuildNonStreamResponse("Hello Gemini");
        var result = GeminiResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);
        Assert.NotNull(decoded);

        var candidates = decoded["candidates"] as IList<Object>;
        Assert.NotNull(candidates);
        Assert.Single(candidates);

        var candidate = candidates[0] as IDictionary<String, Object>;
        Assert.NotNull(candidate);

        var content = candidate["content"] as IDictionary<String, Object>;
        Assert.NotNull(content);
        Assert.Equal("model", content["role"]);

        var parts = content["parts"] as IList<Object>;
        Assert.NotNull(parts);
        Assert.Single(parts);

        var part = parts[0] as IDictionary<String, Object>;
        Assert.NotNull(part);
        Assert.Equal("Hello Gemini", part["text"]);
    }

    [Fact]
    [DisplayName("Gemini 非流式：finishReason 映射为 STOP（camelCase 通过 _camelCaseOptions 保证）")]
    public void Gemini_NonStream_FinishReason()
    {
        var response = BuildNonStreamResponse();
        var result = GeminiResponse.From(response);
        var json = JsonSerializer.Serialize(result, _camelCaseOptions);
        var decoded = JsonParser.Decode(json);

        var candidates = decoded?["candidates"] as IList<Object>;
        var candidate = candidates?[0] as IDictionary<String, Object>;
        Assert.NotNull(candidate);
        // DataMember(Name="finishReason") 覆盖 snake_case 策略，输出 camelCase
        Assert.True(candidate.ContainsKey("finishReason"), "应包含 finishReason（camelCase）");
        Assert.Equal("STOP", candidate["finishReason"]);
    }

    [Fact]
    [DisplayName("Gemini 非流式：usageMetadata 使用 camelCase 命名")]
    public void Gemini_NonStream_UsageMetadata()
    {
        var response = BuildNonStreamResponse();
        var result = GeminiResponse.From(response);
        var json = JsonSerializer.Serialize(result, _camelCaseOptions);
        var decoded = JsonParser.Decode(json);
        Assert.NotNull(decoded);

        Assert.True(decoded.ContainsKey("usageMetadata"), "应包含 usageMetadata");
        var usage = decoded["usageMetadata"] as IDictionary<String, Object>;
        Assert.NotNull(usage);
        Assert.Equal(10, usage["promptTokenCount"].ToInt());
        Assert.Equal(5, usage["candidatesTokenCount"].ToInt());
        Assert.Equal(15, usage["totalTokenCount"].ToInt());
    }

    [Fact]
    [DisplayName("Gemini 非流式：无 choices 和 messages 字段")]
    public void Gemini_NonStream_NoChoicesOrMessages()
    {
        var response = BuildNonStreamResponse();
        var result = GeminiResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        Assert.DoesNotContain("\"choices\"", json);
        Assert.DoesNotContain("\"messages\"", json);
    }
    #endregion

    #region Gemini 流式
    [Fact]
    [DisplayName("Gemini 流式：FromChunk 产生与非流式相同的结构")]
    public void Gemini_Stream_ChunkHasCandidates()
    {
        var chunk = BuildStreamChunk("你好");
        var result = GeminiResponse.FromChunk(chunk);
        Assert.NotNull(result.Candidates);
        Assert.NotEmpty(result.Candidates);
    }
    #endregion

    #region 跨协议对比
    [Fact]
    [DisplayName("同一 ChatResponse 三种协议序列化输出互不相同")]
    public void DifferentProtocols_ProduceDifferentFormats()
    {
        var response = BuildNonStreamResponse("测试内容", "universal-model");

        var openaiJson = JsonSerializer.Serialize(ChatCompletionResponse.From(response), _snakeCaseOptions);
        var anthropicJson = JsonSerializer.Serialize(AnthropicResponse.From(response), _snakeCaseOptions);
        var geminiJson = JsonSerializer.Serialize(GeminiResponse.From(response), _snakeCaseOptions);

        // OpenAI 有 choices，无 candidates/content（顶级）
        Assert.Contains("\"choices\"", openaiJson);
        Assert.DoesNotContain("\"candidates\"", openaiJson);

        // Anthropic 有 content（顶级数组），无 choices/candidates
        Assert.Contains("\"content\"", anthropicJson);
        Assert.DoesNotContain("\"choices\"", anthropicJson);
        Assert.DoesNotContain("\"candidates\"", anthropicJson);

        // Gemini 有 candidates，无 choices
        Assert.Contains("\"candidates\"", geminiJson);
        Assert.DoesNotContain("\"choices\"", geminiJson);
    }

    [Fact]
    [DisplayName("三种协议 JSON 输出均包含回复文本")]
    public void AllProtocols_ContainResponseText()
    {
        var response = BuildNonStreamResponse("hello-test-content");

        var openaiJson = JsonSerializer.Serialize(ChatCompletionResponse.From(response), _snakeCaseOptions);
        var anthropicJson = JsonSerializer.Serialize(AnthropicResponse.From(response), _snakeCaseOptions);
        var geminiJson = JsonSerializer.Serialize(GeminiResponse.From(response), _snakeCaseOptions);

        Assert.Contains("hello-test-content", openaiJson);
        Assert.Contains("hello-test-content", anthropicJson);
        Assert.Contains("hello-test-content", geminiJson);
    }
    #endregion

    #region 边界场景
    [Fact]
    [DisplayName("空 Messages 时各协议 From 不崩溃")]
    public void EmptyMessages_DoNotCrash()
    {
        var response = new ChatResponse
        {
            Id = "empty",
            Model = "test",
            Created = DateTimeOffset.UtcNow,
        };

        var openai = ChatCompletionResponse.From(response);
        Assert.NotNull(openai);
        var openaiJson = JsonSerializer.Serialize(openai, _snakeCaseOptions);
        Assert.NotEmpty(openaiJson);

        var anthropic = AnthropicResponse.From(response);
        Assert.NotNull(anthropic);
        var anthropicJson = JsonSerializer.Serialize(anthropic, _snakeCaseOptions);
        Assert.NotEmpty(anthropicJson);

        var gemini = GeminiResponse.From(response);
        Assert.NotNull(gemini);
        var geminiJson = JsonSerializer.Serialize(gemini, _snakeCaseOptions);
        Assert.NotEmpty(geminiJson);
    }

    [Fact]
    [DisplayName("null Usage 时序列化不包含 usage 相关字段")]
    public void NullUsage_OmittedFromOutput()
    {
        var response = new ChatResponse { Id = "no-usage", Model = "test", Created = DateTimeOffset.UtcNow };
        response.Add("hi");

        var openaiJson = JsonSerializer.Serialize(ChatCompletionResponse.From(response), _snakeCaseOptions);
        Assert.DoesNotContain("\"usage\"", openaiJson);

        var geminiJson = JsonSerializer.Serialize(GeminiResponse.From(response), _snakeCaseOptions);
        Assert.DoesNotContain("\"usageMetadata\"", geminiJson);
    }

    [Fact]
    [DisplayName("finish_reason 为 length 时各协议映射正确")]
    public void FinishReasonLength_MappedCorrectly()
    {
        var response = new ChatResponse { Id = "len", Model = "test", Created = DateTimeOffset.UtcNow };
        response.Add("content", finishReason: FinishReason.Length);

        // OpenAI 保持 length
        var openaiJson = JsonSerializer.Serialize(ChatCompletionResponse.From(response), _snakeCaseOptions);
        Assert.Contains("\"length\"", openaiJson);

        // Anthropic 映射为 max_tokens
        var anthropicJson = JsonSerializer.Serialize(AnthropicResponse.From(response), _snakeCaseOptions);
        Assert.Contains("\"max_tokens\"", anthropicJson);

        // Gemini 映射为 MAX_TOKENS
        var gemini = GeminiResponse.From(response);
        Assert.Equal("MAX_TOKENS", gemini.Candidates?[0].FinishReason);
    }

    [Fact]
    [DisplayName("finish_reason 为 tool_calls 时 Anthropic 映射为 tool_use")]
    public void FinishReasonToolCalls_AnthropicMapped()
    {
        var response = new ChatResponse { Id = "tool", Model = "test", Created = DateTimeOffset.UtcNow };
        response.Add("", finishReason: FinishReason.ToolCalls);

        var anthropicJson = JsonSerializer.Serialize(AnthropicResponse.From(response), _snakeCaseOptions);
        Assert.Contains("\"tool_use\"", anthropicJson);
    }
    #endregion

    #region DashScope 全链路验证
    [Fact]
    [DisplayName("DashScope 响应经 OpenAI 格式化后，客户端能正确解析")]
    public void DashScope_OpenAIFormat_ClientParsesCorrectly()
    {
        var response = new ChatResponse
        {
            Id = "8a773201-1f52-49fc-94bb-0add8330dd17",
            Object = "chat.completion",
            Model = "qwen3.5-flash",
            Usage = new UsageDetails { InputTokens = 100, OutputTokens = 50, TotalTokens = 150 },
        };
        response.Add("你好！很高兴认识你。", finishReason: FinishReason.Stop);

        var openaiResponse = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(openaiResponse, _snakeCaseOptions);

        // 验证客户端能读到 choices[0].message.content
        var decoded = JsonParser.Decode(json);
        var choices = decoded?["choices"] as IList<Object>;
        Assert.NotNull(choices);
        var choice = choices[0] as IDictionary<String, Object>;
        var message = choice?["message"] as IDictionary<String, Object>;
        Assert.Equal("你好！很高兴认识你。", message?["content"]);

        // 验证 usage 字段名正确
        Assert.Contains("\"prompt_tokens\"", json);
        Assert.Contains("\"completion_tokens\"", json);
        var usage = decoded?["usage"] as IDictionary<String, Object>;
        Assert.Equal(100, usage?["prompt_tokens"].ToInt());
        Assert.Equal(50, usage?["completion_tokens"].ToInt());
    }

    [Fact]
    [DisplayName("DashScope 响应经 Anthropic 格式化后，结构符合 Anthropic 协议")]
    public void DashScope_AnthropicFormat_StructureCorrect()
    {
        var response = new ChatResponse
        {
            Id = "dash-001",
            Model = "qwen3.5-flash",
            Usage = new UsageDetails { InputTokens = 100, OutputTokens = 50, TotalTokens = 150 },
        };
        response.Add("你好！", finishReason: FinishReason.Stop);

        var result = AnthropicResponse.From(response);
        var json = JsonSerializer.Serialize(result, _snakeCaseOptions);
        var decoded = JsonParser.Decode(json);

        Assert.Equal("message", decoded?["type"]);
        Assert.Equal("assistant", decoded?["role"]);
        Assert.Equal("end_turn", decoded?["stop_reason"]);

        var content = decoded?["content"] as IList<Object>;
        Assert.NotNull(content);
        var block = content[0] as IDictionary<String, Object>;
        Assert.Equal("text", block?["type"]);
        Assert.Equal("你好！", block?["text"]);
    }

    [Fact]
    [DisplayName("DashScope 响应经 Gemini 格式化后，结构符合 Gemini 协议")]
    public void DashScope_GeminiFormat_StructureCorrect()
    {
        var response = new ChatResponse
        {
            Id = "dash-002",
            Model = "qwen3.5-flash",
            Usage = new UsageDetails { InputTokens = 100, OutputTokens = 50, TotalTokens = 150 },
        };
        response.Add("你好！", finishReason: FinishReason.Stop);

        var result = GeminiResponse.From(response);
        var json = JsonSerializer.Serialize(result, _camelCaseOptions);
        var decoded = JsonParser.Decode(json);

        var candidates = decoded?["candidates"] as IList<Object>;
        Assert.NotNull(candidates);
        var candidate = candidates[0] as IDictionary<String, Object>;
        Assert.NotNull(candidate);

        var content = candidate?["content"] as IDictionary<String, Object>;
        Assert.Equal("model", content?["role"]);

        var parts = content?["parts"] as IList<Object>;
        var part = parts?[0] as IDictionary<String, Object>;
        Assert.Equal("你好！", part?["text"]);
        Assert.True(candidate!.ContainsKey("finishReason"), "应包含 finishReason（camelCase）");
        Assert.Equal("STOP", candidate["finishReason"]);

        Assert.True(decoded!.ContainsKey("usageMetadata"), "应包含 usageMetadata");
        var usage = decoded["usageMetadata"] as IDictionary<String, Object>;
        Assert.Equal(100, usage?["promptTokenCount"].ToInt());
    }
    #endregion
}
