using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients;

/// <summary>语音合成（TTS）能力接口。文本转语音字节流</summary>
/// <remarks>
/// 已实现：OpenAI（tts-1 / gpt-4o-mini-tts）、DashScope（CosyVoice，可选）、NewLifeAI、Azure；
/// 不实现：DeepSeek、Anthropic、Bedrock（AWS 音频在 Polly 独立服务）、Ollama。
/// 
/// 流式语音合成：已在 netstandard2.1+ 声明接口但暂未实现。
/// DashScope CosyVoice 原生支持 WebSocket 流式合成，详见《CosyVoice WebSocket 流式合成》文档。
/// </remarks>
public interface ISpeechClient
{
    /// <summary>语音合成。返回完整音频字节流（格式由 request.ResponseFormat 决定，默认 mp3）</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流</returns>
    Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default);

    /// <summary>流式语音合成。以异步枚举方式逐段返回音频字节流，支持边生成边播放</summary>
    /// <remarks>
    /// 暂未实现，默认抛出 <see cref="NotSupportedException"/>。
    /// DashScope CosyVoice 通过 WebSocket（wss://dashscope.aliyuncs.com/api-ws/v1/inference）实现流式合成，
    /// 详见 Doc/《CosyVoice WebSocket 流式合成.md》。
    /// OpenAI 兼容服务商通过 SSE 分块 /v1/audio/speech?stream=true 实现。
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐段返回音频字节分片</returns>
    IAsyncEnumerable<Byte[]> SpeechStreamAsync(SpeechRequest request, CancellationToken cancellationToken = default);
}
