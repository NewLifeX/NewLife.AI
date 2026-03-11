using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>Google Gemini 服务商。支持 Gemini 系列模型的原生 API 协议</summary>
/// <remarks>
/// Gemini API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 URL 参数 key 传递</item>
/// <item>请求路径包含模型名称</item>
/// <item>消息结构使用 contents 数组，角色为 user/model</item>
/// <item>流式接口路径为 streamGenerateContent</item>
/// <item>响应中的 content 使用 parts 数组</item>
/// </list>
/// </remarks>
public class GeminiProvider : IAiProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public virtual String Code => "Gemini";

    /// <summary>服务商名称</summary>
    public virtual String Name => "谷歌Gemini";

    /// <summary>服务商描述</summary>
    public virtual String? Description => "谷歌 Gemini 系列多模态大模型，支持超长上下文";

    /// <summary>API 协议类型</summary>
    public virtual String ApiProtocol => "Gemini";

    /// <summary>默认 API 地址</summary>
    public virtual String DefaultEndpoint => "https://generativelanguage.googleapis.com";

    /// <summary>主流模型列表。Google Gemini 各主力模型及其能力</summary>
    public virtual AiModelInfo[] Models { get; } =
    [
        new("gemini-2.5-pro",                  "Gemini 2.5 Pro",    new(true,  true, false, true)),
        new("gemini-2.5-flash",                "Gemini 2.5 Flash",  new(true,  true, false, true)),
        new("gemini-2.0-flash",                "Gemini 2.0 Flash",  new(false, true, false, true)),
        new("gemini-1.5-pro",                  "Gemini 1.5 Pro",    new(false, true, false, true)),
        new("imagen-3.0-generate-001",         "Imagen 3",          new(false, false, true, false)),
    ];

    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
    })
    {
        Timeout = TimeSpan.FromMinutes(5),
    };
    #endregion

    #region 方法
    /// <summary>非流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var body = BuildGeminiRequest(request);

        var model = request.Model ?? "gemini-pro";
        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/models/{model}:generateContent?key={options.ApiKey}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商 {Name} 返回错误 {(Int32)httpResponse.StatusCode}: {responseText}");

        return ParseGeminiResponse(responseText, model);
    }

    /// <summary>流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = BuildGeminiRequest(request);

        var model = request.Model ?? "gemini-pro";
        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/models/{model}:streamGenerateContent?alt=sse&key={options.ApiKey}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"AI 服务商 {Name} 返回错误 {(Int32)httpResponse.StatusCode}: {errorText}");
        }

        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseGeminiStreamChunk(data, model);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建 Gemini 请求体</summary>
    /// <param name="request">请求对象</param>
    /// <returns></returns>
    private String BuildGeminiRequest(ChatCompletionRequest request)
    {
        var dic = new Dictionary<String, Object>();

        // 转换消息为 Gemini contents 格式
        var contents = new List<Object>();
        String? systemInstruction = null;

        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                systemInstruction = msg.Content?.ToString();
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
            var parts = new List<Object>();

            if (msg.Content is String textContent)
                parts.Add(new Dictionary<String, Object> { ["text"] = textContent });
            else if (msg.Content != null)
                parts.Add(new Dictionary<String, Object> { ["text"] = msg.Content.ToString() ?? "" });

            contents.Add(new Dictionary<String, Object> { ["role"] = role, ["parts"] = parts });
        }
        dic["contents"] = contents;

        // system instruction
        if (!String.IsNullOrEmpty(systemInstruction))
        {
            dic["systemInstruction"] = new Dictionary<String, Object>
            {
                ["parts"] = new List<Object> { new Dictionary<String, Object> { ["text"] = systemInstruction } }
            };
        }

        // 生成配置
        var genConfig = new Dictionary<String, Object>();
        if (request.Temperature != null) genConfig["temperature"] = request.Temperature.Value;
        if (request.TopP != null) genConfig["topP"] = request.TopP.Value;
        if (request.MaxTokens != null) genConfig["maxOutputTokens"] = request.MaxTokens.Value;
        if (request.Stop != null && request.Stop.Count > 0) genConfig["stopSequences"] = request.Stop;
        if (genConfig.Count > 0)
            dic["generationConfig"] = genConfig;

        // 工具列表
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var functionDeclarations = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                functionDeclarations.Add(fn);
            }
            dic["tools"] = new List<Object>
            {
                new Dictionary<String, Object> { ["functionDeclarations"] = functionDeclarations }
            };
        }

        return dic.ToJson();
    }

    /// <summary>解析 Gemini 非流式响应</summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="model">模型名称</param>
    /// <returns></returns>
    private ChatCompletionResponse ParseGeminiResponse(String json, String model)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Gemini 响应");

        var response = new ChatCompletionResponse
        {
            Model = model,
            Object = "chat.completion",
        };

        if (dic.TryGetValue("candidates", out var candidatesObj) && candidatesObj is IList<Object> candidates)
        {
            var choices = new List<ChatChoice>();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] is not IDictionary<String, Object> candidate) continue;

                var contentText = ExtractGeminiContent(candidate);
                var finishReason = candidate.TryGetValue("finishReason", out var fr) ? MapGeminiFinishReason(fr as String) : null;

                choices.Add(new ChatChoice
                {
                    Index = i,
                    Message = new ChatMessage { Role = "assistant", Content = contentText },
                    FinishReason = finishReason,
                });
            }
            response.Choices = choices;
        }

        // 用量统计
        if (dic.TryGetValue("usageMetadata", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new ChatUsage
            {
                PromptTokens = usageDic.TryGetValue("promptTokenCount", out var pt) ? pt.ToInt() : 0,
                CompletionTokens = usageDic.TryGetValue("candidatesTokenCount", out var ct) ? ct.ToInt() : 0,
                TotalTokens = usageDic.TryGetValue("totalTokenCount", out var tt) ? tt.ToInt() : 0,
            };
        }

        return response;
    }

    /// <summary>解析 Gemini 流式数据块</summary>
    /// <param name="data">JSON 数据</param>
    /// <param name="model">模型名称</param>
    /// <returns></returns>
    private ChatCompletionResponse? ParseGeminiStreamChunk(String data, String model)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatCompletionResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
        };

        if (dic.TryGetValue("candidates", out var candidatesObj) && candidatesObj is IList<Object> candidates && candidates.Count > 0)
        {
            if (candidates[0] is IDictionary<String, Object> candidate)
            {
                var contentText = ExtractGeminiContent(candidate);
                var finishReason = candidate.TryGetValue("finishReason", out var fr) ? MapGeminiFinishReason(fr as String) : null;

                response.Choices = [new ChatChoice
                {
                    Index = 0,
                    Delta = new ChatMessage { Content = contentText },
                    FinishReason = finishReason,
                }];
            }
        }

        if (dic.TryGetValue("usageMetadata", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new ChatUsage
            {
                PromptTokens = usageDic.TryGetValue("promptTokenCount", out var pt) ? pt.ToInt() : 0,
                CompletionTokens = usageDic.TryGetValue("candidatesTokenCount", out var ct) ? ct.ToInt() : 0,
                TotalTokens = usageDic.TryGetValue("totalTokenCount", out var tt) ? tt.ToInt() : 0,
            };
        }

        return response;
    }

    /// <summary>提取 Gemini candidate 中的文本内容</summary>
    /// <param name="candidate">候选项字典</param>
    /// <returns></returns>
    private static String ExtractGeminiContent(IDictionary<String, Object> candidate)
    {
        if (!candidate.TryGetValue("content", out var contentObj) || contentObj is not IDictionary<String, Object> contentDic)
            return "";

        if (!contentDic.TryGetValue("parts", out var partsObj) || partsObj is not IList<Object> parts)
            return "";

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is IDictionary<String, Object> partDic && partDic.TryGetValue("text", out var text))
                sb.Append(text);
        }
        return sb.ToString();
    }

    /// <summary>映射 Gemini 的 finishReason 到 OpenAI 格式</summary>
    /// <param name="finishReason">Gemini 结束原因</param>
    /// <returns></returns>
    private static String? MapGeminiFinishReason(String? finishReason) => finishReason switch
    {
        "STOP" => "stop",
        "MAX_TOKENS" => "length",
        "SAFETY" => "content_filter",
        "RECITATION" => "content_filter",
        _ => finishReason?.ToLower(),
    };
    #endregion
}
