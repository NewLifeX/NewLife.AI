using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Embedding;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>阿里百炼（DashScope）服务商。使用 DashScope 原生协议，支持 Qwen 全系列模型</summary>
/// <remarks>
/// 原生协议端点 https://dashscope.aliyuncs.com/api/v1/<br/>
/// Embedding / 重排序沿用兼容模式 https://dashscope.aliyuncs.com/compatible-mode<br/>
/// 官方文档：https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope
/// </remarks>
public class DashScopeProvider : OpenAiProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public override String Code => "DashScope";

    /// <summary>服务商名称</summary>
    public override String Name => "阿里百炼";

    /// <summary>服务商描述</summary>
    public override String? Description => "阿里云百炼大模型平台，支持 Qwen/通义千问全系列商业版模型";

    /// <summary>API 协议类型。DashScope 原生协议（默认）或 ChatCompletions 兼容协议</summary>
    public override String ApiProtocol { get; set; } = "DashScope";

    /// <summary>默认 API 地址。原生协议使用 DashScope 专属端点，兼容模式使用兼容端点</summary>
    public override String DefaultEndpoint => ApiProtocol == "DashScope" ? NativeEndpoint : CompatibleEndpoint;

    /// <summary>原生 DashScope API 基础地址（/api/v1）</summary>
    protected virtual String NativeEndpoint => "https://dashscope.aliyuncs.com/api/v1";

    /// <summary>兼容模式基础地址。Embedding、重排序等沿用此端点</summary>
    protected virtual String CompatibleEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <summary>主流对话模型列表。阿里百炼/通义千问各主力商业版对话模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        // Qwen Max 旗舰系列（纯文本，支持思考/非思考双模式，上下文 262K）
        new("qwen3-max",     "Qwen3 Max",     new(true,  false, false, true)),

        // Qwen Plus 均衡系列（多模态：文本 + 图像 + 视频，支持思考模式，上下文 1M）
        new("qwen3.5-plus",  "Qwen3.5 Plus",  new(true,  true,  false, true)),

        // Qwen Flash 高速低价系列（多模态，支持思考模式，上下文 1M）
        new("qwen3.5-flash", "Qwen3.5 Flash", new(true,  true,  false, true)),

        // QwQ 推理系列（基于强化学习大幅提升推理能力，数学/代码/逻辑对标 DeepSeek-R1）
        new("qwq-plus",         "QwQ Plus",         new(true,  false, false, true)),

        //// Qwen Long 超长上下文（上下文 10M Token，适合长文档分析、信息抽取、摘要）
        //new("qwen-long",        "Qwen Long",        new(false, false, false, true)),

        //// Qwen VL 视觉理解系列（图像 + 视频输入，支持思考模式，上下文 262K）
        //new("qwen3-vl-plus",    "Qwen3 VL Plus",    new(true,  true,  false, true)),
        //new("qwen3-vl-flash",   "Qwen3 VL Flash",   new(true,  true,  false, true)),

        //// QVQ 视觉推理系列（视觉 + 思维链，擅长数学/编程/视觉分析，上下文 131K）
        //new("qvq-max",          "QVQ Max",          new(true,  true,  false, true)),

        //// Qwen Coder 代码专用系列（支持自主编程 Agent，工具调用能力强，上下文 1M）
        //new("qwen3-coder-plus", "Qwen3 Coder Plus", new(false, false, false, true)),
    ];

    /// <summary>文本嵌入模型列表。用于语义搜索、RAG 向量化、相似度计算等场景</summary>
    /// <remarks>
    /// 通过基类 <see cref="OpenAiProvider.CreateEmbeddingClient"/> 创建客户端后调用。
    /// 端点：POST /compatible-mode/v1/embeddings
    /// </remarks>
    public AiModelInfo[] EmbeddingModels { get; } =
    [
        new("text-embedding-v3", "通用文本向量 V3", new(false, false, false, false)),
        new("text-embedding-v2", "通用文本向量 V2", new(false, false, false, false)),
        new("text-embedding-v1", "通用文本向量 V1", new(false, false, false, false)),
    ];

    /// <summary>文生图模型列表。Wanx 万象系列，通过 <see cref="OpenAiProvider.TextToImageAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/images/generations</remarks>
    public AiModelInfo[] ImageModels { get; } =
    [
        new("wanx3.0-t2i-turbo", "万象3.0 Turbo", new(false, false, true, false)),
        new("wanx3.0-t2i-plus",  "万象3.0 Plus",  new(false, false, true, false)),
        new("wanx2.1-t2i-turbo", "万象2.1 Turbo", new(false, false, true, false)),
        new("wanx2.1-t2i-plus",  "万象2.1 Plus",  new(false, false, true, false)),
    ];

    /// <summary>语音合成模型列表。CosyVoice 系列，通过 <see cref="OpenAiProvider.SpeechAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/audio/speech</remarks>
    public AiModelInfo[] TtsModels { get; } =
    [
        new("cosyvoice-v2", "CosyVoice V2", new(false, false, false, false)),
        new("cosyvoice-v1", "CosyVoice V1", new(false, false, false, false)),
    ];

    /// <summary>文档重排序模型列表。GTE-Rerank 系列，通过 <see cref="RerankAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/reranks（DashScope 专有，非 OpenAI 标准）</remarks>
    public AiModelInfo[] RerankModels { get; } =
    [
        new("gte-rerank-v2", "GTE Rerank V2", new(false, false, false, false)),
        new("gte-rerank",    "GTE Rerank",    new(false, false, false, false)),
    ];
    #endregion

    #region 工厂方法
    /// <summary>创建已绑定连接参数的对话客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IChatClient 实例</returns>
    public override IChatClient CreateClient(AiProviderOptions options)
    {
        if (options.Model.IsNullOrEmpty() && Models != null && Models.Length > 0) options.Model = Models[0].Model;

        return new OpenAiChatClient(this, options) { Log = Log, Tracer = Tracer };
    }

    /// <summary>创建嵌入向量客户端。使用兼容模式地址，与文本对话原生地址隔离</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IEmbeddingClient 实例</returns>
    public override IEmbeddingClient CreateEmbeddingClient(AiProviderOptions options)
    {
        // Embedding 沿用兼容模式 /compatible-mode/v1/embeddings
        var embOptions = new AiProviderOptions
        {
            ApiKey = options.ApiKey,
            Model = options.Model,
            Endpoint = CompatibleEndpoint,
        };
        return new OpenAiEmbeddingClient(Name, CompatibleEndpoint, embOptions);
    }

    /// <summary>创建 DashScope 专属对话选项实例。返回 <see cref="DashScopeChatOptions"/> 以便强类型设置 DashScope 高级参数</summary>
    /// <returns>新建的 DashScopeChatOptions 实例</returns>
    public override ChatOptions CreateChatOptions() => new DashScopeChatOptions();
    #endregion

    #region 对话（ChatAsync / ChatStreamAsync）
    /// <summary>非流式对话。根据 ApiProtocol 选择原生协议或 OpenAI 兼容协议</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整对话响应</returns>
    public override async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (ApiProtocol != "DashScope") return await base.ChatAsync(request, options, cancellationToken).ConfigureAwait(false);

        var url = BuildChatUrl(options);
        var isMultimodal = IsMultimodalModel(options.Model);
        var body = BuildDashScopeRequestBody(request, isMultimodal, false);
        var json = await PostAsync(url, body, options, cancellationToken).ConfigureAwait(false);

        var response = ParseDashScopeResponse(json);
        // 原生响应无顶层 model 字段，从请求回填
        response.Model ??= request.Model;
        return response;
    }

    /// <summary>流式对话。根据 ApiProtocol 选择原生 SSE 协议或 OpenAI 兼容协议</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (ApiProtocol != "DashScope")
        {
            await foreach (var chunk in base.ChatStreamAsync(request, options, cancellationToken))
                yield return chunk;
            yield break;
        }

        var url = BuildChatUrl(options);
        var isMultimodal = IsMultimodalModel(options.Model);
        var body = BuildDashScopeRequestBody(request, isMultimodal, true);

        using var resp = await PostStreamAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            // SSE 格式：id: N  /  event: result  /  data: {...}
            if (line.StartsWith("id:")) continue;

            if (line.StartsWith("event:"))
            {
                lastEvent = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            if (lastEvent == "error")
            {
                var errDic = JsonParser.Decode(data);
                var code = errDic?["code"] as String ?? "error";
                var message = errDic?["message"] as String ?? data;
                throw new HttpRequestException($"[{Name}] 流式错误 {code}: {message}");
            }

            ChatCompletionResponse? chunk = null;
            try
            {
                chunk = ParseDashScopeStreamChunk(data);
            }
            catch
            {
                // 跳过无法解析的行
            }

            if (chunk != null)
            {
                // 原生响应无顶层 model 字段，从请求回填
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }
    #endregion

    #region 模型列表
    /// <summary>获取该服务商当前可用的模型列表。使用兼容模式 /v1/models 端点</summary>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public override async Task<OpenAiModelListResponse?> ListModelsAsync(AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        // 模型列表 API 使用兼容模式
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/models";
        var json = await TryGetAsync(url, options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new OpenAiModelListResponse
        {
            Object = dic["object"] as String,
        };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<OpenAiModelObject>(dataList.Count);
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new OpenAiModelObject
                {
                    Id = d["id"] as String,
                    Object = d["object"] as String,
                    OwnedBy = d["owned_by"] as String,
                    Created = d["created"].ToLong().ToDateTime(),
                });
            }
            response.Data = [.. items];
        }
        return response;
    }
    #endregion

    #region 重排序（Rerank）
    /// <summary>文档重排序。对 RAG 检索召回的候选文档按与查询的语义相关度重新排序</summary>
    /// <remarks>
    /// DashScope 专有接口，使用 /v1/reranks 路径（非 OpenAI 标准），请求格式为：
    /// {"model":"...", "input":{"query":"...","documents":[...]}, "parameters":{"top_n":N,"return_documents":true}}
    /// 推荐模型：gte-rerank-v2（精度较高）、gte-rerank（轻量快速）。
    /// </remarks>
    /// <param name="request">重排序请求，包含 Query、Documents、TopN 等字段</param>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重排序响应，Results 按相关度降序排列</returns>
    public async Task<RerankResponse> RerankAsync(RerankRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/reranks";

        var body = new Dictionary<String, Object?>
        {
            ["model"] = !String.IsNullOrEmpty(request.Model) ? request.Model : RerankModels[0].Model,
            ["input"] = new Dictionary<String, Object>
            {
                ["query"] = request.Query,
                ["documents"] = request.Documents,
            },
            ["parameters"] = BuildRerankParameters(request),
        };

        var json = await PostAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        return ParseRerankResponse(json);
    }

    private static Dictionary<String, Object> BuildRerankParameters(RerankRequest request)
    {
        var p = new Dictionary<String, Object>
        {
            ["return_documents"] = request.ReturnDocuments,
        };
        if (request.TopN != null) p["top_n"] = request.TopN.Value;
        return p;
    }

    private static RerankResponse ParseRerankResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析重排序响应");

        var resp = new RerankResponse
        {
            RequestId = dic["request_id"] as String,
        };

        // DashScope 的结果嵌套在 output.results 中
        if (dic["output"] is IDictionary<String, Object> output &&
            output["results"] is IList<Object> resultList)
        {
            var results = new List<RerankResult>(resultList.Count);
            foreach (var item in resultList)
            {
                if (item is not IDictionary<String, Object> r) continue;
                var result = new RerankResult
                {
                    Index = r["index"].ToInt(),
                    RelevanceScore = r["relevance_score"].ToDouble(),
                };
                // document 字段：字典（含 text 子键）或直接字符串
                var docVal = r["document"];
                if (docVal != null)
                    result.Document = docVal is IDictionary<String, Object> docDic ? docDic["text"] as String : docVal as String;
                results.Add(result);
            }
            resp.Results = results;
        }

        if (dic["usage"] is IDictionary<String, Object> usage)
        {
            resp.Usage = new RerankUsage { TotalTokens = usage["total_tokens"].ToInt() };
        }
        return resp;
    }
    #endregion

    #region 辅助（请求构建 / 响应解析）
    // 原生对话接口路径（纯文本模型，如 qwen3-max / qwq-plus）
    private const String ChatGenerationPath = "/services/aigc/text-generation/generation";

    // 多模态对话接口路径（SupportVision=true 的模型，如 qwen3.5-plus / qwen3.5-flash / qwen3-vl-*）
    private const String MultimodalGenerationPath = "/services/aigc/multimodal-generation/generation";

    private String BuildChatUrl(AiProviderOptions options)
    {
        var path = IsMultimodalModel(options.Model) ? MultimodalGenerationPath : ChatGenerationPath;
        // 原生协议只能对接 /api/v1 端点；若用户在数据库中配置的是兼容模式地址（含 compatible-mode），
        // 须忽略该配置，回退到原生端点，避免将原生路径拼接到兼容端点上导致 URL 错误
        var endpoint = options.Endpoint;
        if (String.IsNullOrWhiteSpace(endpoint) ||
            endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
            endpoint = NativeEndpoint;
        return endpoint.TrimEnd('/') + path;
    }

    /// <summary>判断指定模型是否为多模态模型（需使用 multimodal-generation 端点）</summary>
    /// <remarks>
    /// DashScope 多模态模型命名规律：<br/>
    /// 1. 包含 -vl：Vision-Language 系列，如 qwen3-vl-plus、qwen2.5-vl-72b、qwen-vl-max<br/>
    /// 2. 以 qvq- 开头：视觉推理系列，如 qvq-max、qvq-plus（注意 qwq- 是纯文本推理，不在此列）<br/>
    /// 3. 以 qwen3.5- 开头：3.5 系列内置多模态能力，如 qwen3.5-plus、qwen3.5-flash<br/>
    /// 以上规律均来自官方文档，未匹配时回退到 Models 注册表的 SupportVision 标志
    /// </remarks>
    /// <param name="model">模型标识</param>
    private Boolean IsMultimodalModel(String? model)
    {
        if (String.IsNullOrEmpty(model)) return false;

        // Vision-Language 系列：含 -vl（大小写不敏感）
        if (model.IndexOf("-vl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        // 视觉推理系列：前缀 qvq-（区别于纯文本推理 qwq-）
        if (model.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase)) return true;
        // qwen3.5 系列：内置多模态能力，需走 multimodal-generation 端点
        if (model.StartsWith("qwen3.5-", StringComparison.OrdinalIgnoreCase)) return true;

        // 兜底：查 Models 注册表（覆盖未匹配名称规律的自定义/未来模型）
        if (Models != null)
        {
            foreach (var info in Models)
            {
                if (info.Model.Equals(model, StringComparison.OrdinalIgnoreCase))
                    return info.Capabilities.SupportVision;
            }
        }
        return false;
    }

    /// <summary>构建 DashScope 原生请求体</summary>
    /// <param name="request">对话请求</param>
    /// <param name="isMultimodal">是否多模态模型。多模态端点要求 content 为数组格式</param>
    /// <param name="stream">是否开启流式输出</param>
    private static Object BuildDashScopeRequestBody(ChatCompletionRequest request, Boolean isMultimodal, Boolean stream)
    {
        var messages = BuildMessages(request.Messages, isMultimodal);

        // parameters 字典
        var parameters = new Dictionary<String, Object>
        {
            ["result_format"] = "message",
        };

        // 通用参数：从 ChatCompletionRequest 标准属性读取
        if (request.Temperature != null) parameters["temperature"] = request.Temperature.Value;
        if (request.TopP != null) parameters["top_p"] = request.TopP.Value;
        if (request.MaxTokens != null) parameters["max_tokens"] = request.MaxTokens.Value;
        if (request.Stop != null && request.Stop.Count > 0) parameters["stop"] = request.Stop;
        if (request.PresencePenalty != null) parameters["presence_penalty"] = request.PresencePenalty.Value;
        if (request.FrequencyPenalty != null) parameters["frequency_penalty"] = request.FrequencyPenalty.Value;
        if (request.EnableThinking != null) parameters["enable_thinking"] = request.EnableThinking.Value;
        if (request.ResponseFormat != null) parameters["response_format"] = request.ResponseFormat;
        if (request.Tools != null && request.Tools.Count > 0)
            parameters["tools"] = BuildToolsParam(request.Tools);
        if (request.ToolChoice != null) parameters["tool_choice"] = request.ToolChoice;
        if (request.ParallelToolCalls != null) parameters["parallel_tool_calls"] = request.ParallelToolCalls.Value;

        // DashScope 专属参数 —— 强类型路径（DashScopeChatOptions）优先，回退到 Items 字典
        if (request.Options is DashScopeChatOptions dashOpts)
            ApplyDashScopeOptions(parameters, dashOpts);
        else
            ApplyDashScopeItems(parameters, request);

        // 流式输出：同时需要请求体 stream:true 和 HTTP 头 X-DashScope-SSE:enable
        if (stream)
        {
            parameters["stream"] = true;
            parameters["incremental_output"] = true;
        }

        return new Dictionary<String, Object>
        {
            ["model"] = request.Model ?? "",
            ["input"] = new Dictionary<String, Object> { ["messages"] = messages },
            ["parameters"] = parameters,
        };
    }

    /// <summary>将 DashScopeChatOptions 强类型属性写入 parameters 字典</summary>
    /// <param name="parameters">目标参数字典</param>
    /// <param name="opts">DashScope 专属选项</param>
    private static void ApplyDashScopeOptions(Dictionary<String, Object> parameters, DashScopeChatOptions opts)
    {
        if (opts.TopK != null) parameters["top_k"] = opts.TopK.Value;
        if (opts.Seed != null) parameters["seed"] = opts.Seed.Value;
        if (opts.RepetitionPenalty != null) parameters["repetition_penalty"] = opts.RepetitionPenalty.Value;
        if (opts.N != null) parameters["n"] = opts.N.Value;
        if (opts.ThinkingBudget != null) parameters["thinking_budget"] = opts.ThinkingBudget.Value;
        if (opts.EnableCodeInterpreter != null) parameters["enable_code_interpreter"] = opts.EnableCodeInterpreter.Value;
        if (opts.Logprobs != null) parameters["logprobs"] = opts.Logprobs.Value;
        if (opts.TopLogprobs != null) parameters["top_logprobs"] = opts.TopLogprobs.Value;
        if (opts.VlHighResolutionImages != null) parameters["vl_high_resolution_images"] = opts.VlHighResolutionImages.Value;
        if (opts.VlEnableImageHwOutput != null) parameters["vl_enable_image_hw_output"] = opts.VlEnableImageHwOutput.Value;
        if (opts.MaxPixels != null) parameters["max_pixels"] = opts.MaxPixels.Value;
        if (opts.EnableSearch != null) parameters["enable_search"] = opts.EnableSearch.Value;
        var searchOptions = new Dictionary<String, Object>();
        if (!String.IsNullOrEmpty(opts.SearchStrategy)) searchOptions["search_strategy"] = opts.SearchStrategy;
        if (opts.EnableSource != null) searchOptions["enable_source"] = opts.EnableSource.Value;
        if (opts.ForcedSearch != null) searchOptions["forced_search"] = opts.ForcedSearch.Value;
        if (searchOptions.Count > 0) parameters["search_options"] = searchOptions;
    }

    /// <summary>从 request.Items 字典读取 DashScope 专属参数并写入 parameters（兼容旧接口）</summary>
    /// <param name="parameters">目标参数字典</param>
    /// <param name="request">对话请求</param>
    private static void ApplyDashScopeItems(Dictionary<String, Object> parameters, ChatCompletionRequest request)
    {
        var topK = request["TopK"] as Int32?;
        if (topK != null) parameters["top_k"] = topK.Value;
        var seed = request["Seed"] as Int32?;
        if (seed != null) parameters["seed"] = seed.Value;
        var repetitionPenalty = request["RepetitionPenalty"] as Double?;
        if (repetitionPenalty != null) parameters["repetition_penalty"] = repetitionPenalty.Value;
        var n = request["N"] as Int32?;
        if (n != null) parameters["n"] = n.Value;
        var thinkingBudget = request["ThinkingBudget"] as Int32?;
        if (thinkingBudget != null) parameters["thinking_budget"] = thinkingBudget.Value;
        var enableCodeInterpreter = request["EnableCodeInterpreter"] as Boolean?;
        if (enableCodeInterpreter != null) parameters["enable_code_interpreter"] = enableCodeInterpreter.Value;
        var logprobs = request["Logprobs"] as Boolean?;
        if (logprobs != null) parameters["logprobs"] = logprobs.Value;
        var topLogprobs = request["TopLogprobs"] as Int32?;
        if (topLogprobs != null) parameters["top_logprobs"] = topLogprobs.Value;
        var vlHighResolutionImages = request["VlHighResolutionImages"] as Boolean?;
        if (vlHighResolutionImages != null) parameters["vl_high_resolution_images"] = vlHighResolutionImages.Value;
        var vlEnableImageHwOutput = request["VlEnableImageHwOutput"] as Boolean?;
        if (vlEnableImageHwOutput != null) parameters["vl_enable_image_hw_output"] = vlEnableImageHwOutput.Value;
        var maxPixels = request["MaxPixels"] as Int32?;
        if (maxPixels != null) parameters["max_pixels"] = maxPixels.Value;
        var enableSearch = request["EnableSearch"] as Boolean?;
        if (enableSearch != null) parameters["enable_search"] = enableSearch.Value;
        var searchOptions = new Dictionary<String, Object>();
        var searchStrategy = request["SearchStrategy"] as String;
        if (!String.IsNullOrEmpty(searchStrategy)) searchOptions["search_strategy"] = searchStrategy;
        var enableSource = request["EnableSource"] as Boolean?;
        if (enableSource != null) searchOptions["enable_source"] = enableSource.Value;
        var forcedSearch = request["ForcedSearch"] as Boolean?;
        if (forcedSearch != null) searchOptions["forced_search"] = forcedSearch.Value;
        if (searchOptions.Count > 0) parameters["search_options"] = searchOptions;
    }

    /// <summary>构建 messages 数组。多轮对话时不传入历史 reasoning_content（DashScope 最佳实践）</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="isMultimodal">是否多模态端点。多模态端点要求 content 始终为数组格式</param>
    private static IList<Object> BuildMessages(IList<ChatMessage> messages, Boolean isMultimodal)
    {
        var result = new List<Object>(messages.Count);
        foreach (var msg in messages)
        {
            var m = new Dictionary<String, Object?> { ["role"] = msg.Role };

            // 类型化内容优先
            if (msg.Contents != null && msg.Contents.Count > 0)
                m["content"] = BuildContent(msg.Contents, isMultimodal);
            else if (isMultimodal)
                // DashScope 多模态端点要求 content 为数组格式：[{"text": "..."}]
                m["content"] = new List<Object> { new { text = msg.Content ?? "" } };
            else
                m["content"] = msg.Content;

            if (msg.Name != null) m["name"] = msg.Name;
            if (msg.ToolCallId != null) m["tool_call_id"] = msg.ToolCallId;

            // assistant 消息含工具调用时附上 tool_calls 数组
            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<Object>(msg.ToolCalls.Count);
                foreach (var tc in msg.ToolCalls)
                {
                    var tcDic = new Dictionary<String, Object?>
                    {
                        ["id"] = tc.Id,
                        ["type"] = tc.Type,
                    };
                    if (tc.Function != null)
                    {
                        var args = String.IsNullOrEmpty(tc.Function.Arguments) ? "{}" : tc.Function.Arguments;
                        tcDic["function"] = new Dictionary<String, Object?>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = args,
                        };
                    }
                    toolCalls.Add(tcDic);
                }
                m["tool_calls"] = toolCalls;
            }

            // 历史消息的 reasoning_content 不传入（多轮最佳实践：避免思考过程污染上下文）
            result.Add(m);
        }
        return result;
    }

    /// <summary>构建 tools 参数数组，支持 function / mcp / web_search / code_interpreter 类型</summary>
    private static IList<Object> BuildToolsParam(IList<ChatTool> tools)
    {
        var result = new List<Object>(tools.Count);
        foreach (var tool in tools)
        {
            var t = new Dictionary<String, Object?> { ["type"] = tool.Type };

            if (tool.Type == "function" && tool.Function != null)
            {
                var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                t["function"] = fn;
            }
            else if (tool.Type == "mcp" && tool.Mcp != null)
            {
                var mcp = new Dictionary<String, Object>();
                if (tool.Mcp.ServerUrl != null) mcp["server_url"] = tool.Mcp.ServerUrl;
                if (tool.Mcp.ServerId != null) mcp["server_id"] = tool.Mcp.ServerId;
                if (tool.Mcp.Configs != null) mcp["configs"] = tool.Mcp.Configs;
                if (tool.Mcp.AllowedTools != null) mcp["allowed_tools"] = tool.Mcp.AllowedTools;
                if (tool.Mcp.Authorization != null)
                {
                    mcp["authorization"] = new Dictionary<String, Object?>
                    {
                        ["type"] = tool.Mcp.Authorization.Type,
                        ["token"] = tool.Mcp.Authorization.Token,
                    };
                }
                t["mcp"] = mcp;
            }
            else if (tool.Config != null)
            {
                // web_search / code_interpreter 等内置工具的额外参数
                t[tool.Type ?? "config"] = tool.Config;
            }

            result.Add(t);
        }
        return result;
    }

    /// <param name="contents">内容列表</param>
    /// <param name="isMultimodal">是否多模态端点。DashScope 原生多模态格式与 OpenAI 格式不同</param>
    private static Object BuildContent(IList<AIContent> contents, Boolean isMultimodal = false)
    {
        // 纯文本端点：单个文本内容直接返回字符串（OpenAI 兼容格式）
        if (!isMultimodal && contents.Count == 1 && contents[0] is TextContent singleText)
            return singleText.Text;

        var parts = new List<Object>(contents.Count);
        foreach (var item in contents)
        {
            if (item is TextContent text)
            {
                if (isMultimodal)
                    // DashScope 原生多模态：{"text": "..."}
                    parts.Add(new { text = text.Text });
                else
                    // OpenAI 兼容格式：{"type": "text", "text": "..."}
                    parts.Add(new { type = "text", text = text.Text });
            }
            else if (item is ImageContent img)
            {
                String url;
                if (img.Data != null && img.Data.Length > 0)
                    url = $"data:{img.MediaType ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                else
                    url = img.Uri ?? "";

                if (isMultimodal)
                    // DashScope 原生多模态：{"image": "url_or_data_uri"}
                    parts.Add(new { image = url });
                else if (img.Detail != null)
                    // OpenAI 兼容格式：{"type": "image_url", "image_url": {"url": "...", "detail": "..."}}
                    parts.Add(new { type = "image_url", image_url = new { url, detail = img.Detail } });
                else
                    parts.Add(new { type = "image_url", image_url = new { url } });
            }
            else if (item is DataContent dataCnt && isMultimodal)
            {
                // 音频、视频等二进制内容（仅支持多模态端点）
                var dataUri = $"data:{dataCnt.MediaType};base64,{Convert.ToBase64String(dataCnt.Data)}";
                if (dataCnt.MediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                    // DashScope 音频：{"audio": "data:audio/wav;base64,..."}
                    parts.Add(new { audio = dataUri });
                else if (dataCnt.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    // DashScope 视频：{"video": "data:video/mp4;base64,..."}
                    parts.Add(new { video = dataUri });
                else
                    // 其他二进制（如 PDF 文档）作为 file 类型传递
                    parts.Add(new { file = dataUri });
            }
        }
        return parts;
    }

    /// <summary>解析 DashScope 非流式响应</summary>
    private ChatCompletionResponse ParseDashScopeResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 DashScope 响应");

        // 业务层错误检查（HTTP 200 但业务失败时，code 为非空字符串）
        var errCode = dic["code"] as String;
        var errMsg = dic["message"] as String;
        if (!String.IsNullOrEmpty(errCode))
            throw new HttpRequestException($"[{Name}] 错误 {errCode}: {errMsg}");

        var response = new ChatCompletionResponse
        {
            Object = "chat.completion",
            Id = dic["request_id"] as String,
        };

        // 原生格式：output.choices[]
        if (dic["output"] is IDictionary<String, Object> output &&
            output["choices"] is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>(choicesList.Count);
            for (var i = 0; i < choicesList.Count; i++)
            {
                if (choicesList[i] is not IDictionary<String, Object> choiceDic) continue;
                var choice = new DashScopeChoice
                {
                    Index = i,
                    FinishReason = choiceDic["finish_reason"] as String,
                    Message = ParseChatMessage(choiceDic["message"] as IDictionary<String, Object>),
                    Logprobs = choiceDic.TryGetValue("logprobs", out var lp) ? lp : null,
                };
                choices.Add(choice);
            }
            response.Choices = choices;
        }

        // usage：原生字段名为 input_tokens / output_tokens
        if (dic["usage"] is IDictionary<String, Object> usageDic)
            response.Usage = ParseDashScopeUsage(usageDic);

        return response;
    }

    /// <summary>解析 DashScope 流式 SSE chunk。增量输出时字段名为 message（语义同 OpenAI 的 delta）</summary>
    private ChatCompletionResponse? ParseDashScopeStreamChunk(String data)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatCompletionResponse
        {
            Object = "chat.completion.chunk",
            Id = dic["request_id"] as String,
        };

        if (dic["output"] is IDictionary<String, Object> output &&
            output["choices"] is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>(choicesList.Count);
            for (var i = 0; i < choicesList.Count; i++)
            {
                if (choicesList[i] is not IDictionary<String, Object> choiceDic) continue;
                var choice = new DashScopeChoice
                {
                    Index = i,
                    FinishReason = choiceDic["finish_reason"] as String,
                };

                // DashScope incremental_output=true 时，content 是增量文本，语义同 OpenAI delta
                // delta 字段（如存在）优先，否则回落 message 字段
                IDictionary<String, Object>? incrementalField = null;
                if (choiceDic["delta"] is IDictionary<String, Object> dd)
                    incrementalField = dd;
                else if (choiceDic["message"] is IDictionary<String, Object> md)
                    incrementalField = md;
                choice.Delta = ParseChatMessage(incrementalField);
                if (choiceDic.TryGetValue("logprobs", out var lp2))
                    choice.Logprobs = lp2;
                choices.Add(choice);
            }
            response.Choices = choices;
        }

        if (dic["usage"] is IDictionary<String, Object> usageDic2)
            response.Usage = ParseDashScopeUsage(usageDic2);

        return response;
    }

    /// <summary>解析 DashScope 用量统计。原生字段名为 input_tokens / output_tokens，多模态还含 image/video/audio_tokens</summary>
    /// <param name="usageDic">usage 字典</param>
    private static DashScopeUsage ParseDashScopeUsage(IDictionary<String, Object> usageDic)
    {
        return new DashScopeUsage
        {
            PromptTokens = usageDic["input_tokens"].ToInt(),
            CompletionTokens = usageDic["output_tokens"].ToInt(),
            TotalTokens = usageDic["total_tokens"].ToInt(),
            ImageTokens = usageDic.TryGetValue("image_tokens", out var img) ? img.ToInt() : 0,
            VideoTokens = usageDic.TryGetValue("video_tokens", out var vid) ? vid.ToInt() : 0,
            AudioTokens = usageDic.TryGetValue("audio_tokens", out var aud) ? aud.ToInt() : 0,
        };
    }

    /// <summary>消息解析扩展点。将多模态响应中的 content 数组归一化为字符串</summary>
    /// <remarks>
    /// 多模态端点（multimodal-generation）响应的 content 字段为数组格式，如 [{"text":"..."}]，
    /// 而文本端点（text-generation）为普通字符串。统一归一化为字符串以便上层代码一致处理。
    /// </remarks>
    /// <param name="msg">已完成基础解析的消息对象</param>
    /// <param name="dic">原始 JSON 字典</param>
    protected override void OnParseChatMessage(ChatMessage msg, IDictionary<String, Object> dic)
    {
        if (msg.Content is not IList<Object> contentList) return;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in contentList)
        {
            if (item is IDictionary<String, Object> d && d["text"] is String t)
                sb.Append(t);
        }
        msg.Content = sb.Return(true);
    }

    /// <summary>设置请求头。注入 DashScope Bearer 认证</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">连接选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (ApiProtocol != "DashScope") return;

        var path = request.RequestUri?.AbsolutePath;
        if (String.IsNullOrEmpty(path)) return;

        if (!path.EndsWith(ChatGenerationPath, StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(MultimodalGenerationPath, StringComparison.OrdinalIgnoreCase)) return;

        if (IsMultimodalModel(options.Model))
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        else
            request.Headers.TryAddWithoutValidation("X-DashScope-SSE", "enable");
    }

    #endregion
}

/// <summary>DashScope 专属对话选项。继承 <see cref="ChatOptions"/> 并扩展原生协议高级参数，供强类型访问以替代 Items 字典写法</summary>
/// <remarks>
/// 通过 <see cref="DashScopeProvider.CreateChatOptions"/> 获取实例，然后设置所需参数，
/// 再传入 IChatClient.CompleteAsync/CompleteStreamingAsync 的 options 参数。
/// <code>
/// var opts = (DashScopeChatOptions)provider.CreateChatOptions();
/// opts.EnableSearch = true;
/// opts.TopK = 10;
/// var response = await client.CompleteAsync(messages, opts);
/// </code>
/// </remarks>
public class DashScopeChatOptions : ChatOptions
{
    /// <summary>候选词数量。从 top_k 个概率最高的 Token 中采样，默认不限制</summary>
    public Int32? TopK { get; set; }

    /// <summary>随机种子。固定种子可在相同参数下复现输出，范围 0~2^31-1</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚。大于 1 则抑制已出现的 Token，小于 1 则鼓励重复，默认 1.1</summary>
    public Double? RepetitionPenalty { get; set; }

    /// <summary>返回候选数量。同一输入独立生成 N 条不同输出，默认 1</summary>
    public Int32? N { get; set; }

    /// <summary>思考预算（Token 数）。0=关闭深度思考，-1=不限制，仅思考模型（QwQ/Qwen3）有效</summary>
    public Int32? ThinkingBudget { get; set; }

    /// <summary>是否启用代码解释器。qwen3.5 及思考模式模型可在对话中执行代码</summary>
    public Boolean? EnableCodeInterpreter { get; set; }

    /// <summary>是否返回对数概率。开启后响应携带每个输出 Token 的 logprob 信息</summary>
    public Boolean? Logprobs { get; set; }

    /// <summary>返回对数概率的 top-K Token 数。需同时设置 Logprobs=true，范围 0~20</summary>
    public Int32? TopLogprobs { get; set; }

    /// <summary>是否启用高分辨率图像（VL 专属）。开启后 VL 模型以更高分辨率理解图像细节</summary>
    public Boolean? VlHighResolutionImages { get; set; }

    /// <summary>是否在响应中输出图像宽高（VL 专属）</summary>
    public Boolean? VlEnableImageHwOutput { get; set; }

    /// <summary>图像最大像素数（VL 专属）。限制输入图像分辨率以控制 Token 消耗</summary>
    public Int32? MaxPixels { get; set; }

    /// <summary>是否启用联网搜索。开启后模型可实时检索互联网信息以增强回复</summary>
    public Boolean? EnableSearch { get; set; }

    /// <summary>搜索策略。intelligent（智能，默认）/ force（每次强制搜索）/ prohibited（禁止搜索）</summary>
    public String? SearchStrategy { get; set; }

    /// <summary>是否在回复中展示来源引用链接</summary>
    public Boolean? EnableSource { get; set; }

    /// <summary>是否强制搜索，即使模型判断无需搜索时仍执行</summary>
    public Boolean? ForcedSearch { get; set; }
}

/// <summary>DashScope 高级配置服务商。继承 <see cref="DashScopeProvider"/>，在服务商层面预设 DashScope 专属高级参数</summary>
/// <remarks>
/// 适用于需要为所有对话统一设置 DashScope 特定参数的场景。
/// 在服务商实例上配置参数后，通过 <see cref="CreateChatOptions"/> 即可生成预填充的选项对象。
/// <code>
/// var provider = new DashScopeAdvancedProvider { EnableSearch = true, TopK = 10 };
/// var opts = provider.CreateChatOptions();   // 返回 DashScopeChatOptions，已填充 EnableSearch/TopK
/// var client = provider.CreateClient(apiKey, model);
/// var response = await client.CompleteAsync(messages, opts);
/// </code>
/// </remarks>
public class DashScopeAdvancedProvider : DashScopeProvider
{
    #region DashScope 高级参数
    /// <summary>候选词数量。从 top_k 个概率最高的 Token 中采样</summary>
    public Int32? TopK { get; set; }

    /// <summary>随机种子。固定种子可在相同参数下复现输出</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚。大于 1 则抑制已出现的 Token，小于 1 则鼓励重复</summary>
    public Double? RepetitionPenalty { get; set; }

    /// <summary>返回候选数量。同一输入独立生成 N 条不同输出</summary>
    public Int32? N { get; set; }

    /// <summary>思考预算（Token 数）。0=关闭，-1=不限制，仅思考模型有效</summary>
    public Int32? ThinkingBudget { get; set; }

    /// <summary>是否启用代码解释器</summary>
    public Boolean? EnableCodeInterpreter { get; set; }

    /// <summary>是否返回对数概率</summary>
    public Boolean? Logprobs { get; set; }

    /// <summary>返回对数概率的 top-K Token 数</summary>
    public Int32? TopLogprobs { get; set; }

    /// <summary>是否启用高分辨率图像（VL 专属）</summary>
    public Boolean? VlHighResolutionImages { get; set; }

    /// <summary>是否在响应中输出图像宽高（VL 专属）</summary>
    public Boolean? VlEnableImageHwOutput { get; set; }

    /// <summary>图像最大像素数（VL 专属）</summary>
    public Int32? MaxPixels { get; set; }

    /// <summary>是否启用联网搜索</summary>
    public Boolean? EnableSearch { get; set; }

    /// <summary>搜索策略。intelligent / force / prohibited</summary>
    public String? SearchStrategy { get; set; }

    /// <summary>是否在回复中展示来源引用链接</summary>
    public Boolean? EnableSource { get; set; }

    /// <summary>是否强制搜索</summary>
    public Boolean? ForcedSearch { get; set; }
    #endregion

    /// <summary>创建预填充服务商高级参数的 DashScope 对话选项</summary>
    /// <returns>已填充当前服务商配置的 DashScopeChatOptions 实例</returns>
    public override ChatOptions CreateChatOptions() => new DashScopeChatOptions
    {
        TopK = TopK,
        Seed = Seed,
        RepetitionPenalty = RepetitionPenalty,
        N = N,
        ThinkingBudget = ThinkingBudget,
        EnableCodeInterpreter = EnableCodeInterpreter,
        Logprobs = Logprobs,
        TopLogprobs = TopLogprobs,
        VlHighResolutionImages = VlHighResolutionImages,
        VlEnableImageHwOutput = VlEnableImageHwOutput,
        MaxPixels = MaxPixels,
        EnableSearch = EnableSearch,
        SearchStrategy = SearchStrategy,
        EnableSource = EnableSource,
        ForcedSearch = ForcedSearch,
    };
}

/// <summary>DashScope 专属选择项。继承 <see cref="ChatChoice"/> 并扩展 logprobs 字段</summary>
public class DashScopeChoice : ChatChoice
{
    /// <summary>对数概率信息。当请求参数 logprobs=true 时返回，包含输出 Token 的概率分布</summary>
    public Object? Logprobs { get; set; }
}

/// <summary>DashScope 专属用量统计。继承 <see cref="ChatUsage"/> 并扩展多模态 Token 字段</summary>
public class DashScopeUsage : ChatUsage
{
    /// <summary>图像 Token 数。多模态请求中图像输入消耗的 Token 数</summary>
    public Int32 ImageTokens { get; set; }

    /// <summary>视频 Token 数。多模态请求中视频输入消耗的 Token 数</summary>
    public Int32 VideoTokens { get; set; }

    /// <summary>音频 Token 数。多模态请求中音频输入消耗的 Token 数</summary>
    public Int32 AudioTokens { get; set; }
}
