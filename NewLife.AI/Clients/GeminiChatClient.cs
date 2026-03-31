using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>Google Gemini 对话客户端。实现 Gemini 原生 API 协议</summary>
/// <remarks>
/// Gemini API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 URL 参数 key 传递，不使用 Authorization 请求头</item>
/// <item>请求路径包含模型名称：/v1/models/{model}:generateContent</item>
/// <item>消息结构使用 contents 数组，角色为 user/model（非 assistant）</item>
/// <item>system 指令通过独立的 systemInstruction 顶级字段传入</item>
/// <item>流式接口路径为 :streamGenerateContent?alt=sse</item>
/// </list>
/// </remarks>
/// <remarks>用连接选项初始化 Gemini 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
[AiClient("Gemini", "谷歌Gemini", "https://generativelanguage.googleapis.com", Protocol = "Gemini", Description = "谷歌 Gemini 系列多模态大模型，支持超长上下文")]
[AiClientModel("gemini-2.5-pro", "Gemini 2.5 Pro", Thinking = true, Vision = true)]
[AiClientModel("gemini-2.5-flash", "Gemini 2.5 Flash", Thinking = true, Vision = true)]
[AiClientModel("imagen-3.0-generate-001", "Imagen 3", ImageGeneration = true, FunctionCalling = false)]
public class GeminiChatClient(AiClientOptions options) : AiClientBase(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "谷歌Gemini";
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建 Gemini 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public GeminiChatClient(String apiKey, String? model = null, String? endpoint = null)
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

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseChunk(data, request, null);
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
        if (request.Stream)
            return $"{endpoint}/v1/models/{request.Model}:streamGenerateContent?alt=sse&key={_options.ApiKey}";
        else
            return $"{endpoint}/v1/models/{request.Model}:generateContent?key={_options.ApiKey}";
    }

    /// <summary>构建 Gemini 请求体</summary>
    protected override Object BuildRequest(IChatRequest request) => GeminiRequest.FromChatRequest(request);

    /// <summary>解析 Gemini 响应</summary>
    protected override IChatResponse ParseResponse(String data, IChatRequest request) => data.ToJsonEntity<GeminiResponse>()!.ToChatResponse(request.Model, request.Stream);

    #endregion
}
