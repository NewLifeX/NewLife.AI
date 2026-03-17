namespace NewLife.AI.Models;

/// <summary>图像生成请求。兼容 OpenAI /v1/images/generations（DALL·E 3）接口格式</summary>
/// <remarks>
/// 阿里百炼 Wanx 系列模型（wanx3.0-t2i-turbo 等）通过 compatible-mode 端点支持此格式。
/// 官方参考：https://help.aliyun.com/zh/model-studio/getting-started/models
/// </remarks>
public class ImageGenerationRequest
{
    /// <summary>模型编码。如 wanx3.0-t2i-turbo、wanx3.0-t2i-plus、dall-e-3</summary>
    public String? Model { get; set; }

    /// <summary>图像提示词（正向描述）</summary>
    public String Prompt { get; set; } = null!;

    /// <summary>负向提示词。描述不希望出现的内容。部分服务商专有（如 Wanx）</summary>
    public String? NegativePrompt { get; set; }

    /// <summary>生成图像数量。1~10，默认 1</summary>
    public Int32? N { get; set; }

    /// <summary>图像尺寸。如 1024x1024、1024x1792、1792x1024</summary>
    public String? Size { get; set; }

    /// <summary>图像质量。standard（默认）或 hd（高清，DALL·E 3 专有）</summary>
    public String? Quality { get; set; }

    /// <summary>画面风格。vivid（鲜明，DALL·E 3）/ realistic（写实）/ anime（动漫）。依服务商而异</summary>
    public String? Style { get; set; }

    /// <summary>响应格式。url（默认，返回图片链接）或 b64_json（返回 Base64）</summary>
    public String? ResponseFormat { get; set; }

    /// <summary>用户标识。用于追踪和限流</summary>
    public String? User { get; set; }
}

/// <summary>图像生成响应。兼容 OpenAI /v1/images/generations 与 /v1/images/edits 返回格式</summary>
public class ImageGenerationResponse
{
    /// <summary>生成时间戳（Unix 秒）</summary>
    public Int64 Created { get; set; }

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
