namespace NewLife.AI.ModelContextProtocol;

/// <summary>进度通知值。用于IProgress接口</summary>
public class ProgressValue
{
    /// <summary>当前进度</summary>
    public Int32 Progress { get; set; }

    /// <summary>总进度</summary>
    public Int32 Total { get; set; }

    /// <summary>进度消息</summary>
    public String Message { get; set; } = String.Empty;
}
