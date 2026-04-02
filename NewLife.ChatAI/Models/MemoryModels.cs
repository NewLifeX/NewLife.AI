namespace NewLife.ChatAI.Models;

/// <summary>记忆列表响应</summary>
public class MemoryListDto
{
    /// <summary>总条数</summary>
    public Int32 Total { get; set; }
    /// <summary>当前页</summary>
    public Int32 Page { get; set; }
    /// <summary>每页条数</summary>
    public Int32 PageSize { get; set; }
    /// <summary>记忆列表</summary>
    public IList<MemoryItemDto> Items { get; set; } = [];
}

/// <summary>记忆条目 DTO</summary>
public class MemoryItemDto
{
    /// <summary>记忆ID</summary>
    public Int64 Id { get; set; }
    /// <summary>分类</summary>
    public String? Category { get; set; }
    /// <summary>键</summary>
    public String? Key { get; set; }
    /// <summary>值</summary>
    public String? Value { get; set; }
    /// <summary>置信度</summary>
    public Int32 Confidence { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>更新记忆请求</summary>
public class UpdateMemoryRequest
{
    /// <summary>新的值</summary>
    public String? Value { get; set; }
    /// <summary>新的置信度</summary>
    public Int32? Confidence { get; set; }
    /// <summary>新的分类</summary>
    public String? Category { get; set; }
}


