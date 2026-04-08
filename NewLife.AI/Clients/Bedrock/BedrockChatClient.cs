using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Bedrock;

/// <summary>AWS Bedrock 对话客户端。实现 Amazon Bedrock Converse API 原生协议</summary>
/// <remarks>
/// Amazon Bedrock 的主要特点：
/// <list type="bullet">
/// <item>使用 AWS SigV4 签名认证，无需 Bearer Token</item>
/// <item>URL 格式：https://bedrock-runtime.{region}.amazonaws.com/model/{modelId}/converse</item>
/// <item>请求/响应格式与 OpenAI 不同，使用 Bedrock Converse API 格式</item>
/// <item>支持 Claude、Llama、Mistral 等多种底座模型</item>
/// </list>
/// 凭证通过 AiClientOptions 传递：ApiKey=AccessKeyId, Organization=SecretAccessKey。
/// 区域通过 AiClientOptions.Protocol 字段传递，默认 us-east-1。
/// </remarks>
[AiClient("Bedrock", "AWS Bedrock", "https://bedrock-runtime.us-east-1.amazonaws.com",
    Protocol = "Bedrock", Description = "Amazon Bedrock 托管模型服务，支持 Claude/Llama/Mistral 等", Order = 41)]
[AiClientModel("anthropic.claude-sonnet-4-20250514-v1:0", "Claude Sonnet 4 (Bedrock)", Code = "Bedrock", Vision = true, Thinking = true)]
[AiClientModel("anthropic.claude-haiku-4-20250514-v1:0", "Claude Haiku 4 (Bedrock)", Code = "Bedrock", Vision = true)]
[AiClientModel("meta.llama3-3-70b-instruct-v1:0", "Llama 3.3 70B (Bedrock)", Code = "Bedrock", FunctionCalling = true)]
[AiClientModel("mistral.mistral-large-2407-v1:0", "Mistral Large (Bedrock)", Code = "Bedrock", FunctionCalling = true)]
[AiClientModel("amazon.nova-pro-v1:0", "Amazon Nova Pro", Code = "Bedrock", Vision = true, FunctionCalling = true)]
public class BedrockChatClient : AiClientBase
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "Bedrock";

    /// <summary>AWS 区域。默认从 options.Protocol 读取，未设置时使用 us-east-1</summary>
    public String Region => _options.Protocol.IsNullOrEmpty() ? "us-east-1" : _options.Protocol;

    private const String ServiceName = "bedrock";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.CamelCase,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项</param>
    public BedrockChatClient(AiClientOptions options) : base(options) => JsonOptions = DefaultJsonOptions;

    /// <summary>以 AWS 凭证快速创建 Bedrock 客户端</summary>
    /// <param name="accessKeyId">AWS Access Key ID</param>
    /// <param name="secretAccessKey">AWS Secret Access Key</param>
    /// <param name="model">默认模型 ID，如 anthropic.claude-sonnet-4-20250514-v1:0</param>
    /// <param name="region">AWS 区域，默认 us-east-1</param>
    public BedrockChatClient(String accessKeyId, String secretAccessKey, String? model = null, String? region = null)
        : this(new AiClientOptions { ApiKey = accessKeyId, Organization = secretAccessKey, Model = model, Protocol = region ?? "us-east-1" }) { }
    #endregion

    #region 核心方法
    /// <summary>流式对话</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    /// <summary>构建请求地址</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var endpoint = GetRegionEndpoint();
        var model = request.Model ?? _options.Model;
        if (request.Stream)
            return $"{endpoint}/model/{Uri.EscapeDataString(model!)}/converse-stream";
        else
            return $"{endpoint}/model/{Uri.EscapeDataString(model!)}/converse";
    }

    /// <summary>获取区域化的 Bedrock 端点</summary>
    private String GetRegionEndpoint()
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint);
        if (!String.IsNullOrEmpty(endpoint) && !endpoint.Contains("us-east-1"))
            return endpoint.TrimEnd('/');

        return $"https://bedrock-runtime.{Region}.amazonaws.com";
    }

    /// <summary>构建 Bedrock Converse API 请求体</summary>
    protected override Object BuildRequest(IChatRequest request)
    {
        if (request is BedrockRequest br) return br;
        return BedrockRequest.FromChatRequest(request);
    }

    /// <summary>解析 Bedrock Converse API 非流式响应</summary>
    protected override IChatResponse ParseResponse(String json, IChatRequest request)
    {
        var bedrockResp = json.ToJsonEntity<BedrockResponse>(JsonOptions);
        if (bedrockResp == null) return new ChatResponse { Model = request.Model, Object = "chat.completion" };
        bedrockResp.Model ??= request.Model;
        if (String.IsNullOrEmpty(((IChatResponse)bedrockResp).Object)) ((IChatResponse)bedrockResp).Object = "chat.completion";
        return bedrockResp;
    }

    /// <summary>解析流式 chunk</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
        => data.ToJsonEntity<BedrockStreamEvent>(JsonOptions)?.ToChunkResponse(request.Model);

    /// <summary>设置请求头。使用 AWS SigV4 签名认证</summary>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        var accessKey = options.ApiKey;
        var secretKey = options.Organization;

        if (String.IsNullOrEmpty(accessKey) || String.IsNullOrEmpty(secretKey))
            return;

        // 读取请求体用于签名
        var payload = "";
        if (request.Content != null)
            payload = request.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        var uri = request.RequestUri!;
        var headers = new Dictionary<String, String>
        {
            ["host"] = uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port),
            ["content-type"] = "application/json",
        };

        var result = AwsSigV4Signer.Sign(
            request.Method.Method,
            uri,
            headers,
            payload,
            accessKey,
            secretKey,
            Region,
            ServiceName);

        request.Headers.TryAddWithoutValidation("Authorization", result.Authorization);
        request.Headers.TryAddWithoutValidation("X-Amz-Date", result.Timestamp);
        request.Headers.TryAddWithoutValidation("X-Amz-Content-Sha256", result.ContentHash);
    }
    #endregion
}
