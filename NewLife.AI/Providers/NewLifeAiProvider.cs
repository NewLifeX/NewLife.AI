namespace NewLife.AI.Providers;

/// <summary>新生命 AI 服务商。新生命团队的统一 AI 网关，兼容 OpenAI 协议</summary>
/// <remarks>
/// 星语（StarChat）网关，支持多模型路由、负载均衡和流量控制。
/// 接入地址：https://ai.newlifex.com/ai
/// </remarks>
public class NewLifeAiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "NewLifeAI";

    /// <summary>服务商名称</summary>
    public override String Name => "新生命AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "新生命团队星语 AI 网关，统一对接多种大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://ai.newlifex.com/ai";

    /// <summary>主流模型列表。通过网关路由到后端各服务商模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("qwen-plus",         "Qwen Plus",     new(true,  true,  false, true)),
        new("deepseek-chat",     "DeepSeek V3",   new(false, false, false, true)),
        new("deepseek-reasoner", "DeepSeek R1",   new(true,  false, false, true)),
        new("glm-4-flash",       "GLM-4 Flash",   new(false, false, false, true)),
    ];
}
