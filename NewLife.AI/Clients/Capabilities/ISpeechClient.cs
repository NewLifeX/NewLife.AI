using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients.Capabilities;

/// <summary>语音合成（TTS）能力接口。文本转语音字节流</summary>
/// <remarks>
/// 已实现：OpenAI（tts-1 / gpt-4o-mini-tts）、DashScope（CosyVoice，可选）、NewLifeAI、Azure；
/// 不实现：DeepSeek、Anthropic、Bedrock（AWS 音频在 Polly 独立服务）、Ollama。
/// </remarks>
public interface ISpeechClient
{
    /// <summary>语音合成。返回音频字节流（格式由 request.ResponseFormat 决定，默认 mp3）</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流</returns>
    Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default);
}
