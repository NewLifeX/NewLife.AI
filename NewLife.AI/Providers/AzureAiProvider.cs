using System.Net.Http;
using System.Net.Http.Headers;

namespace NewLife.AI.Providers;

/// <summary>Azure AI Foundry 服务商。Azure 托管的 OpenAI 模型</summary>
/// <remarks>
/// Azure OpenAI 支持两种部署模式：
/// <list type="bullet">
/// <item>Azure AI Foundry（models.inference.ai.azure.com）：无需创建资源组，按模型直接调用</item>
/// <item>Azure OpenAI Service（{resource}.openai.azure.com）：需要在 Azure 门户创建资源并部署模型</item>
/// </list>
/// Azure OpenAI Service 的 Endpoint 需要用户配置为自己的资源地址。
/// 认证方式：Bearer Token 或 api-key 头。
/// </remarks>
public class AzureAiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "AzureAI";

    /// <summary>服务商名称</summary>
    public override String Name => "微软Azure AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "微软 Azure AI 平台，支持 OpenAI 模型及多种开源模型";

    /// <summary>默认 API 地址。Azure AI Foundry 推理入口</summary>
    public override String DefaultEndpoint => "https://models.inference.ai.azure.com";

    /// <summary>主流模型列表。Azure AI 平台托管的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("gpt-4o",        "GPT-4o",         new(false, true,  false, true)),
        new("gpt-4o-mini",   "GPT-4o Mini",    new(false, true,  false, true)),
        new("o3-mini",       "o3-mini",        new(true,  false, false, true)),
        new("DeepSeek-R1",   "DeepSeek-R1",    new(true,  false, false, true)),
        new("Phi-4",         "Phi-4",          new(false, true,  false, true)),
    ];
}
