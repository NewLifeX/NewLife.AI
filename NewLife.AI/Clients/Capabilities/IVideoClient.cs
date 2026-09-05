using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients;

/// <summary>视频生成能力接口。视频生成普遍采用异步任务模式：提交任务获取 TaskId，再轮询查询直至完成</summary>
/// <remarks>
/// 已实现：OpenAI（Sora 预览）、DashScope（Wan 系列）、NewLifeAI；
/// 官方支持但 SDK 未实现：Gemini（Veo 2）、Bedrock（Nova Reel）——调用方按 <c>client is IVideoClient</c> 判断，未实现时返回 MODEL_UNSUPPORTED；
/// 不实现：DeepSeek、Anthropic、Ollama、Azure。
/// </remarks>
public interface IVideoClient
{
    /// <summary>提交视频生成任务。返回 TaskId 用于后续轮询</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务提交响应，含 TaskId</returns>
    Task<VideoTaskSubmitResponse> SubmitVideoGenerationAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>查询视频生成任务状态</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态响应。完成时含视频 URL 列表</returns>
    Task<VideoTaskStatusResponse> GetVideoTaskAsync(String taskId, CancellationToken cancellationToken = default);
}
