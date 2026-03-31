using System.Runtime.CompilerServices;
using System.Text;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Anthropic;

/// <summary>Anthropic Claude 对话客户端。实现 Anthropic Messages API 原生协议</summary>
/// <remarks>
/// Anthropic API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 x-api-key 头传递，需附加 anthropic-version 头</item>
/// <item>system 消息为顶级独立字段，不在 messages 数组中</item>
/// <item>响应中的内容为 content 数组（text_delta / thinking_delta）</item>
/// <item>流式响应使用 event/data 格式而非 OpenAI 的 data-only 格式</item>
/// </list>
/// </remarks>
/// <remarks>用连接选项初始化 Anthropic 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
[AiClient("Anthropic", "Anthropic", "https://api.anthropic.com", Protocol = "AnthropicMessages", Description = "Anthropic Claude 系列模型")]
[AiClientModel("claude-opus-4-6", "Claude Opus 4.6", Thinking = true, Vision = true)]
[AiClientModel("claude-sonnet-4-6", "Claude Sonnet 4.6", Thinking = true, Vision = true)]
[AiClientModel("claude-haiku-4-5", "Claude Haiku 4.5", Thinking = true, Vision = true)]
public class AnthropicChatClient(AiClientOptions options) : AiClientBase(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "Anthropic";

    /// <summary>Anthropic API 版本</summary>
    protected virtual String ApiVersion => "2023-06-01";
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建 Anthropic 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public AnthropicChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 方法
    /// <summary>流式对话</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = BuildUrl(request);
        var body = BuildRequest(request);

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (line.StartsWith("event:"))
            {
                lastEvent = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseChunk(data, request, lastEvent);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        return $"{endpoint}/v1/messages";
    }

    /// <summary>构建 Anthropic 请求体</summary>
    /// <param name="request">请求</param>
    protected override Object BuildRequest(IChatRequest request)
    {
        if (request is AnthropicRequest ar) return ar;
        return AnthropicRequest.FromChatRequest(request);
    }

    /// <summary>解析 Anthropic 非流式响应</summary>
    protected override IChatResponse ParseResponse(String json, IChatRequest request)
    {
        var resp = json.ToJsonEntity<AnthropicResponse>()!;
        resp.Model ??= request.Model;
        return resp;
    }

    /// <summary>解析 Anthropic 流式 chunk</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
        => data.ToJsonEntity<AnthropicStreamEvent>()?.ToChunkResponse(request.Model);

    /// <summary>设置 Anthropic 认证请求头</summary>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Add("x-api-key", options.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
    }
    #endregion
}
