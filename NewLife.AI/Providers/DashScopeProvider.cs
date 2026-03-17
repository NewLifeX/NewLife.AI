using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>阿里百炼（DashScope）服务商。支持 Qwen/通义千问全系列模型</summary>
/// <remarks>
/// 百炼平台提供 OpenAI 兼容模式接口，统一入口为 /compatible-mode/v1/...
/// 官方文档：https://help.aliyun.com/zh/model-studio/getting-started/models
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

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <summary>主流对话模型列表。阿里百炼/通义千问各主力商业版对话模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        // Qwen Max 旗舰系列（纯文本，支持思考/非思考双模式，上下文 262K）
        new("qwen3-max",        "Qwen3 Max",        new(true,  false, false, true)),
        //new("qwen-max",         "Qwen Max",         new(true,  false, false, true)),

        // Qwen Plus 均衡系列（多模态：文本 + 图像 + 视频，支持思考模式，上下文 1M）
        new("qwen3.5-plus",     "Qwen3.5 Plus",     new(true,  true,  false, true)),
        //new("qwen-plus",        "Qwen Plus",        new(true,  true,  false, true)),

        // Qwen Flash 高速低价系列（多模态，支持思考模式，上下文 1M）
        new("qwen3.5-flash",    "Qwen3.5 Flash",    new(true,  true,  false, true)),
        //new("qwen-flash",       "Qwen Flash",       new(true,  true,  false, true)),

        // Qwen Turbo（已停更，建议迁移至 Qwen Flash；当前版本支持思考模式，上下文 1M）
        new("qwen-turbo",       "Qwen Turbo",       new(true,  false, false, true)),

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
        new("text-embedding-v3",  "通用文本向量 V3", new(false, false, false, false)),  // 最高 2048 维，中英双语
        new("text-embedding-v2",  "通用文本向量 V2", new(false, false, false, false)),  // 1536 维，支持多语言
        new("text-embedding-v1",  "通用文本向量 V1", new(false, false, false, false)),  // 1536 维
    ];

    /// <summary>文生图模型列表。Wanx 万象系列，通过 <see cref="OpenAiProvider.TextToImageAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/images/generations</remarks>
    public AiModelInfo[] ImageModels { get; } =
    [
        new("wanx3.0-t2i-turbo", "万象3.0 Turbo",  new(false, false, true, false)),  // 快速，1024×1024，成本低
        new("wanx3.0-t2i-plus",  "万象3.0 Plus",   new(false, false, true, false)),  // 高质量，支持多种宽高比
        new("wanx2.1-t2i-turbo", "万象2.1 Turbo",  new(false, false, true, false)),
        new("wanx2.1-t2i-plus",  "万象2.1 Plus",   new(false, false, true, false)),
    ];

    /// <summary>语音合成模型列表。CosyVoice 系列，通过 <see cref="OpenAiProvider.SpeechAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/audio/speech</remarks>
    public AiModelInfo[] TtsModels { get; } =
    [
        new("cosyvoice-v2", "CosyVoice V2", new(false, false, false, false)),  // 最新版，音质最佳，支持流式
        new("cosyvoice-v1", "CosyVoice V1", new(false, false, false, false)),
    ];

    /// <summary>文档重排序模型列表。GTE-Rerank 系列，通过 <see cref="RerankAsync"/> 调用</summary>
    /// <remarks>端点：POST /compatible-mode/v1/reranks（DashScope 专有，非 OpenAI 标准）</remarks>
    public AiModelInfo[] RerankModels { get; } =
    [
        new("gte-rerank-v2", "GTE Rerank V2", new(false, false, false, false)),  // 多语言，精度较高
        new("gte-rerank",    "GTE Rerank",    new(false, false, false, false)),  // 轻量快速
    ];
    #endregion

    #region 重排序（Rerank）
    /// <summary>文档重排序。对 RAG 检索召回的候选文档按与查询的语义相关度重新排序</summary>
    /// <remarks>
    /// DashScope 专有接口，使用 /v1/reranks 路径（非 OpenAI 标准），请求格式为：
    /// {"model":"...", "input":{"query":"...","documents":[...]}, "parameters":{"top_n":N,"return_documents":true}}
    /// 推荐模型：gte-rerank-v2（精度较高）、gte-rerank（轻量快速）。
    /// </remarks>
    /// <param name="request">重排序请求，包含 Query、Documents、TopN 等字段</param>
    /// <param name="options">连接选项（Endpoint、ApiKey）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重排序响应，Results 按相关度降序排列</returns>
    public async Task<RerankResponse> RerankAsync(RerankRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/reranks";

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
                if (r["document"] is IDictionary<String, Object> docDic)
                    result.Document = docDic["text"] as String;
                else
                    result.Document = r["document"] as String;
                results.Add(result);
            }
            resp.Results = results;
        }

        if (dic["usage"] is IDictionary<String, Object> usage)
            resp.Usage = new RerankUsage { TotalTokens = usage["total_tokens"].ToInt() };

        return resp;
    }
    #endregion
}

