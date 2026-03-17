namespace NewLife.AI.Models;

/// <summary>文档重排序请求。用于 RAG 检索后对候选文档按与查询的相关度重新排序</summary>
/// <remarks>
/// 目前阿里百炼（DashScope）的重排序接口格式为 DashScope 专有，
/// 由 DashScopeProvider.RerankAsync 负责将此通用请求转换为实际请求体。
/// </remarks>
public class RerankRequest
{
    /// <summary>模型编码。如 gte-rerank、gte-rerank-v2</summary>
    public String? Model { get; set; }

    /// <summary>查询文本</summary>
    public String Query { get; set; } = null!;

    /// <summary>候选文档列表。每项为纯文本字符串，最多 100 条</summary>
    public IList<String> Documents { get; set; } = [];

    /// <summary>返回结果数量上限。默认与候选文档数相同</summary>
    public Int32? TopN { get; set; }

    /// <summary>是否在响应中返回原始文档文本。默认 true</summary>
    public Boolean ReturnDocuments { get; set; } = true;
}

/// <summary>文档重排序响应</summary>
public class RerankResponse
{
    /// <summary>请求编号</summary>
    public String? RequestId { get; set; }

    /// <summary>重排序结果列表（按相关度降序）</summary>
    public IList<RerankResult> Results { get; set; } = [];

    /// <summary>令牌用量统计</summary>
    public RerankUsage? Usage { get; set; }
}

/// <summary>重排序结果项</summary>
public class RerankResult
{
    /// <summary>原始文档在 Documents 列表中的下标（0 起）</summary>
    public Int32 Index { get; set; }

    /// <summary>相关度分数。0~1，越高越相关</summary>
    public Double RelevanceScore { get; set; }

    /// <summary>文档文本（RerankRequest.ReturnDocuments=true 时返回）</summary>
    public String? Document { get; set; }
}

/// <summary>重排序令牌用量统计</summary>
public class RerankUsage
{
    /// <summary>总令牌数</summary>
    public Int32 TotalTokens { get; set; }
}
