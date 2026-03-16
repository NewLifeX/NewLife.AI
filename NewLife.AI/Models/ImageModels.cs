using System;

namespace NewLife.AI.Models;

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
