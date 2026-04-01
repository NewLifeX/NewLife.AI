#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Clients;

public class NewLifeAiIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("NewLifeAI")!;
    private readonly String _apiKey;

    public NewLifeAiIntegrationTests()
    {
        _apiKey = LoadApiKey() ?? "";
    }

    private static String? LoadApiKey()
    {
        var configPath = "config/NewLifeAI.key".GetFullPath();
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

        return Environment.GetEnvironmentVariable("NEWLIFEAI_API_KEY");
    }

    /// <summary>AppKey �Ƿ����</summary>
    private Boolean HasApiKey() => !String.IsNullOrWhiteSpace(_apiKey);

    /// <summary>����Ĭ������ѡ��</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
        ApiKey = _apiKey,
    };

    /// <summary>�����򵥵��û���Ϣ����</summary>
    private static ChatRequest CreateSimpleRequest(String prompt, Int32 maxTokens = 200) => new()
    {
        Model = "qwen3.5",
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
    };

    /// <summary>������ϵͳ��ʾ������</summary>
    private static ChatRequest CreateRequestWithSystem(String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = "qwen3.5",
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

    /// <summary>���� NewLifeAI ר�ÿͻ��ˣ��� ResponsesAsync/MessagesAsync ����չ�˵㣩</summary>
    private NewLifeAIChatClient CreateNewLifeAiClient() => (NewLifeAIChatClient)_descriptor.Factory(CreateOptions());

    #region Ԫ������֤������ AppKey��

    [Fact]
    [DisplayName("Ԫ����_Code��Ϊ��")]
    public void Provider_Code_IsNewLifeAI()
    {
        Assert.Equal("NewLifeAI", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Ԫ����_Name��Ϊ��")]
    public void Provider_Name_NotEmpty()
    {
        Assert.False(String.IsNullOrWhiteSpace(_descriptor.DisplayName));
    }

    [Fact]
    [DisplayName("Ԫ����_DefaultEndpointָ��������AI����")]
    public void Provider_DefaultEndpoint_PointsToNewLifeAI()
    {
        Assert.StartsWith("https://ai.newlifex.com", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Ԫ����_Models����qwen3.5")]
    public void Provider_Models_ContainsQwen35()
    {
        Assert.NotNull(_descriptor.Models);
        Assert.NotEmpty(_descriptor.Models);
        Assert.Contains(_descriptor.Models, m => m.Model == "qwen3.5");
    }

    [Fact]
    [DisplayName("Ԫ����_Description��Ϊ��")]
    public void Provider_Description_NotEmpty()
    {
        Assert.False(String.IsNullOrWhiteSpace(_descriptor.Description));
    }

    #endregion

    #region ����ʽ�Ի� - Chat Completions��/v1/chat/completions��

    [Fact]
    [DisplayName("����ʽ_Qwen3.5_������Ч��Ӧ")]
    public async Task ChatAsync_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("��һ�仰�����Լ�");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI �ظ����ݲ�ӦΪ��");

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0, "Token ����Ӧ���� 0");
    }

    [Fact]
    [DisplayName("����ʽ_ϵͳ��ʾ����Ч")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        if (!HasApiKey()) return;

        var request = CreateRequestWithSystem(
            "����һ��ֻ����JSON��ʽ�ظ��Ļ����ˣ��ظ���ʽΪ��{\"reply\":\"����\"}",
            "���",
            100);

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
    }

    [Fact]
    [DisplayName("����ʽ_���ֶԻ������ı���")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen3.5",
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

    [Fact]
    [DisplayName("����ʽ_FinishReason��ȷ����")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Stop || finishReason == FinishReason.Length,
            $"FinishReason ӦΪ stop �� length��ʵ��Ϊ: {finishReason}");
    }

    [Fact]
    [DisplayName("����ʽ_��Ӧ����ģ�ͱ�ʶ")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("hi", 100);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
    }

    [Fact]
    [DisplayName("����ʽ_Temperature������Ч")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("��һ������������", 100);
        request.Temperature = 0.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("����ʽ_MaxTokens������Ч")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("дһƪ���ڴ���ĳ���", 10);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.OutputTokens <= 15,
            $"CompletionTokens={response.Usage.OutputTokens} Ӧ�� MaxTokens ����");
    }

    #endregion

    #region ��ʽ�Ի� - Chat Completions��/v1/chat/completions��

    [Fact]
    [DisplayName("��ʽ_���ض��Chunk")]
    public async Task ChatStreamAsync_ReturnsChunks()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("�򵥽���һ��C#����");
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
        Assert.True(hasContent, "��ʽӦ��������һ�������ݵ� chunk");
    }

    [Fact]
    [DisplayName("��ʽ_���ݿ�ƴ��Ϊ������Ӧ")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("1+1���ڼ���ֻ�ش�����");
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            var text = chunk.Messages?[0].Delta?.Content as String;
            if (!String.IsNullOrEmpty(text)) fullContent += text;
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent), "ƴ�Ӻ����ݲ�ӦΪ��");
        Assert.Contains("2", fullContent);
    }

    [Fact]
    [DisplayName("��ʽ_ȡ�����ƿ���ֹ��")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        if (!HasApiKey()) return;

        using var cts = new CancellationTokenSource();
        var request = CreateSimpleRequest("������1��100��ÿ�����ֵ���һ��");
        request.MaxTokens = 500;
        request.Stream = true;

        var count = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in ChatStreamAsync(request, null, cts.Token))
            {
                count++;
                if (count >= 3) cts.Cancel();
            }
        });

        Assert.True(count >= 3, "ȡ��ǰӦ���յ����� 3 �� chunk");
    }

    #endregion

    #region OpenAI Responses API��/v1/responses��

    [Fact]
    [DisplayName("ResponsesAPI_����ʽ_������Ч��Ӧ")]
    public async Task ResponsesAsync_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("��һ�仰�����Լ�");
        var response = await CreateNewLifeAiClient().ResponsesAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/responses �ظ����ݲ�ӦΪ��");
    }

    [Fact]
    [DisplayName("ResponsesAPI_��ʽ_���ض��Chunk")]
    public async Task ResponsesStreamAsync_ReturnsChunks()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("����һ��Python");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().ResponsesStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region Anthropic Messages API��/v1/messages��

    [Fact]
    [DisplayName("MessagesAPI_����ʽ_������Ч��Ӧ")]
    public async Task MessagesAsync_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("��ã���򵥻ظ�");
        var response = await CreateNewLifeAiClient().MessagesAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/messages �ظ����ݲ�ӦΪ��");
    }

    [Fact]
    [DisplayName("MessagesAPI_��ʽ_���ض��Chunk")]
    public async Task MessagesStreamAsync_ReturnsChunks()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("���ʺ�");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().MessagesStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region Google Gemini API��/v1/gemini��

    [Fact]
    [DisplayName("GeminiAPI_����ʽ_������Ч��Ӧ")]
    public async Task GeminiAsync_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("����ʺ���");
        var response = await CreateNewLifeAiClient().GeminiAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/gemini �ظ����ݲ�ӦΪ��");
    }

    [Fact]
    [DisplayName("GeminiAPI_��ʽ_���ض��Chunk")]
    public async Task GeminiStreamAsync_ReturnsChunks()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("����һ���Լ�");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().GeminiStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region ͼ�����ɣ�/v1/images/generations��

    [Fact]
    [DisplayName("ͼ������_��Ч��ʾ��_������Ӧ")]
    public async Task ImageGenerationsAsync_ReturnsResponse()
    {
        if (!HasApiKey()) return;

        ImageGenerationResponse? response = null;
        try
        {
            response = await CreateNewLifeAiClient().ImageGenerationsAsync(
                "A cute robot reading a book",
                "qwen3.5",
                "1024x1024");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            // ��ǰģ�Ͳ�֧��ͼ������ʱ����������ʧ��
            if (ex.Message.Contains("400") || ex.Message.Contains("404") || ex.Message.Contains("405")
                || ex.Message.Contains("��֧��") || ex.Message.Contains("unsupported"))
                return;
            throw;
        }

        Assert.NotNull(response);
    }

    #endregion

    #region ����ע����֤

    [Fact]
    [DisplayName("����_NewLifeAI��ע��")]
    public void Factory_NewLifeAiProvider_IsRegistered()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("NewLifeAI");
        Assert.NotNull(descriptor);
        Assert.Equal("NewLifeAI", descriptor!.Code);
    }

    #endregion
}
