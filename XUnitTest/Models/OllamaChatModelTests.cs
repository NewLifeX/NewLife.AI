using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>OllamaChatRequest/Response 模型类单元测试</summary>
[DisplayName("Ollama Chat 模型单元测试")]
public class OllamaChatModelTests
{
    #region OllamaChatRequest.FromChatRequest
    [Fact]
    [DisplayName("FromChatRequest—基本字段映射")]
    public void FromChatRequest_BasicFields()
    {
        var request = new ChatRequest
        {
            Model = "qwen3:8b",
            Stream = false,
            Temperature = 0.7,
            TopP = 0.9,
            MaxTokens = 1024,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.Equal("qwen3:8b", result.Model);
        Assert.False(result.Stream);
        Assert.Single(result.Messages);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("Hello", result.Messages[0].Content?.ToString());
        Assert.NotNull(result.Options);
        Assert.Equal(0.7, result.Options!.Temperature);
        Assert.Equal(0.9, result.Options.TopP);
        Assert.Equal(1024, result.Options.NumPredict);
    }

    [Fact]
    [DisplayName("FromChatRequest—Think 参数显式传递")]
    public void FromChatRequest_ThinkParameter()
    {
        var request = new ChatRequest { Model = "qwen3:8b", EnableThinking = true };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.True(result.Think);
    }

    [Fact]
    [DisplayName("FromChatRequest—Think 为 null 时不设置")]
    public void FromChatRequest_ThinkNull()
    {
        var request = new ChatRequest { Model = "qwen3:8b" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hi" });

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.Null(result.Think);
    }

    [Fact]
    [DisplayName("FromChatRequest—无 Options 参数时不创建 Options")]
    public void FromChatRequest_NoOptions()
    {
        var request = new ChatRequest { Model = "qwen3:8b" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hi" });

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.Null(result.Options);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具调用消息中 arguments 转为对象")]
    public void FromChatRequest_ToolCallArguments()
    {
        var request = new ChatRequest { Model = "qwen3:8b" };
        var msg = new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_123",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = "{\"city\":\"Beijing\"}"
                    }
                }
            ]
        };
        request.Messages.Add(msg);

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.Single(result.Messages);
        Assert.NotNull(result.Messages[0].ToolCalls);
        Assert.Single(result.Messages[0].ToolCalls!);
        var tc = result.Messages[0].ToolCalls![0];
        Assert.Equal("call_123", tc.Id);
        Assert.Equal("function", tc.Type);
        Assert.Equal("get_weather", tc.Function?.Name);
        // arguments 应已解析为对象（非字符串）
        Assert.IsNotType<String>(tc.Function?.Arguments);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具定义转换")]
    public void FromChatRequest_Tools()
    {
        var request = new ChatRequest { Model = "qwen3:8b" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "天气" });
        request.Tools =
        [
            new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "get_weather",
                    Description = "获取天气",
                }
            }
        ];

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.NotNull(result.Tools);
        Assert.Single(result.Tools!);
        // 携带工具时应自动设置 num_predict 限制
        Assert.NotNull(result.Options);
        Assert.Equal(4096, result.Options!.NumPredict);
    }

    [Fact]
    [DisplayName("FromChatRequest—携带工具且已设置 MaxTokens 时不覆盖")]
    public void FromChatRequest_ToolsWithMaxTokens()
    {
        var request = new ChatRequest { Model = "qwen3:8b", MaxTokens = 2048 };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "天气" });
        request.Tools =
        [
            new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition { Name = "get_weather" }
            }
        ];

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.Equal(2048, result.Options!.NumPredict);
    }

    [Fact]
    [DisplayName("FromChatRequest—Stop 序列传递")]
    public void FromChatRequest_Stop()
    {
        var request = new ChatRequest
        {
            Model = "qwen3:8b",
            Stop = new List<String> { "###", "END" },
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hi" });

        var result = OllamaChatRequest.FromChatRequest(request);

        Assert.NotNull(result.Options);
        Assert.NotNull(result.Options!.Stop);
        Assert.Equal(2, result.Options.Stop!.Count);
        Assert.Equal("###", result.Options.Stop[0]);
    }
    #endregion

    #region OllamaChatResponse JSON 反序列化
    [Fact]
    [DisplayName("JSON 反序列化—标准非流式响应")]
    public void JsonDeserialize_NonStreamResponse()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": ""Hello!""
            },
            ""done"": true,
            ""done_reason"": ""stop"",
            ""prompt_eval_count"": 10,
            ""eval_count"": 5,
            ""total_duration"": 1000000000,
            ""load_duration"": 100000000,
            ""prompt_eval_duration"": 200000000,
            ""eval_duration"": 700000000
        }";

        var result = json.ToJsonEntity<OllamaChatResponse>();

        Assert.NotNull(result);
        Assert.Equal("qwen3:8b", result!.Model);
        Assert.Equal("2024-01-01T00:00:00Z", result.CreatedAt);
        Assert.True(result.Done);
        Assert.Equal("stop", result.DoneReason);
        Assert.Equal(10, result.PromptEvalCount);
        Assert.Equal(5, result.EvalCount);
        Assert.NotNull(result.Message);
        Assert.Equal("assistant", result.Message!.Role);
        Assert.Equal("Hello!", result.Message.Content?.ToString());
    }

    [Fact]
    [DisplayName("JSON 反序列化—含 thinking 字段")]
    public void JsonDeserialize_WithThinking()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": ""答案是42"",
                ""thinking"": ""让我分析一下...""
            },
            ""done"": true,
            ""done_reason"": ""stop""
        }";

        var result = json.ToJsonEntity<OllamaChatResponse>();

        Assert.NotNull(result);
        Assert.Equal("答案是42", result!.Message!.Content?.ToString());
        Assert.Equal("让我分析一下...", result.Message.Thinking);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含 tool_calls")]
    public void JsonDeserialize_WithToolCalls()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": """",
                ""tool_calls"": [
                    {
                        ""function"": {
                            ""name"": ""get_weather"",
                            ""arguments"": {""city"": ""Beijing""}
                        }
                    }
                ]
            },
            ""done"": true,
            ""done_reason"": ""stop""
        }";

        var result = json.ToJsonEntity<OllamaChatResponse>();

        Assert.NotNull(result);
        Assert.NotNull(result!.Message!.ToolCalls);
        Assert.Single(result.Message.ToolCalls!);
        Assert.Equal("get_weather", result.Message.ToolCalls![0].Function?.Name);
        Assert.NotNull(result.Message.ToolCalls[0].Function?.Arguments);
    }

    [Fact]
    [DisplayName("JSON 反序列化—流式 chunk（done=false）")]
    public void JsonDeserialize_StreamChunk()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": ""He""
            },
            ""done"": false
        }";

        var result = json.ToJsonEntity<OllamaChatResponse>();

        Assert.NotNull(result);
        Assert.False(result!.Done);
        Assert.Equal("He", result.Message!.Content?.ToString());
    }
    #endregion

    #region ToChatResponse
    [Fact]
    [DisplayName("ToChatResponse—基本非流式转换")]
    public void ToChatResponse_Basic()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            CreatedAt = "2024-01-01T00:00:00Z",
            Message = new OllamaChatMessage { Role = "assistant", Content = "Hello!" },
            Done = true,
            DoneReason = "stop",
            PromptEvalCount = 10,
            EvalCount = 5,
        };

        var result = resp.ToChatResponse();

        Assert.Equal("chat.completion", result.Object);
        Assert.Equal("qwen3:8b", result.Model);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal("stop", result.Messages![0].FinishReason);
        Assert.NotNull(result.Messages[0].Message);
        Assert.Equal("Hello!", result.Messages[0].Message!.Content?.ToString());
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
        Assert.Equal(15, result.Usage.TotalTokens);
    }

    [Fact]
    [DisplayName("ToChatResponse—thinking 映射为 ReasoningContent")]
    public void ToChatResponse_WithThinking()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = "答案是42",
                Thinking = "让我分析一下...",
            },
            Done = true,
            DoneReason = "stop",
        };

        var result = resp.ToChatResponse();

        var msg = result.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("答案是42", msg!.Content?.ToString());
        Assert.Equal("让我分析一下...", msg.ReasoningContent);
    }

    [Fact]
    [DisplayName("ToChatResponse—tool_calls 转换")]
    public void ToChatResponse_WithToolCalls()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            Message = new OllamaChatMessage
            {
                Role = "assistant",
                Content = "",
                ToolCalls =
                [
                    new OllamaToolCall
                    {
                        Function = new OllamaFunctionCall
                        {
                            Name = "get_weather",
                            Arguments = new Dictionary<String, Object> { ["city"] = "Beijing" },
                        }
                    }
                ],
            },
            Done = true,
            DoneReason = "stop",
        };

        var result = resp.ToChatResponse();

        var msg = result.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.NotNull(msg!.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("get_weather", msg.ToolCalls![0].Function?.Name);
        Assert.Contains("Beijing", msg.ToolCalls[0].Function?.Arguments);
        // Ollama 响应中无 id/type，应使用默认值
        Assert.Equal("", msg.ToolCalls[0].Id);
        Assert.Equal("function", msg.ToolCalls[0].Type);
    }

    [Fact]
    [DisplayName("ToChatResponse—无 Usage 统计时不创建 Usage")]
    public void ToChatResponse_NoUsage()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            Message = new OllamaChatMessage { Role = "assistant", Content = "Hi" },
            Done = true,
        };

        var result = resp.ToChatResponse();

        Assert.Null(result.Usage);
    }
    #endregion

    #region ToStreamChunk
    [Fact]
    [DisplayName("ToStreamChunk—中间 chunk（done=false）")]
    public void ToStreamChunk_Intermediate()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            CreatedAt = "2024-01-01T00:00:00Z",
            Message = new OllamaChatMessage { Role = "assistant", Content = "He" },
            Done = false,
        };

        var result = resp.ToStreamChunk();

        Assert.NotNull(result);
        Assert.Equal("chat.completion.chunk", result!.Object);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.NotNull(result.Messages![0].Delta);
        Assert.Equal("He", result.Messages[0].Delta!.Content?.ToString());
        Assert.Null(result.Messages[0].FinishReason);
        Assert.Null(result.Usage);
    }

    [Fact]
    [DisplayName("ToStreamChunk—最终 chunk（done=true）含 finish_reason 和 Usage")]
    public void ToStreamChunk_Final()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            Message = new OllamaChatMessage { Role = "assistant", Content = "" },
            Done = true,
            DoneReason = "stop",
            PromptEvalCount = 20,
            EvalCount = 100,
        };

        var result = resp.ToStreamChunk();

        Assert.NotNull(result);
        Assert.Equal("stop", result!.Messages![0].FinishReason);
        Assert.NotNull(result.Usage);
        Assert.Equal(20, result.Usage!.InputTokens);
        Assert.Equal(100, result.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("ToStreamChunk—done=true 但无消息时创建空 Delta")]
    public void ToStreamChunk_DoneNoMessage()
    {
        var resp = new OllamaChatResponse
        {
            Model = "qwen3:8b",
            Done = true,
            DoneReason = "stop",
        };

        var result = resp.ToStreamChunk();

        Assert.NotNull(result);
        Assert.NotNull(result!.Messages);
        Assert.NotNull(result.Messages![0].Delta);
        Assert.Equal("assistant", result.Messages[0].Delta!.Role);
        Assert.Equal("stop", result.Messages[0].FinishReason);
    }
    #endregion

    #region OllamaChatMessage.ToChatMessage
    [Fact]
    [DisplayName("ToChatMessage—基本文本消息")]
    public void ToChatMessage_BasicText()
    {
        var msg = new OllamaChatMessage
        {
            Role = "assistant",
            Content = "Hello!",
        };

        var result = msg.ToChatMessage();

        Assert.Equal("assistant", result.Role);
        Assert.Equal("Hello!", result.Content?.ToString());
        Assert.Null(result.ReasoningContent);
        Assert.Null(result.ToolCalls);
    }

    [Fact]
    [DisplayName("ToChatMessage—含 thinking")]
    public void ToChatMessage_WithThinking()
    {
        var msg = new OllamaChatMessage
        {
            Role = "assistant",
            Content = "答案",
            Thinking = "分析过程",
        };

        var result = msg.ToChatMessage();

        Assert.Equal("答案", result.Content?.ToString());
        Assert.Equal("分析过程", result.ReasoningContent);
    }

    [Fact]
    [DisplayName("ToChatMessage—含 tool_calls 且 arguments 为字典")]
    public void ToChatMessage_ToolCallsDictArguments()
    {
        var msg = new OllamaChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new OllamaToolCall
                {
                    Function = new OllamaFunctionCall
                    {
                        Name = "get_weather",
                        Arguments = new Dictionary<String, Object> { ["city"] = "Beijing" },
                    }
                }
            ],
        };

        var result = msg.ToChatMessage();

        Assert.NotNull(result.ToolCalls);
        Assert.Single(result.ToolCalls!);
        // arguments 字典应序列化为 JSON 字符串
        Assert.IsType<String>(result.ToolCalls![0].Function?.Arguments);
        Assert.Contains("Beijing", result.ToolCalls[0].Function!.Arguments);
    }

    [Fact]
    [DisplayName("ToChatMessage—含 tool_calls 且 arguments 为字符串")]
    public void ToChatMessage_ToolCallsStringArguments()
    {
        var msg = new OllamaChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new OllamaToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new OllamaFunctionCall
                    {
                        Name = "get_weather",
                        Arguments = "{\"city\":\"Shanghai\"}",
                    }
                }
            ],
        };

        var result = msg.ToChatMessage();

        Assert.Equal("call_1", result.ToolCalls![0].Id);
        Assert.Equal("function", result.ToolCalls[0].Type);
        Assert.Contains("Shanghai", result.ToolCalls[0].Function!.Arguments);
    }
    #endregion

    #region JSON 往返
    [Fact]
    [DisplayName("JSON 往返—OllamaChatResponse 反序列化后 ToChatResponse 完整流程")]
    public void JsonRoundTrip_DeserializeAndConvert()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": ""Hello!"",
                ""thinking"": ""Analyzing...""
            },
            ""done"": true,
            ""done_reason"": ""stop"",
            ""prompt_eval_count"": 10,
            ""eval_count"": 5,
            ""total_duration"": 1000000000
        }";

        var ollamaResp = json.ToJsonEntity<OllamaChatResponse>();
        Assert.NotNull(ollamaResp);

        var chatResp = ollamaResp!.ToChatResponse();

        Assert.Equal("qwen3:8b", chatResp.Model);
        Assert.Equal("chat.completion", chatResp.Object);
        Assert.NotNull(chatResp.Messages);
        Assert.Single(chatResp.Messages!);
        var msg = chatResp.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("Hello!", msg!.Content?.ToString());
        Assert.Equal("Analyzing...", msg.ReasoningContent);
        Assert.Equal("stop", chatResp.Messages[0].FinishReason);
        Assert.Equal(10, chatResp.Usage!.InputTokens);
        Assert.Equal(5, chatResp.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("JSON 往返—OllamaChatResponse 工具调用反序列化后转换")]
    public void JsonRoundTrip_ToolCalls()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:00Z"",
            ""message"": {
                ""role"": ""assistant"",
                ""content"": """",
                ""tool_calls"": [
                    {
                        ""function"": {
                            ""name"": ""get_weather"",
                            ""arguments"": {""city"": ""Beijing""}
                        }
                    }
                ]
            },
            ""done"": true,
            ""done_reason"": ""stop""
        }";

        var ollamaResp = json.ToJsonEntity<OllamaChatResponse>();
        var chatResp = ollamaResp!.ToChatResponse();

        var msg = chatResp.Messages![0].Message;
        Assert.NotNull(msg!.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("get_weather", msg.ToolCalls![0].Function?.Name);
        // arguments 应从对象转为 JSON 字符串
        Assert.Contains("Beijing", msg.ToolCalls[0].Function!.Arguments);
    }

    [Fact]
    [DisplayName("JSON 往返—流式 chunk 反序列化后 ToStreamChunk")]
    public void JsonRoundTrip_StreamChunk()
    {
        var json = @"{
            ""model"": ""qwen3:8b"",
            ""created_at"": ""2024-01-01T00:00:01Z"",
            ""message"": {""role"": ""assistant"", ""content"": ""He""},
            ""done"": false
        }";

        var ollamaResp = json.ToJsonEntity<OllamaChatResponse>();
        var chunk = ollamaResp!.ToStreamChunk();

        Assert.NotNull(chunk);
        Assert.Equal("chat.completion.chunk", chunk!.Object);
        Assert.NotNull(chunk.Messages![0].Delta);
        Assert.Equal("He", chunk.Messages[0].Delta!.Content?.ToString());
        Assert.Null(chunk.Messages[0].FinishReason);
    }
    #endregion
}
