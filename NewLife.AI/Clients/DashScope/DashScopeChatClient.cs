using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

/// <summary>阿里百炼（DashScope）对话客户端。支持 DashScope 原生协议与 OpenAI 兼容协议双模式</summary>
/// <remarks>
/// 通过 <see cref="AiClientOptions.Protocol"/> 控制协议模式：
/// <list type="bullet">
/// <item>"DashScope"（默认/空）：使用阿里云 DashScope 原生协议，走 /api/v1 端点</item>
/// <item>"ChatCompletions"：使用 OpenAI 兼容协议，走 /compatible-mode 端点，复用基类逻辑</item>
/// </list>
/// 官方文档：https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope
/// </remarks>
/// <remarks>用连接选项初始化 DashScope 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model、Protocol 等）</param>
[AiClient("DashScope", "阿里百炼", "https://dashscope.aliyuncs.com/api/v1", Protocol = "DashScope", Description = "阿里云百炼大模型平台，支持 Qwen/通义千问全系列商业版模型")]
[AiClientModel("qwen3-max", "Qwen3 Max", Thinking = true)]
[AiClientModel("qwen3.5-plus", "Qwen3.5 Plus", Thinking = true, Vision = true)]
[AiClientModel("qwen3.5-flash", "Qwen3.5 Flash", Vision = true)]
[AiClientModel("qwq-plus", "QwQ Plus", Thinking = true)]
[AiClientModel("qwen3-plus", "Qwen3 Plus", Thinking = true)]
[AiClientModel("qwen-vl-max", "Qwen VL Max", Vision = true)]
[AiClientModel("qwen3-coder", "Qwen3 Coder")]
[AiClientModel("wanx2.1-t2i-turbo", "Wanx 文生图", ImageGeneration = true, FunctionCalling = false)]
public class DashScopeChatClient(AiClientOptions options) : OpenAIChatClient(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "阿里百炼";

    /// <summary>原生 DashScope API 基础地址（/api/v1）</summary>
    protected virtual String NativeEndpoint => "https://dashscope.aliyuncs.com/api/v1";

    /// <summary>兼容模式基础地址。Embedding、重排序等沿用此端点</summary>
    protected virtual String CompatibleEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <inheritdoc/>
    public override String DefaultEndpoint
    {
        get => IsNativeProtocol ? NativeEndpoint : CompatibleEndpoint;
        set => base.DefaultEndpoint = value;
    }

    /// <summary>是否使用 DashScope 原生协议。Protocol 为空或 "DashScope" 时为原生模式</summary>
    protected Boolean IsNativeProtocol => _options.Protocol.IsNullOrEmpty() || _options.Protocol == "DashScope";
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建阿里百炼客户端</summary>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public DashScopeChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 对话（重写）
    /// <summary>非流式对话。原生协议走 DashScope 格式，兼容模式委托基类</summary>
    protected override async Task<IChatResponse> ChatAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
            return await base.ChatAsync(request, cancellationToken).ConfigureAwait(false);

        var model = request.Model ?? _options.Model;
        var url = BuildUrl(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));
        var json = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        var dashResp = json.ToJsonEntity<DashScopeResponse>()!;
        if (!dashResp.Code.IsNullOrEmpty())
            throw new HttpRequestException($"[DashScope] 错误 {dashResp.Code}: {dashResp.Message}");

        // 原生响应无顶层 model 字段，从请求回填
        dashResp.Model = model;

        return dashResp;
    }

    /// <summary>流式对话。原生协议走 DashScope SSE 格式，兼容模式委托基类</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
        {
            await foreach (var chunk in base.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var url = BuildUrl(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

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

            IChatResponse? chunk = null;
            try { chunk = ParseChunk(data, request, null); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }
    #endregion

    #region 模型列表
    /// <summary>获取可用模型列表。使用兼容模式端点以保证返回完整模型目录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public override async Task<OpenAiModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/models";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new OpenAiModelListResponse { Object = dic["object"] as String };

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
    /// <summary>文档重排序。对 RAG 检索召回的候选文档按语义相关度重新排序</summary>
    /// <param name="request">重排序请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重排序响应</returns>
    public async Task<RerankResponse> RerankAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/reranks";
        var body = new Dictionary<String, Object?>
        {
            ["model"] = !String.IsNullOrEmpty(request.Model) ? request.Model : "gte-rerank-v2",
            ["input"] = new Dictionary<String, Object> { ["query"] = request.Query, ["documents"] = request.Documents },
            ["parameters"] = BuildRerankParameters(request),
        };
        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseRerankResponse(json);
    }

    private static Dictionary<String, Object> BuildRerankParameters(RerankRequest request)
    {
        var p = new Dictionary<String, Object> { ["return_documents"] = request.ReturnDocuments };
        if (request.TopN != null) p["top_n"] = request.TopN.Value;
        return p;
    }

    private static RerankResponse ParseRerankResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析重排序响应");

        var resp = new RerankResponse { RequestId = dic["request_id"] as String };

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
                var docVal = r["document"];
                if (docVal != null)
                    result.Document = docVal is IDictionary<String, Object> docDic
                        ? docDic["text"] as String
                        : docVal as String;
                results.Add(result);
            }
            resp.Results = results;
        }

        if (dic["usage"] is IDictionary<String, Object> usage)
            resp.Usage = new RerankUsage { TotalTokens = usage["total_tokens"].ToInt() };

        return resp;
    }
    #endregion

    #region 辅助
    // 原生对话路径（纯文本）
    private const String ChatGenerationPath = "/services/aigc/text-generation/generation";

    // 原生对话路径（多模态：含视觉/音频/视频输入）
    private const String MultimodalGenerationPath = "/services/aigc/multimodal-generation/generation";

    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var path = IsMultimodalModel(request.Model) ? MultimodalGenerationPath : ChatGenerationPath;

        // 原生协议只能对接 /api/v1 端点；若用户配置了兼容模式地址则忽略并回退到原生端点
        var endpoint = _options.Endpoint;
        if (endpoint.IsNullOrWhiteSpace() ||
            endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
            endpoint = NativeEndpoint;
        return endpoint.TrimEnd('/') + path;
    }

    /// <summary>判断指定模型是否为多模态模型（需走 multimodal-generation 端点）</summary>
    /// <remarks>
    /// 命名规律：
    /// <list type="bullet">
    /// <item>含 -vl：Vision-Language 系列</item>
    /// <item>qvq- 前缀：视觉推理系列（区别于纯文本推理 qwq-）</item>
    /// <item>qwen3.5- 前缀：内置多模态能力，仅支持 multimodal-generation 端点</item>
    /// </list>
    /// </remarks>
    private static Boolean IsMultimodalModel(String? model)
    {
        if (String.IsNullOrEmpty(model)) return false;
        if (model.IndexOf("-vl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (model.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase)) return true;
        if (model.StartsWith("qwen3.5-", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>解析 DashScope 原生流式 SSE chunk，DashScopeResponse 适配器同时设置 Delta</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
    {
        var chunk = data.ToJsonEntity<DashScopeResponse>();
        if (chunk != null) chunk.Model = request.Model;
        return chunk;
    }

    /// <inheritdoc/>
    /// <remarks>多模态响应中 content 为数组格式（[{"text":"..."}]），归一化为字符串</remarks>
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

    /// <inheritdoc/>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!IsNativeProtocol) return;
        if (chatRequest == null || !chatRequest.Stream) return;

        var path = request.RequestUri?.AbsolutePath;
        if (String.IsNullOrEmpty(path)) return;

        if (!path.EndsWith(ChatGenerationPath, StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(MultimodalGenerationPath, StringComparison.OrdinalIgnoreCase)) return;

        // qwen-plus 不能识别为多模态，得使用文本完成地址，但是accept需要text/event-stream
        //var model = chatRequest?.Model ?? options.Model;
        //if (IsMultimodalModel(model) || model.EndsWithIgnoreCase("-max", "-plus", "-turbo"))
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        //else
        request.Headers.TryAddWithoutValidation("X-DashScope-SSE", "enable");
    }

    /// <summary>根据千问模型 ID 命名规律推断模型能力</summary>
    /// <remarks>
    /// 阿里百炼模型命名规律：
    /// <list type="bullet">
    /// <item>qwen*-vl* / qvq-*：视觉能力</item>
    /// <item>qwen3.5-*：全系列内置多模态（视觉）</item>
    /// <item>qwq-* / 含 max/plus 的高端系列：思考能力</item>
    /// <item>wanx* / flux*：文生图能力</item>
    /// <item>embed*/rerank*/paraformer*/cosyvoice*：非对话模型</item>
    /// <item>farui* / qwen-mt*：专用模型，不支持函数调用</item>
    /// </list>
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public override AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (String.IsNullOrEmpty(modelId)) return null;

        // 非对话模型：嵌入、重排序、语音识别、语音合成
        if (modelId.StartsWith("text-embedding", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("paraformer", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("cosyvoice", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("sambert", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, false);

        var thinking = false;
        var vision = false;
        var imageGen = false;
        var funcCall = true;

        // 文生图：wanx / flux 系列
        if (modelId.StartsWith("wanx", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("flux", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("stable-diffusion", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, true, false);

        // 视觉能力
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen3.5-", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // 思考/推理能力
        if (modelId.StartsWith("qwq-", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 高端系列（max/plus）默认支持思考
        if (modelId.Contains("-max", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-plus", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // qwen3 全系列支持思考
        if (modelId.StartsWith("qwen3-", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen3.", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 专用模型不支持函数调用
        if (modelId.StartsWith("farui", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-mt", StringComparison.OrdinalIgnoreCase))
            funcCall = false;

        return new AiProviderCapabilities(thinking, vision, imageGen, funcCall);
    }
    #endregion
}
