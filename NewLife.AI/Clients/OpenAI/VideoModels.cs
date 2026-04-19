namespace NewLife.AI.Clients.OpenAI;

/// <summary>视频生成请求。兼容 OpenAI Sora 和阿里百炼 Wan2 系列接口</summary>
/// <remarks>
/// 视频生成为异步任务模式：提交请求获取任务 ID，轮询查询直到完成。
/// OpenAI Sora：POST /v1/video/generations
/// DashScope Wan2：POST /api/v1/services/aigc/video-generation/video-synthesis
/// </remarks>
public class VideoGenerationRequest
{
    /// <summary>模型编码。如 sora、wan2.7-t2v、wan2.7-i2v</summary>
    public String? Model { get; set; }

    /// <summary>视频描述提示词</summary>
    public String Prompt { get; set; } = null!;

    /// <summary>视频尺寸。如 1280*720、1920*1080。不同模型支持范围不同</summary>
    public String? Size { get; set; }

    /// <summary>视频时长（秒）。如 5、10。部分服务商需要此参数</summary>
    public Int32? Duration { get; set; }

    /// <summary>参考图像 URL（图生视频场景）。为空时为纯文生视频</summary>
    public String? ImageUrl { get; set; }

    /// <summary>帧率。如 24 fps</summary>
    public Int32? Fps { get; set; }

    /// <summary>负向提示词。描述不希望出现的内容</summary>
    public String? NegativePrompt { get; set; }

    /// <summary>随机种子。用于结果复现</summary>
    public Int64? Seed { get; set; }
}

/// <summary>视频生成任务提交响应</summary>
public class VideoTaskSubmitResponse
{
    /// <summary>任务编号。后续轮询时使用</summary>
    public String? TaskId { get; set; }

    /// <summary>请求编号。部分服务商返回，用于问题排查</summary>
    public String? RequestId { get; set; }

    /// <summary>任务状态。PENDING / RUNNING / SUCCEEDED / FAILED</summary>
    public String? Status { get; set; }
}

/// <summary>视频生成任务查询响应</summary>
public class VideoTaskStatusResponse
{
    /// <summary>任务编号</summary>
    public String? TaskId { get; set; }

    /// <summary>请求编号</summary>
    public String? RequestId { get; set; }

    /// <summary>任务状态。PENDING / RUNNING / SUCCEEDED / FAILED</summary>
    public String? Status { get; set; }

    /// <summary>视频结果 URL 列表。任务完成后可用</summary>
    public String[]? VideoUrls { get; set; }

    /// <summary>错误码。任务失败时返回</summary>
    public String? ErrorCode { get; set; }

    /// <summary>错误信息。任务失败时返回</summary>
    public String? ErrorMessage { get; set; }
}
