using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>GeminiResponse 模型类单元测试</summary>
[DisplayName("GeminiResponse 单元测试")]
public class GeminiResponseTests
{
    #region JSON 反序列化
    [Fact]
    [DisplayName("JSON 反序列化—标准 Gemini 响应")]
    public void JsonDeserialize_StandardResponse()
    {
        var json = @"{
            ""candidates"": [{
                ""content"": {
                    ""role"": ""model"",
                    ""parts"": [{""text"": ""Hello from Gemini!""}]
                },
                ""finishReason"": ""STOP""
            }],
            ""usageMetadata"": {
                ""promptTokenCount"": 10,
                ""candidatesTokenCount"": 5,
                ""totalTokenCount"": 15
            }
        }";

        var result = json.ToJsonEntity<GeminiResponse>();

        Assert.NotNull(result);
        Assert.NotNull(result!.Candidates);
        Assert.Single(result.Candidates!);
        Assert.Equal("STOP", result.Candidates![0].FinishReason);
        Assert.Equal("model", result.Candidates[0].Content?.Role);
        Assert.Equal("Hello from Gemini!", result.Candidates[0].Content?.Parts?[0].Text);
        Assert.NotNull(result.UsageMetadata);
        Assert.Equal(10, result.UsageMetadata!.PromptTokenCount);
        Assert.Equal(5, result.UsageMetadata.CandidatesTokenCount);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含函数调用")]
    public void JsonDeserialize_WithFunctionCall()
    {
        var json = @"{
            ""candidates"": [{
                ""content"": {
                    ""role"": ""model"",
                    ""parts"": [{
                        ""functionCall"": {
                            ""name"": ""get_weather"",
                            ""args"": {""city"": ""Beijing""}
                        }
                    }]
                },
                ""finishReason"": ""STOP""
            }]
        }";

        var result = json.ToJsonEntity<GeminiResponse>();

        Assert.NotNull(result);
        var part = result!.Candidates![0].Content!.Parts![0];
        Assert.NotNull(part.FunctionCall);
        Assert.Equal("get_weather", part.FunctionCall!.Name);
        Assert.NotNull(part.FunctionCall.Args);
    }
    #endregion

    #region ToChatResponse
    [Fact]
    [DisplayName("ToChatResponse—基本转换")]
    public void ToChatResponse_Basic()
    {
        var gr = new GeminiResponse
        {
            Candidates =
            [
                new GeminiCandidate
                {
                    Content = new GeminiResponseContent
                    {
                        Role = "model",
                        Parts = [new GeminiResponsePart { Text = "你好！" }],
                    },
                    FinishReason = "STOP",
                }
            ],
            UsageMetadata = new GeminiUsageMetadata
            {
                PromptTokenCount = 10,
                CandidatesTokenCount = 5,
                TotalTokenCount = 15,
            }
        };

        var result = gr.ToChatResponse("gemini-2.5-flash", false);

        Assert.Equal("gemini-2.5-flash", result.Model);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal(FinishReason.Stop, result.Messages![0].FinishReason);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("ToChatResponse—函数调用转换")]
    public void ToChatResponse_FunctionCall()
    {
        var gr = new GeminiResponse
        {
            Candidates =
            [
                new GeminiCandidate
                {
                    Content = new GeminiResponseContent
                    {
                        Role = "model",
                        Parts =
                        [
                            new GeminiResponsePart
                            {
                                FunctionCall = new GeminiFunctionCall
                                {
                                    Name = "get_weather",
                                    Args = new Dictionary<String, Object> { ["city"] = "Beijing" },
                                }
                            }
                        ],
                    },
                    FinishReason = "STOP",
                }
            ]
        };

        var result = gr.ToChatResponse("gemini-2.5-flash", false);

        Assert.NotNull(result.Messages);
        var msg = result.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.NotNull(msg!.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("get_weather", msg.ToolCalls![0].Function?.Name);
        Assert.Contains("Beijing", msg.ToolCalls[0].Function?.Arguments);
    }

    [Fact]
    [DisplayName("ToChatResponse—STOP 映射为 stop")]
    public void ToChatResponse_FinishReasonMapping()
    {
        var gr = new GeminiResponse
        {
            Candidates =
            [
                new GeminiCandidate
                {
                    Content = new GeminiResponseContent
                    {
                        Role = "model",
                        Parts = [new GeminiResponsePart { Text = "Done" }],
                    },
                    FinishReason = "STOP",
                }
            ]
        };

        var result = gr.ToChatResponse("gemini-2.5-flash", false);

        Assert.Equal(FinishReason.Stop, result.Messages![0].FinishReason);
    }
    #endregion
}
