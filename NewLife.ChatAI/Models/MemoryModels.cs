namespace NewLife.ChatAI.Models;

/// <summary>更新记忆请求</summary>
public class UpdateMemoryRequest
{
    /// <summary>新的值</summary>
    public String? Value { get; set; }
    /// <summary>新的置信度</summary>
    public Int32? Confidence { get; set; }
    /// <summary>新的分类</summary>
    public String? Category { get; set; }
    /// <summary>是否有效（切换启停用）</summary>
    public Boolean? Enable { get; set; }
}


