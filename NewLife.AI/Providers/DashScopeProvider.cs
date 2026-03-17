namespace NewLife.AI.Providers;

/// <summary>阿里百炼（DashScope）服务商。支持 Qwen/通义千问系列模型</summary>
/// <remarks>
/// 百炼平台提供 OpenAI 兼容模式接口，路径为 /compatible-mode/v1/chat/completions。
/// 官方文档：https://help.aliyun.com/zh/model-studio/
/// </remarks>
public class DashScopeProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "DashScope";

    /// <summary>服务商名称</summary>
    public override String Name => "阿里百炼";

    /// <summary>服务商描述</summary>
    public override String? Description => "阿里云百炼大模型平台，支持 Qwen/通义千问全系列模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <summary>主流模型列表。阿里百炼/通义千问各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("qwen-max",          "Qwen Max",          new(true,  true,  false, true)),
        new("qwen-plus",         "Qwen Plus",         new(true,  true,  false, true)),
        new("qwen-turbo",        "Qwen Turbo",        new(false, false, false, true)),
    ];
}
