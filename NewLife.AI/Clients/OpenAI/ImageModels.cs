using System.Runtime.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>图像生成请求。兼容 OpenAI /v1/images/generations（DALL·E 3）接口格式</summary>
/// <remarks>
/// 阿里百炼 Wanx 系列模型（wanx3.0-t2i-turbo 等）通过 compatible-mode 端点支持此格式。
/// 官方参考：https://help.aliyun.com/zh/model-studio/getting-started/models
/// </remarks>
public class ImageGenerationRequest
{
    /// <summary>模型编码。如 wanx3.0-t2i-turbo、wanx3.0-t2i-plus、dall-e-3</summary>
    [DataMember(Name = "model")]
    public String? Model { get; set; }

    /// <summary>图像提示词（正向描述）</summary>
    [DataMember(Name = "prompt")]
    public String Prompt { get; set; } = null!;

    /// <summary>负向提示词。描述不希望出现的内容。部分服务商专有（如 Wanx）</summary>
    [DataMember(Name = "negative_prompt")]
    public String? NegativePrompt { get; set; }

    /// <summary>生成图像数量。1~10，默认 1</summary>
    [DataMember(Name = "n")]
    public Int32? N { get; set; }

    /// <summary>图像尺寸。如 1024x1024、1024x1792、1792x1024</summary>
    [DataMember(Name = "size")]
    public String? Size { get; set; }

    /// <summary>图像质量。standard（默认）或 hd（高清，DALL·E 3 专有）</summary>
    [DataMember(Name = "quality")]
    public String? Quality { get; set; }

    /// <summary>画面风格。vivid（鲜明，DALL·E 3）/ realistic（写实）/ anime（动漫）。依服务商而异</summary>
    [DataMember(Name = "style")]
    public String? Style { get; set; }

    /// <summary>响应格式。url（默认，返回图片链接）或 b64_json（返回 Base64）</summary>
    [DataMember(Name = "response_format")]
    public String? ResponseFormat { get; set; }

    /// <summary>用户标识。用于追踪和限流</summary>
    [DataMember(Name = "user")]
    public String? User { get; set; }
}

/// <summary>语音合成请求。兼容 OpenAI /v1/audio/speech 接口格式</summary>
public class SpeechRequest
{
    /// <summary>TTS 模型编码。如 tts-1、cosyvoice-v2</summary>
    [DataMember(Name = "model")]
    public String Model { get; set; } = "tts-1";

    /// <summary>要合成的文本内容</summary>
    [DataMember(Name = "input")]
    public String Input { get; set; } = null!;

    /// <summary>音色名称。如 longxiaochun、alloy</summary>
    [DataMember(Name = "voice")]
    public String Voice { get; set; } = null!;

    /// <summary>音频格式。mp3（默认）/ wav / opus / flac / pcm</summary>
    [DataMember(Name = "response_format")]
    public String? ResponseFormat { get; set; }

    /// <summary>语速倍率。0.25~4.0，默认 1.0</summary>
    [DataMember(Name = "speed")]
    public Double? Speed { get; set; }
}

/// <summary>图像编辑请求。对应 OpenAI /v1/images/edits（multipart/form-data）接口参数</summary>
/// <remarks>含 Stream 字段，仅用于参数传递，不可 JSON 序列化</remarks>
public class ImageEditsRequest
{
    /// <summary>原始图像流（PNG 格式）</summary>
    public Stream ImageStream { get; set; } = null!;

    /// <summary>图像文件名。默认 image.png</summary>
    public String ImageFileName { get; set; } = "image.png";

    /// <summary>编辑提示词</summary>
    public String Prompt { get; set; } = null!;

    /// <summary>模型名称，为 null 时使用默认</summary>
    public String? Model { get; set; }

    /// <summary>输出尺寸，为 null 时使用服务端默认。如 1024x1024</summary>
    public String? Size { get; set; }

    /// <summary>蒙版图像流（可选，PNG 格式，透明区域为编辑区域）</summary>
    public Stream? MaskStream { get; set; }

    /// <summary>蒙版文件名。默认 mask.png</summary>
    public String? MaskFileName { get; set; }
}

/// <summary>图像生成响应。兼容 OpenAI /v1/images/generations 与 /v1/images/edits 返回格式</summary>
public class ImageGenerationResponse
{
    /// <summary>生成时间戳（Unix 秒）</summary>
    public DateTime Created { get; set; }

    /// <summary>图像数据列表</summary>
    public ImageData[]? Data { get; set; }
}

/// <summary>单张图像数据</summary>
public class ImageData
{
    /// <summary>修正后的提示词（部分服务商在安全过滤后会返回修改版）</summary>
    public String? RevisedPrompt { get; set; }

    /// <summary>图像 URL（url 响应格式）</summary>
    public String? Url { get; set; }

    /// <summary>图像 Base64 内容（b64_json 响应格式）</summary>
    public String? B64Json { get; set; }

    /// <summary>图像内容（旧版 content 字段，StarChat 网关兼容）</summary>
    public String? Content { get; set; }
}
