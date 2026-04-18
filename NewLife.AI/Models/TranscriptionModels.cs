namespace NewLife.AI.Models;

/// <summary>语音识别（STT）请求。兼容 OpenAI /v1/audio/transcriptions multipart/form-data 接口</summary>
/// <remarks>
/// 输入二选一：本地文件流（<see cref="File"/> + <see cref="FileName"/>）或远程文件 URL（<see cref="FileUrl"/>）。
/// OpenAI 仅支持文件流；DashScope Paraformer 异步任务模式支持 URL。
/// </remarks>
public class TranscriptionRequest
{
    /// <summary>STT 模型编码。如 whisper-1、gpt-4o-transcribe、paraformer-v2</summary>
    public String? Model { get; set; }

    /// <summary>音频文件流。与 <see cref="FileUrl"/> 二选一</summary>
    public Stream? File { get; set; }

    /// <summary>音频文件名。含扩展名，如 audio.mp3</summary>
    public String? FileName { get; set; }

    /// <summary>音频文件 URL。与 <see cref="File"/> 二选一</summary>
    public String? FileUrl { get; set; }

    /// <summary>音频语言 ISO-639-1 代码。如 zh / en，不指定则自动检测</summary>
    public String? Language { get; set; }

    /// <summary>提示词。用于引导识别风格或专有名词</summary>
    public String? Prompt { get; set; }

    /// <summary>响应格式。json（默认）/ text / srt / verbose_json / vtt</summary>
    public String? ResponseFormat { get; set; }

    /// <summary>采样温度。0~1，默认 0</summary>
    public Double? Temperature { get; set; }

    /// <summary>时间戳粒度。<c>word</c>(单词级) 或 <c>segment</c>(片段级)，需配合 verbose_json</summary>
    public IList<String>? TimestampGranularities { get; set; }
}

/// <summary>语音识别响应</summary>
public class TranscriptionResponse
{
    /// <summary>识别文本（完整）</summary>
    public String? Text { get; set; }

    /// <summary>检测到的语言</summary>
    public String? Language { get; set; }

    /// <summary>音频时长（秒）</summary>
    public Double? Duration { get; set; }

    /// <summary>片段级时间戳。verbose_json + segment 粒度时返回</summary>
    public IList<TranscriptionSegment>? Segments { get; set; }

    /// <summary>词级时间戳。verbose_json + word 粒度时返回</summary>
    public IList<TranscriptionWord>? Words { get; set; }

    /// <summary>错误码。失败时返回</summary>
    public String? ErrorCode { get; set; }

    /// <summary>错误信息。失败时返回</summary>
    public String? ErrorMessage { get; set; }
}

/// <summary>识别片段</summary>
public class TranscriptionSegment
{
    /// <summary>片段序号</summary>
    public Int32 Id { get; set; }

    /// <summary>开始时间（秒）</summary>
    public Double Start { get; set; }

    /// <summary>结束时间（秒）</summary>
    public Double End { get; set; }

    /// <summary>片段文本</summary>
    public String? Text { get; set; }
}

/// <summary>识别单词</summary>
public class TranscriptionWord
{
    /// <summary>开始时间（秒）</summary>
    public Double Start { get; set; }

    /// <summary>结束时间（秒）</summary>
    public Double End { get; set; }

    /// <summary>单词文本</summary>
    public String? Word { get; set; }
}
