using System.Net.Http.Headers;
using NewLife.AI.Models;
using NewLife.AI.Providers;

namespace NewLife.AI.Clients;

/// <summary>Azure OpenAI 对话客户端。支持 Azure 托管的 OpenAI 模型部署</summary>
/// <remarks>
/// Azure OpenAI 使用与标准 OpenAI 不同的 URL 格式和认证方式：
/// <list type="bullet">
/// <item>URL 包含 deployment 名称和 api-version 查询参数</item>
/// <item>使用 api-key 请求头代替 Bearer Token 认证</item>
/// </list>
/// Endpoint 由用户填入完整 URL，如 https://myresource.openai.azure.com
/// </remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 即 deployment 名称）</param>
[AiClient("AzureAI", "Azure OpenAI", "https://{resource}.openai.azure.com",
    Description = "微软 Azure 托管的 OpenAI 模型服务，使用 deployment 方式部署", Order = 3)]
public class AzureAIChatClient(AiClientOptions options) : OpenAIChatClient(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "AzureAI";

    /// <summary>API 版本。默认 2024-10-21</summary>
    public String ApiVersion { get; set; } = "2024-10-21";
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选 deployment 名称快速创建 Azure OpenAI 客户端</summary>
    /// <param name="apiKey">Azure API Key</param>
    /// <param name="model">deployment 名称（对应 Azure 中的模型部署）</param>
    /// <param name="endpoint">Azure OpenAI 完整地址，如 https://myresource.openai.azure.com</param>
    public AzureAIChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。Azure OpenAI 使用 deployment 方式的 URL 格式</summary>
    protected override String BuildUrl(ChatRequest request)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var model = request.Model ?? _options.Model;
        return $"{endpoint}/openai/deployments/{model}/chat/completions?api-version={ApiVersion}";
    }

    /// <summary>设置请求头。Azure OpenAI 使用 api-key 请求头认证</summary>
    protected override void SetHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Add("api-key", options.ApiKey);
    }
    #endregion
}
