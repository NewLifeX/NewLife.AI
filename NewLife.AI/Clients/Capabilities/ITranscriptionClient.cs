using NewLife.AI.Models;

namespace NewLife.AI.Clients.Capabilities;

/// <summary>语音识别（STT）能力接口。音频转文本</summary>
/// <remarks>
/// 已实现：OpenAI（Whisper / gpt-4o-transcribe）、DashScope（Paraformer，可选）、NewLifeAI、Azure；
/// 不实现：DeepSeek、Anthropic、Gemini（多模态 chat 输入音频，无独立 STT 端点）、Bedrock（Transcribe 独立服务）、Ollama。
/// </remarks>
public interface ITranscriptionClient
{
    /// <summary>音频转文本。OpenAI 兼容 multipart/form-data 上传</summary>
    /// <param name="request">语音识别请求。File 流或 FileUrl 二选一</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>识别文本与可选时间戳</returns>
    Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default);
}
