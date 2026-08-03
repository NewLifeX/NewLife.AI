using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAI 协议基础客户端。提供 OpenAI Chat Completions 协议通用的聊天与模型列表能力</summary>
/// <remarks>
/// 负责 OpenAI 兼容协议的核心通信逻辑：SSE 流式解析、请求构建、响应解析、Bearer 认证和模型列表查询。
/// 仅包含聊天与模型列表能力；多模态能力（图像/视频/语音/嵌入）由子类 <see cref="OpenAIChatClient"/> 按需扩展。
/// 大部分 OpenAI 兼容服务商（AzureAI、DeepSeek 等）可直接继承此类而不依赖多模态方法。
/// </remarks>
public class OpenAIClientBase : AiClientBase, IModelListClient
{
    #region 属性
    /// <summary>对话完成路径。默认 /v1/chat/completions，部分服务商需要调整</summary>
    public override String ChatPath { get; set; } = "/v1/chat/completions";

    /// <summary>默认 Json 序列化选项（蛇形命名 + 忽略 null）</summary>
    public static JsonOptions DefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.SnakeCaseLower,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public OpenAIClientBase(AiClientOptions options) : base(options) => JsonOptions = DefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建 OpenAI 兼容客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public OpenAIClientBase(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region IChatClient
    /// <summary>流式对话（OpenAI SSE 格式）</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(request);
        var body = BuildRequest(request);

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            // 兼容 data: 与 data: （部分服务商省略空格）
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

            var data = line.Substring(5).Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            // 流式错误：部分服务商在 data 中返回 {"error":{...}} 而非正常 chunk，识别后抛出而非静默吞掉
            EnsureNoStreamError(data, Name);

            IChatResponse? chunk = null;
            try { chunk = ParseChunk(data, request, null); } catch { }
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 模型列表
    /// <summary>获取该服务商当前可用的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public virtual async Task<ModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = BuildApiUrl("/v1/models");

        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new ModelListResponse
        {
            Object = dic["object"] as String,
        };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<ModelInfo>();
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new ModelInfo
                {
                    Id = d["id"] as String,
                    Name = d["name"] as String,
                    Object = d["object"] as String,
                    Created = d["created"].ToLong().ToDateTime(),
                    OwnedBy = d["owned_by"] as String,
                    ContextLength = d.TryGetValue("context_length", out var cl) ? cl.ToInt() : 0,
                    SupportThinking = d.TryGetValue("support_thinking", out var st) && st.ToBoolean(),
                    SupportFunction = d.TryGetValue("support_function", out var sfc) && sfc.ToBoolean(),
                    SupportVision = d.TryGetValue("support_vision", out var sv) && sv.ToBoolean(),
                    SupportAudio = d.TryGetValue("support_audio", out var sa) && sa.ToBoolean(),
                    SupportSpeech = d.TryGetValue("support_speech", out var sp) && sp.ToBoolean(),
                    SupportImage = d.TryGetValue("support_image", out var sig) && sig.ToBoolean(),
                    SupportVideo = d.TryGetValue("support_video", out var svg) && svg.ToBoolean(),
                    SupportEmbedding = d.TryGetValue("support_embedding", out var se) && se.ToBoolean(),
                    SupportRerank = d.TryGetValue("support_rerank", out var sr) && sr.ToBoolean(),
                    PricingMode = d.TryGetValue("pricing_mode", out var pm) ? pm as String : null,
                    InputPrice = d.TryGetValue("input_price", out var inp) ? inp.ToDecimal() : 0,
                    OutputPrice = d.TryGetValue("output_price", out var outp) ? outp.ToDecimal() : 0,
                    CachedInputPrice = d.TryGetValue("cached_input_price", out var cip) ? cip.ToDecimal() : 0,
                    CacheCreationInputPrice = d.TryGetValue("cache_creation_input_price", out var ccp) ? ccp.ToDecimal() : 0,
                    UnitPrice = d.TryGetValue("unit_price", out var up) ? up.ToDecimal() : 0,
                    Unit = d.TryGetValue("unit", out var u) ? u as String : null,
                });
            }
            response.Data = [.. items];
        }

        return response;
    }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径</summary>
    /// <param name="request">对话请求</param>
    /// <returns>完整请求 URL</returns>
    protected override String BuildUrl(IChatRequest request) => BuildApiUrl(ChatPath);

    /// <summary>构建请求体。返回符合 OpenAI 格式的协议请求对象</summary>
    /// <param name="request">请求对象</param>
    /// <returns>ChatCompletionRequest 实例</returns>
    protected override Object BuildRequest(IChatRequest request) => request is ChatCompletionRequest cr ? cr : ChatCompletionRequest.FromChatRequest(request);

    /// <summary>解析响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="request">请求对象</param>
    /// <returns>解析后的响应对象</returns>
    protected override IChatResponse ParseResponse(String json, IChatRequest request)
    {
        var resp = json.ToJsonEntity<ChatCompletionResponse>(JsonOptions) ?? new ChatCompletionResponse();
        resp.Model = request.Model;
        if (resp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion";
        return resp;
    }

    /// <summary>解析消息对象</summary>
    /// <param name="dic">字典</param>
    /// <returns>解析后的消息对象</returns>
    protected virtual ChatMessage? ParseChatMessage(IDictionary<String, Object>? dic)
    {
        if (dic == null) return null;

        var msg = new ChatMessage
        {
            Role = dic["role"] as String ?? "",
            Content = dic["content"],
            ReasoningContent = dic["reasoning_content"] as String ?? dic["reasoning"] as String,
        };

        if (dic["tool_calls"] is IList<Object> tcList)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tcItem in tcList)
            {
                if (tcItem is not IDictionary<String, Object> tcDic) continue;

                var tc = new ToolCall
                {
                    Index = tcDic["index"] is Object idxVal ? idxVal.ToInt() : null,
                    Id = tcDic["id"] as String ?? "",
                    Type = tcDic["type"] as String ?? "function",
                };

                if (tcDic["function"] is IDictionary<String, Object> fnDic)
                {
                    tc.Function = new FunctionCall
                    {
                        Name = fnDic["name"] as String ?? "",
                        Arguments = fnDic["arguments"] as String,
                    };
                }

                toolCalls.Add(tc);
            }
            msg.ToolCalls = toolCalls;
        }

        OnParseChatMessage(msg, dic);
        return msg;
    }

    /// <summary>消息解析扩展点。子类可重写此方法处理自定义响应字段</summary>
    /// <param name="msg">已完成基础解析的消息对象</param>
    /// <param name="dic">原始 JSON 字典，可读取额外字段</param>
    protected virtual void OnParseChatMessage(ChatMessage msg, IDictionary<String, Object> dic) { }

    /// <summary>设置请求头。Bearer Token + OpenAI-Organization</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="chatRequest">对话请求，可为 null</param>
    /// <param name="options">连接选项</param>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!String.IsNullOrEmpty(options.Organization))
            request.Headers.Add("OpenAI-Organization", options.Organization);
    }

    /// <summary>将 AIContent 集合转换为 OpenAI 格式的 content 字段</summary>
    /// <param name="contents">AIContent 列表</param>
    /// <returns>字符串（单一文本）或内容数组（多模态）</returns>
    protected static Object BuildContent(IList<AIContent> contents) => ChatCompletionRequest.BuildContent(contents);

    /// <summary>根据模型 ID 命名规律推断模型能力。子类可重写以实现服务商特定的推断逻辑</summary>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public virtual AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (modelId.IsNullOrEmpty()) return null;

        // 非对话模型：嵌入、语音合成、语音识别等
        if (modelId.Contains("embed", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportEmbedding: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.5m));
        if (modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportRerank: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 1m));
        if (modelId.StartsWith("tts", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportSpeech: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.2m));
        if (modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportAudio: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.2m));

        var thinking = false;
        var funcCall = true;
        var vision = false;
        var audio = false;
        var speech = false;
        var imageGen = false;
        var videoGen = false;
        var contextLength = 0;

        // 视觉能力：含 -vl / -vision / 含 vision
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("vision", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // 思考/推理能力
        if (modelId.Contains("-reasoner", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-thinking", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // OpenAI o 系列推理模型
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 高端系列（max/plus）通常支持思考
        if (modelId.Contains("-max", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-plus", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 文生图
        if (modelId.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("image-gen", StringComparison.OrdinalIgnoreCase))
        {
            imageGen = true;
            funcCall = false;
        }

        // 音频能力：gpt-4o-audio 系列（既能语音识别输入，也能语音合成输出）
        if (modelId.Contains("-audio", StringComparison.OrdinalIgnoreCase))
        {
            audio = true;
            speech = true;
        }

        // 文生视频：Sora 系列
        if (modelId.StartsWith("sora", StringComparison.OrdinalIgnoreCase))
        {
            videoGen = true;
            funcCall = false;
        }

        // === 上下文长度 ===
        // OpenAI o 系列推理模型：200K
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            contextLength = 200_000;
        // GPT-4o 系列：128K
        else if (modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            contextLength = 128_000;
        // GPT-4 Turbo：128K
        else if (modelId.StartsWith("gpt-4-turbo", StringComparison.OrdinalIgnoreCase))
            contextLength = 128_000;
        // GPT-4 经典：8K
        else if (modelId.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase))
            contextLength = 8_192;
        // GPT-3.5 Turbo：16K
        else if (modelId.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase))
            contextLength = 16_385;
        // Claude 系列：200K
        else if (modelId.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            contextLength = 200_000;
        // DeepSeek V4 系列：1M
        else if (modelId.StartsWith("deepseek-v4", StringComparison.OrdinalIgnoreCase))
            contextLength = 1_048_576;
        // DeepSeek 旧系列：64K
        else if (modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            contextLength = 65_536;

        // === 推理强度 ===
        String? reasoningEfforts = null;
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            reasoningEfforts = "low,medium,high";
        else if (thinking)
            // 其他支持思考的模型默认提供基础推理强度
            reasoningEfforts = "high";

        // === 价格推断（元/百万Token，汇率 6.9）===
        AiModelPricing? pricing = null;

        // OpenAI 系列
        if (modelId.StartsWith("gpt-4.1-mini", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(2.76m, 11.04m, 0.69m, 0);
        else if (modelId.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(13.8m, 55.2m, 3.45m, 0);
        else if (modelId.StartsWith("gpt-4o-mini", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(1.035m, 4.14m, 0.518m, 0);
        else if (modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(17.25m, 69m, 8.625m, 0);
        else if (modelId.StartsWith("gpt-4-turbo", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(72m, 216m, 36m, 0);
        else if (modelId.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(216m, 432m, 0, 0);
        else if (modelId.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(3.6m, 10.8m, 1.8m, 0);
        else if (modelId.StartsWith("gpt-5-mini", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(5.175m, 31.05m, 0.518m, 0);
        else if (modelId.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(17.25m, 103.5m, 4.313m, 0);
        // o 系列
        else if (modelId.StartsWith("o4-mini", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(7.59m, 30.36m, 1.898m, 0);
        else if (modelId.StartsWith("o3-mini", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(7.59m, 30.36m, 1.898m, 0);
        else if (modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(69m, 276m, 6.9m, 0);
        else if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(103.5m, 414m, 10.35m, 0);
        // Claude 系列（非 Bedrock）
        else if (modelId.StartsWith("claude-opus", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(34.5m, 172.5m, 3.45m, 0);
        else if (modelId.StartsWith("claude-sonnet", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(20.7m, 103.5m, 2.07m, 0);
        else if (modelId.StartsWith("claude-haiku", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(6.9m, 34.5m, 0.69m, 0);
        else if (modelId.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(20.7m, 103.5m, 2.07m, 0);
        // DeepSeek V4 系列（官网 2026-Q2 定价）
        else if (modelId.StartsWith("deepseek-v4-flash", StringComparison.OrdinalIgnoreCase) ||
                 modelId.StartsWith("deepseek-chat", StringComparison.OrdinalIgnoreCase) ||
                 modelId.StartsWith("deepseek-reasoner", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(1m, 2m, 0.02m, 1m);
        else if (modelId.StartsWith("deepseek-v4-pro", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(3m, 6m, 0.025m, 3m);
        else if (modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            pricing = new AiModelPricing(1m, 2m, 0.02m, 1m);

        return new AiProviderCapabilities(thinking, funcCall, vision, audio, speech, imageGen, videoGen, false, false, contextLength, reasoningEfforts, pricing);
    }

    /// <summary>根据模型 ID 推断可读显示名称。将连字符分隔的各段首字母大写，如 qwen3.7-max → Qwen3.7 Max</summary>
    /// <remarks>子类可重写以实现服务商特定格式（如品牌名全大写等）。已在 <see cref="AiClientDescriptor"/> 注册的模型优先使用 DisplayName 属性，此方法作为未注册模型的兜底推断</remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的显示名称，输入为空时返回 null</returns>
    public virtual String? InferModelDisplayName(String? modelId)
    {
        if (modelId.IsNullOrEmpty()) return null;

        var parts = modelId!.Split('-');
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            var part = parts[i];
            if (part.Length > 0 && Char.IsLower(part[0]))
            {
                sb.Append(Char.ToUpper(part[0]));
                if (part.Length > 1) sb.Append(part, 1, part.Length - 1);
            }
            else
                sb.Append(part);
        }
        return sb.ToString();
    }
    #endregion
}
