namespace NewLife.AI.Providers;

/// <summary>深度求索服务商。支持 DeepSeek-V3/R1 推理模型</summary>
/// <remarks>
/// DeepSeek 原生支持 reasoning_content 字段返回推理思考过程。
/// 官方文档：https://platform.deepseek.com/api-docs
/// </remarks>
public class DeepSeekProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "DeepSeek";

    /// <summary>服务商名称</summary>
    public override String Name => "深度求索";

    /// <summary>服务商描述</summary>
    public override String? Description => "深度求索，DeepSeek-V3/R1 推理模型，支持思考过程输出";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.deepseek.com";

    /// <summary>主流模型列表。DeepSeek 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("deepseek-chat",     "DeepSeek V3", new(false, false, false, true)),
        new("deepseek-reasoner", "DeepSeek R1", new(true,  false, false, true)),
    ];
}
