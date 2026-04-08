#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Gateway;

/// <summary>
/// 网关响应序列化验证。
/// 架构变更：ChatResponse 为内部统一模型，属性 Messages 序列化为 messages。
/// 各协议响应通过专用 DTO（ChatCompletionResponse / AnthropicResponse / GeminiResponse）的 From 方法转换，
/// 其中 ChatCompletionResponse.Choices 序列化为 choices，符合 OpenAI 协议要求。
/// </summary>
public class GatewayResponseSerializationTests
{
    #region 辅助

    /// <summary>复现 GatewayController._snakeCaseOptions 的序列化配置（含 SystemJson.Apply 使 DataMember 生效）</summary>
    private static readonly JsonSerializerOptions _snakeCaseOptions;

    static GatewayResponseSerializationTests()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(options, true);
        _snakeCaseOptions = options;
    }

    /// <summary>模拟 OpenAiChatClient.ParseResponse 中读取 choices 的核心逻辑</summary>
    private static IList<Object>? ReadChoices(String json)
    {
        var dic = JsonParser.Decode(json);
        return dic?["choices"] as IList<Object>;
    }

    /// <summary>模拟 OpenAiChatClient.ParseResponse 读取 choices[0].message.content 作为回复文本</summary>
    private static String? ReadResponseText(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic?["choices"] is not IList<Object> choices) return null;
        if (choices.Count == 0) return null;
        if (choices[0] is not IDictionary<String, Object> choice) return null;
        if (choice["message"] is not IDictionary<String, Object> msg) return null;
        return msg["content"] as String;
    }

    #endregion

    #region ChatResponse 内部模型序列化

    [Fact]
    [DisplayName("ChatResponse.Messages 序列化为 messages（协议无关的内部模型）")]
    public void ChatResponse_Messages_SerializesAs_Messages()
    {
        var response = new ChatResponse { Id = "id-001", Object = "chat.completion", Model = "qwen3.5-flash" };
        response.Add("你好！");

        var json = JsonSerializer.Serialize(response, _snakeCaseOptions);

        // ChatResponse 是内部模型，Messages 属性按 snake_case 序列化为 messages
        Assert.Contains("\"messages\"", json);
    }

    #endregion

    #region ChatCompletionResponse 转换后序列化

    [Fact]
    [DisplayName("ChatCompletionResponse.From 将 Messages 转为 choices，客户端可正确解析")]
    public void ChatCompletionResponse_HasChoices_ClientParsesText()
    {
        var response = new ChatResponse { Id = "id-001", Object = "chat.completion", Model = "qwen3.5-flash" };
        response.Add("你好！");

        var openaiResponse = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(openaiResponse, _snakeCaseOptions);

        // 转换后包含 choices 字段
        Assert.Contains("\"choices\"", json);
        // 不包含 messages 字段（那是 ChatResponse 内部名称）
        Assert.DoesNotContain("\"messages\"", json);

        // OpenAI 客户端可正确读取
        var text = ReadResponseText(json);
        Assert.Equal("你好！", text);
    }

    [Fact]
    [DisplayName("Bug 场景：直接序列化 ChatResponse 产生 messages，客户端读 choices 为 null")]
    public void DirectChatResponse_HasMessages_ClientCannotRead()
    {
        // 模拟直接序列化 ChatResponse（不经过 ChatCompletionResponse.From）
        const String gatewayJson = """
            {
              "id": "id-001",
              "object": "chat.completion",
              "model": "qwen3.5-flash",
              "messages": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "你好！" },
                  "finish_reason": "stop"
                }
              ],
              "usage": { "total_tokens": 20 }
            }
            """;

        // OpenAI 客户端读 choices → null
        var choices = ReadChoices(gatewayJson);
        var text = ReadResponseText(gatewayJson);

        Assert.Null(choices);
        Assert.Null(text);
    }

    #endregion

    #region OpenAI 完整场景

    [Fact]
    [DisplayName("ChatCompletionResponse 包含 choices 和 prompt_tokens，客户端全链路可解析")]
    public void ChatCompletionResponse_FullScenario()
    {
        var response = new ChatResponse
        {
            Id = "8a773201-1f52-49fc-94bb-0add8330dd17",
            Object = "chat.completion",
            Model = "qwen3.5-flash",
            Usage = new UsageDetails { InputTokens = 15, OutputTokens = 10, TotalTokens = 25 },
        };
        response.Add(
            content: "你好！很高兴认识你。\n\n我是 **Qwen3.5**，由阿里巴巴集团……",
            reasoning: "Thinking Process:\n1. Analyze the Request...",
            finishReason: FinishReason.Stop);

        var openaiResponse = ChatCompletionResponse.From(response);
        var json = JsonSerializer.Serialize(openaiResponse, _snakeCaseOptions);

        // 包含 choices
        Assert.Contains("\"choices\"", json);
        Assert.DoesNotContain("\"messages\"", json);

        // 客户端可解析文本
        var text = ReadResponseText(json);
        Assert.NotNull(text);
        Assert.Contains("Qwen3.5", text);

        // Usage 使用 OpenAI 命名
        Assert.Contains("\"prompt_tokens\"", json);
        Assert.Contains("\"completion_tokens\"", json);
        Assert.DoesNotContain("\"input_tokens\"", json);
        Assert.DoesNotContain("\"output_tokens\"", json);

        // 验证 usage 数值
        var decoded = JsonParser.Decode(json);
        var usage = decoded?["usage"] as IDictionary<String, Object>;
        Assert.NotNull(usage);
        Assert.Equal(15, usage["prompt_tokens"].ToInt());
        Assert.Equal(10, usage["completion_tokens"].ToInt());
        Assert.Equal(25, usage["total_tokens"].ToInt());
    }

    #endregion
}
