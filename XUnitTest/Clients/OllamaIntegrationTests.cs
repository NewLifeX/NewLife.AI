#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>Ollama ���ط��񼯳ɲ��ԡ���Ҫ�������� Ollama ������ȡ qwen3:0.6b ģ��</summary>
/// <remarks>
/// ǰ��������
/// 1. ��װ������ Ollama��Ĭ�ϼ��� http://localhost:11434��
/// 2. ִ�� ollama pull qwen3:0.6b ��ȡģ��
/// δ��⵽ Ollama ����ʱ�����Զ�����
/// </remarks>
public class OllamaIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("Ollama")!;
    private const String Model = "qwen3.5:0.8b";

    private static readonly Boolean _ollamaAvailable = CheckOllamaAvailable();

    /// <summary>�������ӱ��� Ollama �����ж��Ƿ����</summary>
    private static Boolean CheckOllamaAvailable()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = client.GetAsync("http://localhost:11434").GetAwaiter().GetResult();
            return (Int32)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ollama �����Ƿ����</summary>
    private static Boolean HasOllama() => _ollamaAvailable;

    /// <summary>����Ĭ������ѡ��</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
    };

    /// <summary>�����򵥵��û���Ϣ����</summary>
    private static ChatRequest CreateSimpleRequest(String prompt, Int32 maxTokens = 100) => new()
    {
        Model = Model,
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
    };

    /// <summary>������ϵͳ��ʾ������</summary>
    private static ChatRequest CreateRequestWithSystem(String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = Model,
        Messages =
        [
            new ChatMessage { Role = "system", Content = systemPrompt },
            new ChatMessage { Role = "user", Content = userPrompt },
        ],
        MaxTokens = maxTokens,
    };
    /// <summary>�����ͻ��˲�ִ�з���ʽ����</summary>
    private async Task<ChatResponse> ChatAsync(ChatRequest request, AiClientOptions? opts = null)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        return await client.GetResponseAsync(request);
    }

    /// <summary>�����ͻ��˲�ִ����ʽ����</summary>
    private async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, AiClientOptions? opts = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    #region ����ʽ�Ի� - ��������

    [Fact]
    [DisplayName("����ʽ_������Ч��Ӧ")]
    public async Task ChatAsync_ReturnsValidResponse()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("��һ�仰�����Լ�");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI �ظ����ݲ�ӦΪ��");
    }

    [Fact]
    [DisplayName("����ʽ_ϵͳ��ʾ����Ч")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        if (!HasOllama()) return;

        var request = CreateRequestWithSystem(
            "You are a calculator. Only reply with the numeric result.",
            "1+1");

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("����ʽ_���ֶԻ������ı���")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
            Messages =
            [
                new ChatMessage { Role = "user", Content = "My name is Xiao Ming, remember it." },
                new ChatMessage { Role = "assistant", Content = "Got it, your name is Xiao Ming." },
                new ChatMessage { Role = "user", Content = "What is my name? Reply with only the name." },
            ],
            MaxTokens = 200,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("Xiao Ming", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ����ʽ�Ի� - ��������

    [Fact]
    [DisplayName("����_Temperature������Ч")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.Temperature = 0.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("����_TopP������Ч")]
    public async Task ChatAsync_TopP_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.TopP = 0.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("����_MaxTokens������Ч")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a story about a robot", 5);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("����_Stopֹͣ����Ч")]
    public async Task ChatAsync_Stop_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("count from 1 to 10, comma separated", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("����_���п�ѡ����ͬʱ����")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.Temperature = 0.7;
        request.TopP = 0.9;
        request.Stop = ["."];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region ����ʽ�Ի� - ��Ӧ�ṹ��֤

    [Fact]
    [DisplayName("��Ӧ�ṹ_FinishReason��ȷ����")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == "stop" || finishReason == "length",
            $"FinishReason ӦΪ stop �� length��ʵ��Ϊ: {finishReason}");
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_����ģ�ͱ�ʶ")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_������ӦId")]
    public async Task ChatAsync_Response_ContainsId()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Id));
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Object�ֶ�Ϊchat.completion")]
    public async Task ChatAsync_Response_ObjectField()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.Equal("chat.completion", response.Object);
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Choices������ȷ")]
    public async Task ChatAsync_Response_ChoiceIndex()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Message��ɫΪassistant")]
    public async Task ChatAsync_Response_MessageRole()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
    }

    [Fact]
    [DisplayName("����_����ʽ��Ӧ����Usage")]
    public async Task ChatAsync_Usage_Returned()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Usage);
        Assert.True(response.Usage.InputTokens > 0, "PromptTokens Ӧ���� 0");
        Assert.True(response.Usage.OutputTokens > 0, "CompletionTokens Ӧ���� 0");
        Assert.True(response.Usage.TotalTokens > 0, "TotalTokens Ӧ���� 0");
    }

    #endregion

    #region ��ʽ�Ի� - ��������

    [Fact]
    [DisplayName("��ʽ_���ض��Chunk")]
    public async Task ChatStreamAsync_ReturnsChunks()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a bubble sort in C#", 200);
        request.Stream = true;

        var chunks = new List<ChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        var hasContent = chunks.Any(c => c.Messages?.Any(ch =>
        {
            var text = ch.Delta?.Content as String;
            return !String.IsNullOrEmpty(text);
        }) == true);
        Assert.True(hasContent, "��ʽ��ӦӦ��������һ������ chunk");
    }

    [Fact]
    [DisplayName("��ʽ_���ݿ�ƴ��Ϊ�����ı�")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hello in English", 200);
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.Delta?.Content is String text)
                        fullContent += text;
                }
            }
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent), "ƴ�Ӻ�����ݲ�ӦΪ��");
    }

    [Fact]
    [DisplayName("��ʽ_ϵͳ��ʾ����Ч")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        if (!HasOllama()) return;

        var request = CreateRequestWithSystem("Always reply with only one word.", "hello", 200);
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.Delta?.Content is String text)
                        fullContent += text;
                }
            }
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent));
    }

    [Fact]
    [DisplayName("��ʽ_CancellationToken_���ж�")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a 500 word essay about AI", 300);
        request.Stream = true;

        using var cts = new CancellationTokenSource();
        var chunks = new List<ChatResponse>();

        try
        {
            await foreach (var chunk in ChatStreamAsync(request, null, cts.Token))
            {
                chunks.Add(chunk);
                if (chunks.Count >= 3)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Ԥ����Ϊ
        }

        Assert.True(chunks.Count >= 3, "ȡ��ǰӦ�յ����� 3 �� chunk");
    }

    #endregion

    #region ��ʽ�Ի� - �ṹ��֤

    [Fact]
    [DisplayName("��ʽ�ṹ_ÿ��Chunk����Choices")]
    public async Task ChatStreamAsync_EachChunk_HasChoices()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        request.Stream = true;

        var chunksWithChoices = 0;
        var totalChunks = 0;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            totalChunks++;
            if (chunk.Messages != null && chunk.Messages.Count > 0)
                chunksWithChoices++;
        }

        Assert.True(totalChunks > 0);
        Assert.True(chunksWithChoices > 0);
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_Chunkʹ��Delta����Message")]
    public async Task ChatStreamAsync_Chunk_UsesDelta()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        request.Stream = true;

        var hasDelta = false;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta != null)
                    hasDelta = true;
            }
        }

        Assert.True(hasDelta, "��ʽ chunk Ӧʹ�� Delta �ֶ�");
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_Object�ֶ�Ϊchat.completion.chunk")]
    public async Task ChatStreamAsync_ObjectField()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        request.Stream = true;

        String? objectField = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Object != null)
            {
                objectField = chunk.Object;
                break;
            }
        }

        Assert.NotNull(objectField);
        Assert.Equal("chat.completion.chunk", objectField);
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_���һ��Chunk����FinishReason")]
    public async Task ChatStreamAsync_LastChunk_HasFinishReason()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        request.Stream = true;

        String? lastFinishReason = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.FinishReason != null)
                        lastFinishReason = choice.FinishReason;
                }
            }
        }

        Assert.NotNull(lastFinishReason);
        Assert.True(lastFinishReason == "stop" || lastFinishReason == "length",
            $"���һ�� chunk �� FinishReason ӦΪ stop �� length��ʵ��Ϊ: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_����ģ�ͱ�ʶ")]
    public async Task ChatStreamAsync_ContainsModel()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        request.Stream = true;

        String? model = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Model != null)
            {
                model = chunk.Model;
                break;
            }
        }

        Assert.NotNull(model);
    }

    #endregion

    #region ������

    [Fact]
    [DisplayName("����_�����ڵ�ģ��_�׳�HttpRequestException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request);
        });

        Assert.Contains("Ollama", ex.Message);
    }

    [Fact]
    [DisplayName("����_��ЧEndpoint_�׳��쳣")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        var request = CreateSimpleRequest("hi");
        var options = new AiClientOptions
        {
            Endpoint = "http://localhost:19999",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("����_��ʽ�����ڵ�ģ��_�׳�HttpRequestException")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
            Stream = true,
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
            }
        });

        Assert.Contains("Ollama", ex.Message);
    }

    [Fact]
    [DisplayName("����_����Ϣ�б�_�׳��쳣")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
            Messages = [],
            MaxTokens = 200,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request);
        });
    }

    #endregion

    #region FunctionCalling

    [Fact]
    [DisplayName("FunctionCalling_���߶��屻��ȷ����")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get weather info for a city",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["city"] = new Dictionary<String, Object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "city name",
                                },
                            },
                            ["required"] = new[] { "city" },
                        },
                    },
                },
            ],
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        // qwen3:0.6b ���ܴ������ߵ��ã�Ҳ����ֱ�ӻش�
        var choice = response.Messages[0];
        if (choice.FinishReason == "tool_calls")
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            var toolCall = choice.Message.ToolCalls[0];
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_weather", toolCall.Function?.Name);
        }
    }

    #endregion

    #region OllamaProvider ������֤

    [Fact]
    [DisplayName("Provider_CodeΪOllama")]
    public void Provider_Code_IsOllama()
    {
        Assert.Equal("Ollama", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Provider_NameΪOllama")]
    public void Provider_Name_IsCorrect()
    {
        Assert.Equal("本地Ollama", _descriptor.DisplayName);
    }

    [Fact]
    [DisplayName("Provider_DefaultEndpoint��ȷ")]
    public void Provider_DefaultEndpoint_IsCorrect()
    {
        Assert.Equal("http://localhost:11434", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Provider_ApiProtocol\u4e3aOllama\u539f\u751f\u534f\u8bae")]
    public void Provider_ApiProtocol_IsChatCompletions()
    {
        // Ollama \u5ba2\u6237\u7aef\u4f7f\u7528\u539f\u751f /api/chat \u63a5\u53e3\uff0c\u534f\u8bae\u6807\u8bc6\u4e3a "Ollama"\uff0c\u975e OpenAI \u517c\u5bb9\u6a21\u5f0f
        Assert.Equal("Ollama", _descriptor.Protocol);
    }

    [Fact]
    [DisplayName("Provider_Models�б��ǿ�")]
    public void Provider_Models_NotEmpty()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
    }

    [Fact]
    [DisplayName("Provider_IAiProvider�ӿ�ʵ��")]
    public void Provider_Implements_IAiProvider()
    {
        Assert.IsType<AiClientDescriptor>(_descriptor);
    }

    #endregion

    #region Options ��֤

    [Fact]
    [DisplayName("Options_EndpointΪ��ʱʹ��Ĭ��")]
    public async Task Options_EmptyEndpoint_UsesDefault()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var options = new AiClientOptions { Endpoint = "" };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpointβ��б�ܱ���ȷ����")]
    public async Task Options_TrailingSlash_Handled()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var options = new AiClientOptions { Endpoint = "http://localhost:11434/" };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    #endregion

    #region �������ȶ���

    [Fact]
    [DisplayName("����_�������ͬʱ����")]
    public async Task ChatAsync_Concurrent_Requests()
    {
        if (!HasOllama()) return;

        var tasks = Enumerable.Range(1, 3).Select(i =>
        {
            var request = CreateSimpleRequest($"{i}+{i}=? reply with only the number", 200);
            return ChatAsync(request, CreateOptions());
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Messages);
            Assert.NotEmpty(response.Messages);
        }
    }

    [Fact]
    [DisplayName("�ȶ���_����ʽ����ʽ�������")]
    public async Task ChatAsync_And_StreamAsync_Interleaved()
    {
        if (!HasOllama()) return;

        // ����ʽ
        var request1 = CreateSimpleRequest("1+1=? reply number only", 200);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        // ��ʽ
        var request2 = CreateSimpleRequest("2+2=? reply number only", 200);
        request2.Stream = true;
        var chunks = new List<ChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2, CreateOptions()))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // �ٴη���ʽ
        var request3 = CreateSimpleRequest("3+3=? reply number only", 200);
        var response3 = await ChatAsync(request3, CreateOptions());
        Assert.NotNull(response3?.Messages);
    }

    #endregion
}
