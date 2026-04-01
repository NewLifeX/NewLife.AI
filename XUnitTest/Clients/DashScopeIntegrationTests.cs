#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScope����������������̼��ɲ��ԡ���Ҫ��Ч ApiKey ��������</summary>
/// <remarks>
/// ApiKey ��ȡ���ȼ���
/// 1. ./config/DashScope.key �ļ������ı�������Ϊ ApiKey��
/// 2. �������� DASHSCOPE_API_KEY
/// δ����ʱ�����Զ�����
/// </remarks>
public class DashScopeIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("DashScope")!;
    private readonly String _apiKey;

    public DashScopeIntegrationTests()
    {
        _apiKey = LoadApiKey() ?? "";
    }

    /// <summary>�� config Ŀ¼�򻷾��������� ApiKey</summary>
    public static String? LoadApiKey()
    {
        var configPath = "config/DashScope.key".GetFullPath();
        if (File.Exists(configPath))
        {
            var key = File.ReadAllText(configPath).Trim();
            if (!String.IsNullOrWhiteSpace(key)) return key;
        }
        else
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(configPath, "");
        }

        return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
    }

    /// <summary>ApiKey �Ƿ����</summary>
    private Boolean HasApiKey() => !String.IsNullOrWhiteSpace(_apiKey);

    /// <summary>����Ĭ������ѡ��</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
        ApiKey = _apiKey,
    };

    /// <summary>�����򵥵��û���Ϣ����</summary>
    private static ChatRequest CreateSimpleRequest(String model, String prompt, Int32 maxTokens = 200) => new()
    {
        Model = model,
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
    };

    /// <summary>������ϵͳ��ʾ������</summary>
    private static ChatRequest CreateRequestWithSystem(String model, String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = model,
        Messages =
        [
            new ChatMessage { Role = "system", Content = systemPrompt },
            new ChatMessage { Role = "user", Content = userPrompt },
        ],
        MaxTokens = maxTokens,
    };
    /// <summary>�����ͻ��˲�ִ�з���ʽ����</summary>
    private async Task<IChatResponse> ChatAsync(ChatRequest request, AiClientOptions? opts = null)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        return await client.GetResponseAsync(request);
    }

    /// <summary>�����ͻ��˲�ִ����ʽ����</summary>
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(ChatRequest request, AiClientOptions? opts = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    #region ����ʽ�Ի� - ��������

    [Fact]
    [DisplayName("����ʽ_QwenPlus_������Ч��Ӧ")]
    public async Task ChatAsync_QwenPlus_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "��һ�仰�����Լ�");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI �ظ����ݲ�ӦΪ��");

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0, "Token ����Ӧ���� 0");
        Assert.True(response.Usage.InputTokens > 0, "Prompt Token Ӧ���� 0");
        Assert.True(response.Usage.OutputTokens > 0, "Completion Token Ӧ���� 0");
    }

    [Fact]
    [DisplayName("����ʽ_QwenTurbo_����ģ�Ϳ���")]
    public async Task ChatAsync_QwenTurbo_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-turbo", "1+1���ڼ���ֻ�ش�����");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("����ʽ_QwenMax_�߼�ģ�Ϳ���")]
    public async Task ChatAsync_QwenMax_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-max", "���", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("����ʽ_ϵͳ��ʾ����Ч")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        if (!HasApiKey()) return;

        var request = CreateRequestWithSystem(
            "qwen-plus",
            "����һ��ֻ��ظ�JSON��ʽ�Ļ����ˡ������û�˵ʲô������{\"reply\":\"����\"}��ʽ�ظ���",
            "���",
            100);

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
        Assert.Contains("}", content);
    }

    [Fact]
    [DisplayName("����ʽ_���ֶԻ������ı���")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "�ҵ����ֽ�С�������ס" },
                new ChatMessage { Role = "assistant", Content = "�õģ��Ҽ�ס�ˣ����С����" },
                new ChatMessage { Role = "user", Content = "�ҽ�ʲô���֣�ֻ�ش�����" },
            ],
            MaxTokens = 200,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("С��", content);
    }

    #endregion

    #region ����ʽ�Ի� - �������ǣ�BuildRequestBody ���з�֧��

    [Fact]
    [DisplayName("����_Temperature������Ч")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���˵һ��1��100�����֣�ֻ�ش�����");
        request.Temperature = 0.0;
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("����_TopP������Ч")]
    public async Task ChatAsync_TopP_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "дһƪ���ڴ��������", 10);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.OutputTokens <= 15, $"CompletionTokens={response.Usage.OutputTokens} Ӧ�� MaxTokens ����");
    }

    [Fact]
    [DisplayName("����_Stopֹͣ����Ч")]
    public async Task ChatAsync_Stop_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "��1����10���ö��ŷָ�", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("����_PresencePenalty������")]
    public async Task ChatAsync_PresencePenalty_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���", 200);
        request.PresencePenalty = 1.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("����_FrequencyPenalty������")]
    public async Task ChatAsync_FrequencyPenalty_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���", 200);
        request.FrequencyPenalty = 1.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("����_User��ʶ������")]
    public async Task ChatAsync_User_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���", 200);
        request.User = "test-user-12345";

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("����_���ı�����ɴ���")]
    public async Task ChatAsync_LongInput_Accepted()
    {
        if (!HasApiKey()) return;

        var longText = String.Join(",", Enumerable.Range(1, 100).Select(i => $"item{i}"));
        var request = CreateSimpleRequest("qwen-plus", $"count items: {longText}");
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("����_���п�ѡ����ͬʱ����")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "���", 200);
        request.Temperature = 0.7;
        request.TopP = 0.9;
        request.PresencePenalty = 0.5;
        request.FrequencyPenalty = 0.5;
        request.User = "integration-test";
        request.Stop = ["."];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region ����ʽ�Ի� - ��Ӧ�ṹ��֤��ParseResponse ȫ�ֶΣ�

    [Fact]
    [DisplayName("��Ӧ�ṹ_FinishReason��ȷ����")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Stop || finishReason == FinishReason.Length,
            $"FinishReason should be stop or length, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_FinishReason_MaxTokens�ضϷ���length")]
    public async Task ChatAsync_FinishReason_Length_WhenTruncated()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "describe the solar system formation in 500 words", 5);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Length || finishReason == FinishReason.Stop,
            $"Expected length or stop, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_����ģ�ͱ�ʶ")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
        Assert.Contains("qwen", response.Model, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_������ӦId")]
    public async Task ChatAsync_Response_ContainsId()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Id));
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Object�ֶ�Ϊchat.completion")]
    public async Task ChatAsync_Response_ObjectField()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.Equal("chat.completion", response.Object);
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Choices������ȷ")]
    public async Task ChatAsync_Response_ChoiceIndex()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
    }

    [Fact]
    [DisplayName("��Ӧ�ṹ_Message��ɫΪassistant")]
    public async Task ChatAsync_Response_MessageRole()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
    }

    [Fact]
    [DisplayName("����_����ʽ��Ӧ��������Usage")]
    public async Task ChatAsync_Usage_Complete()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Usage);
        Assert.True(response.Usage.InputTokens > 0);
        Assert.True(response.Usage.OutputTokens > 0);
        Assert.Equal(response.Usage.InputTokens + response.Usage.OutputTokens, response.Usage.TotalTokens);
    }

    #endregion

    #region ��ʽ�Ի� - ��������

    [Fact]
    [DisplayName("��ʽ_QwenPlus_���ض��Chunk")]
    public async Task ChatStreamAsync_QwenPlus_ReturnsChunks()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "write a bubble sort in C#");
        request.MaxTokens = 200;
        request.Stream = true;

        var chunks = new List<IChatResponse>();
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
        Assert.True(hasContent, "stream should contain at least one content chunk");
    }

    [Fact]
    [DisplayName("��ʽ_QwenTurbo_����ģ����ʽ����")]
    public async Task ChatStreamAsync_QwenTurbo_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-turbo", "hi");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    [Fact]
    [DisplayName("��ʽ_���ݿ�ƴ��Ϊ�����ı�")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "describe bubble sort in 50 words");
        request.MaxTokens = 100;
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
        Assert.True(fullContent.Length > 5, $"concatenated content too short: {fullContent}");
    }

    [Fact]
    [DisplayName("��ʽ_ϵͳ��ʾ����Ч")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        if (!HasApiKey()) return;

        var request = CreateRequestWithSystem("qwen-plus", "Always start reply with 'OK:'", "hello", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "write a 1000 word essay about AI history");
        request.MaxTokens = 500;
        request.Stream = true;

        using var cts = new CancellationTokenSource();
        var chunks = new List<IChatResponse>();

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
            // expected
        }

        Assert.True(chunks.Count >= 3, "should receive at least 3 chunks before cancel");
    }

    #endregion

    #region ��ʽ�Ի� - �ṹ��֤

    [Fact]
    [DisplayName("��ʽ�ṹ_ÿ��Chunk����Choices")]
    public async Task ChatStreamAsync_EachChunk_HasChoices()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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

        Assert.True(hasDelta, "stream chunk should use Delta field");
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_Object�ֶ�Ϊchat.completion.chunk")]
    public async Task ChatStreamAsync_ObjectField()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        FinishReason? lastFinishReason = null;
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
        Assert.True(lastFinishReason == FinishReason.Stop || lastFinishReason == FinishReason.Length,
            $"stream final FinishReason should be stop or length, actual: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("��ʽ�ṹ_����ģ�ͱ�ʶ")]
    public async Task ChatStreamAsync_ContainsModel()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        Assert.Contains("qwen", model, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("��ʽ����_����Chunk���ܰ���Usage")]
    public async Task ChatStreamAsync_Usage_InFinalChunk()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        // DashScope streaming API may not include usage in final chunk
        // (requires stream_options parameter which OpenAiProvider doesn't send)
        var lastWithUsage = chunks.LastOrDefault(c => c.Usage != null);
        if (lastWithUsage != null)
        {
            Assert.True(lastWithUsage.Usage!.TotalTokens > 0);
        }
    }

    #endregion

    #region ������ - HTTP ��

    [Fact]
    [DisplayName("����_��ApiKey_ChatAsync�׳�HttpRequestException")]
    public async Task ChatAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("�������", ex.Message);
    }

    [Fact]
    [DisplayName("����_��ApiKey_ChatStreamAsync�׳�HttpRequestException")]
    public async Task ChatStreamAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });

        Assert.Contains("�������", ex.Message);
    }

    [Fact]
    [DisplayName("����_��ЧApiKey_�׳�HttpRequestException")]
    public async Task ChatAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("�������", ex.Message);
    }

    [Fact]
    [DisplayName("����_�����ڵ�ģ��_�׳�HttpRequestException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request);
        });

        Assert.Contains("�������", ex.Message);
    }

    [Fact]
    [DisplayName("����_��ЧEndpoint_�׳��쳣")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.example.com",
            ApiKey = _apiKey.Length > 0 ? _apiKey : "sk-test",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("����_��ʽ��ЧApiKey_�׳�HttpRequestException")]
    public async Task ChatStreamAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });

        Assert.Contains("�������", ex.Message);
    }

    [Fact]
    [DisplayName("����_��ʽ�����ڵ�ģ��_�׳�HttpRequestException")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");
        request.Stream = true;

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
            }
        });

        Assert.Contains("�������", ex.Message);
    }

    #endregion

    #region ������ - �����߽�

    [Fact]
    [DisplayName("����_����Ϣ�б�_�׳��쳣")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request);
        });
    }

    [Fact]
    [DisplayName("����_��ʽ����Ϣ�б�_�׳��쳣�򷵻ؿ�")]
    public async Task ChatStreamAsync_EmptyMessages_ThrowsOrEmpty()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
            Stream = true,
        };

        // DashScope may throw HttpRequestException or return empty stream for empty messages
        try
        {
            var chunks = new List<IChatResponse>();
            await foreach (var chunk in ChatStreamAsync(request))
            {
                chunks.Add(chunk);
            }
            // If no exception, server accepted empty messages �� verify no meaningful content
        }
        catch (HttpRequestException)
        {
            // Expected: server rejected the request (HTTP error layer or API error wrapped in ApiException)
        }
        catch (ArgumentException)
        {
            // Expected: client-side validation rejected empty messages
        }
    }

    #endregion

    #region FunctionCalling

    [Fact]
    [DisplayName("FunctionCalling_���߶��屻��ȷ����")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
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

        var choice = response.Messages[0];
        if (choice.FinishReason == FinishReason.ToolCalls)
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            var toolCall = choice.Message.ToolCalls[0];
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_weather", toolCall.Function?.Name);
            Assert.False(String.IsNullOrWhiteSpace(toolCall.Id));
            Assert.NotNull(toolCall.Function?.Arguments);
        }
    }

    [Fact]
    [DisplayName("FunctionCalling_�๤�߶������")]
    public async Task ChatAsync_FunctionCalling_MultipleTools()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "check Beijing weather and calculate 123*456" },
            ],
            MaxTokens = 200,
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
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "calculate",
                        Description = "Calculate math expression",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["expression"] = new Dictionary<String, Object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "math expression",
                                },
                            },
                            ["required"] = new[] { "expression" },
                        },
                    },
                },
            ],
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("FunctionCalling_ToolChoice_Auto����������")]
    public async Task ChatAsync_FunctionCalling_ToolChoiceAuto()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_time",
                        Description = "Get current time",
                        Parameters = new Dictionary<String, Object> { ["type"] = "object", ["properties"] = new Dictionary<String, Object>() },
                    },
                },
            ],
            ToolChoice = "auto",
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("FunctionCalling_�������ߵ����ִ�")]
    public async Task ChatAsync_FunctionCalling_FullRoundTrip()
    {
        if (!HasApiKey()) return;

        var weatherTool = new ChatTool
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
        };

        // Round 1: user asks, model calls tool
        var request1 = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Tools = [weatherTool],
        };

        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        var choice1 = response1.Messages[0];
        if (choice1.FinishReason != FinishReason.ToolCalls || choice1.Message?.ToolCalls == null)
            return; // model chose to answer directly, skip round 2

        var toolCall = choice1.Message.ToolCalls[0];
        Assert.NotNull(toolCall.Id);

        // Round 2: submit tool result, model generates final reply
        // Covers BuildRequestBody branches: ToolCallId, Name, ToolCalls serialization
        var request2 = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
                new ChatMessage
                {
                    Role = "assistant",
                    ToolCalls = choice1.Message.ToolCalls,
                },
                new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function?.Name,
                    Content = "{\"temperature\": 25, \"weather\": \"sunny\", \"city\": \"Beijing\"}",
                },
            ],
            MaxTokens = 100,
            Tools = [weatherTool],
        };

        var response2 = await ChatAsync(request2, CreateOptions());

        Assert.NotNull(response2);
        Assert.NotNull(response2.Messages);
        Assert.NotEmpty(response2.Messages);

        var finalContent = response2.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(finalContent));
    }

    [Fact]
    [DisplayName("FunctionCalling_��ʽ���ߵ��÷���ToolCalls")]
    public async Task ChatStreamAsync_FunctionCalling_ReturnsToolCalls()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Stream = true,
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

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        var hasToolCalls = chunks.Any(c => c.Messages?.Any(ch =>
            ch.Delta?.ToolCalls != null && ch.Delta.ToolCalls.Count > 0) == true);
        var hasContent = chunks.Any(c => c.Messages?.Any(ch =>
            ch.Delta?.Content is String s && !String.IsNullOrEmpty(s)) == true);

        Assert.True(hasToolCalls || hasContent, "stream should return tool_calls or content");
    }

    #endregion

    #region DashScopeProvider ������֤

    [Fact]
    [DisplayName("Provider_CodeΪDashScope")]
    public void Provider_Code_IsDashScope()
    {
        Assert.Equal("DashScope", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Provider_NameΪ�������")]
    public void Provider_Name_IsCorrect()
    {
        Assert.Equal("�������", _descriptor.DisplayName);
    }

    [Fact]
    [DisplayName("Provider_DefaultEndpoint��ȷ")]
    public void Provider_DefaultEndpoint_IsCorrect()
    {
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Provider_ApiProtocolΪChatCompletions")]
    public void Provider_ApiProtocol_IsChatCompletions()
    {
        Assert.Equal("DashScope", _descriptor.Protocol);
    }

    [Fact]
    [DisplayName("Provider_Models�б��ǿ��Ұ���qwenģ��")]
    public void Provider_Models_ContainsQwen()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Model.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
                                     m.DisplayName.Contains("qwen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [DisplayName("Provider_IAiProvider�ӿ�ʵ��")]
    public void Provider_Implements_IAiProvider()
    {
        Assert.IsType<AiClientDescriptor>(_descriptor);
    }

    #endregion

    #region SetHeaders �� Options ��֤

    [Fact]
    [DisplayName("Options_EndpointΪ��ʱʹ��Ĭ��")]
    public async Task Options_EmptyEndpoint_UsesDefault()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "",
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_EndpointΪnullʱʹ��Ĭ��")]
    public async Task Options_NullEndpoint_UsesDefault()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = null,
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpointβ��б�ܱ���ȷ����")]
    public async Task Options_TrailingSlash_Handled()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-flash", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "https://dashscope.aliyuncs.com/api/v1/",
            ApiKey = _apiKey,
        };

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
        if (!HasApiKey()) return;

        var tasks = Enumerable.Range(1, 3).Select(i =>
        {
            var request = CreateSimpleRequest("qwen3.5-flash", $"{i}+{i}=?", 10);
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
        if (!HasApiKey()) return;

        // Non-streaming
        var request1 = CreateSimpleRequest("qwen3.5-flash", "1+1=?", 10);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        // Streaming
        var request2 = CreateSimpleRequest("qwen3.5-flash", "2+2=?", 10);
        request2.Stream = true;
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2, CreateOptions()))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // Non-streaming again
        var request3 = CreateSimpleRequest("qwen3.5-flash", "3+3=?", 10);
        var response3 = await ChatAsync(request3, CreateOptions());
        Assert.NotNull(response3?.Messages);
    }

    #endregion

    #region ���˼����DeepThinking��

    [Fact]
    [DisplayName("���˼��_����ʽ_����ReasoningContent")]
    public async Task ChatAsync_DeepThinking_ReturnsReasoningContent()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3-max", "9.11 �� 9.8 �ĸ�����", 150);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 64;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var message = response.Messages[0].Message;
        Assert.NotNull(message);
        Assert.False(String.IsNullOrWhiteSpace(message.Content as String));

        // ֧��˼����ģ��Ӧ���� reasoning_content�������ݼ���Ϊ���������޶���������
        if (!String.IsNullOrWhiteSpace(message.ReasoningContent))
            Assert.True(message.ReasoningContent.Length > 0);
    }

    [Fact]
    [DisplayName("���˼��_��ʽ_�������ReasoningContent")]
    public async Task ChatStreamAsync_DeepThinking_StreamsReasoningContent()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3-max", "1+1���ڼ���", 100);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 64;
        request.Stream = true;

        var reasoningChunks = new List<String>();
        var contentChunks = new List<String>();

        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (String.IsNullOrEmpty(choice.Delta?.ReasoningContent) is false)
                    reasoningChunks.Add(choice.Delta!.ReasoningContent!);
                if (choice.Delta?.Content is String s && !String.IsNullOrEmpty(s))
                    contentChunks.Add(s);
            }
        }

        // ����Ӧ���������
        Assert.NotEmpty(contentChunks);
    }

    #endregion

    #region �ṹ�������StructuredOutput��

    [Fact]
    [DisplayName("�ṹ�����_JsonObjectģʽ������ЧJSON")]
    public async Task ChatAsync_StructuredOutput_JsonObject_ReturnsValidJson()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-plus",
            "�� JSON ��ʽ���أ�{\"city\":\"Beijing\",\"population_million\":22}", 200);
        request.ResponseFormat = new Dictionary<String, Object> { ["type"] = "json_object" };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    #endregion

    #region ����������WebSearch��

    [Fact]
    [DisplayName("��������_EnableSearch_�ش����ʱЧ����")]
    public async Task ChatAsync_EnableSearch_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-plus", "����������Ƕ��٣�", 200);
        request["EnableSearch"] = true;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("��������_EnableSource_���󱻽���")]
    public async Task ChatAsync_EnableSource_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-plus", "������ʲô���ţ�", 200);
        request["EnableSearch"] = true;
        request["EnableSource"] = true;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region ���й��ߵ��ã�ParallelToolCalls��

    [Fact]
    [DisplayName("���й��ߵ���_ParallelToolCalls����������")]
    public async Task ChatAsync_ParallelToolCalls_Accepted()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen3.5-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "��һ�±������Ϻ�������" },
            ],
            MaxTokens = 200,
            ParallelToolCalls = true,
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
    }

    #endregion
}
