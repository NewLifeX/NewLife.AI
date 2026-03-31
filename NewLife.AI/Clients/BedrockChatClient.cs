using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

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
/// <param name="options">连接选项</param>
[AiClient("Bedrock", "AWS Bedrock", "https://bedrock-runtime.us-east-1.amazonaws.com",
    Protocol = "Bedrock", Description = "Amazon Bedrock 托管模型服务，支持 Claude/Llama/Mistral 等", Order = 41)]
[AiClientModel("anthropic.claude-sonnet-4-20250514-v1:0", "Claude Sonnet 4 (Bedrock)", Code = "Bedrock", Vision = true, Thinking = true)]
[AiClientModel("anthropic.claude-haiku-4-20250514-v1:0", "Claude Haiku 4 (Bedrock)", Code = "Bedrock", Vision = true)]
[AiClientModel("meta.llama3-3-70b-instruct-v1:0", "Llama 3.3 70B (Bedrock)", Code = "Bedrock", FunctionCalling = true)]
[AiClientModel("mistral.mistral-large-2407-v1:0", "Mistral Large (Bedrock)", Code = "Bedrock", FunctionCalling = true)]
[AiClientModel("amazon.nova-pro-v1:0", "Amazon Nova Pro", Code = "Bedrock", Vision = true, FunctionCalling = true)]
public class BedrockChatClient(AiClientOptions options) : AiClientBase(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "Bedrock";

    /// <summary>AWS 区域。默认从 options.Protocol 读取，未设置时使用 us-east-1</summary>
    public String Region => _options.Protocol.IsNullOrEmpty() ? "us-east-1" : _options.Protocol;

    private const String ServiceName = "bedrock";
    #endregion

    #region 构造
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
    protected override async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    protected override String BuildUrl(ChatRequest request)
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
    protected override Object BuildRequest(ChatRequest request)
    {
        var dic = new Dictionary<String, Object>();

        // 构建 messages 数组
        var messages = new List<Object>();
        var systemTexts = new List<Object>();

        if (request.Messages != null)
        {
            foreach (var msg in request.Messages)
            {
                if (msg.Role == "system")
                {
                    // system 消息放到顶级 system 字段
                    var content = msg.Content as String;
                    if (!String.IsNullOrEmpty(content))
                        systemTexts.Add(new Dictionary<String, Object> { ["text"] = content });
                }
                else if (msg.Role == "tool")
                {
                    // 工具结果映射
                    var toolResultContent = new List<Object>
                    {
                        new Dictionary<String, Object> { ["text"] = msg.Content as String ?? "" }
                    };
                    var toolResult = new Dictionary<String, Object>
                    {
                        ["toolUseId"] = msg.ToolCallId ?? "",
                        ["content"] = toolResultContent,
                    };
                    messages.Add(new Dictionary<String, Object>
                    {
                        ["role"] = "user",
                        ["content"] = new List<Object>
                        {
                            new Dictionary<String, Object> { ["toolResult"] = toolResult }
                        }
                    });
                }
                else
                {
                    var contentBlocks = new List<Object>();

                    // 处理工具调用（assistant 消息中的 tool_calls）
                    if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                    {
                        foreach (var tc in msg.ToolCalls)
                        {
                            if (tc.Function == null) continue;
                            var toolUse = new Dictionary<String, Object>
                            {
                                ["toolUseId"] = tc.Id ?? "",
                                ["name"] = tc.Function.Name ?? "",
                            };

                            // 将 arguments JSON 字符串解析为对象
                            if (!String.IsNullOrEmpty(tc.Function.Arguments))
                            {
                                var args = JsonParser.Decode(tc.Function.Arguments);
                                if (args != null)
                                    toolUse["input"] = args;
                                else
                                    toolUse["input"] = new Dictionary<String, Object>();
                            }
                            else
                            {
                                toolUse["input"] = new Dictionary<String, Object>();
                            }

                            contentBlocks.Add(new Dictionary<String, Object> { ["toolUse"] = toolUse });
                        }
                    }

                    // 文本内容
                    var textContent = msg.Content as String;
                    if (!String.IsNullOrEmpty(textContent))
                        contentBlocks.Add(new Dictionary<String, Object> { ["text"] = textContent });

                    if (contentBlocks.Count > 0)
                    {
                        messages.Add(new Dictionary<String, Object>
                        {
                            ["role"] = msg.Role == "assistant" ? "assistant" : "user",
                            ["content"] = contentBlocks,
                        });
                    }
                }
            }
        }

        dic["messages"] = messages;

        if (systemTexts.Count > 0)
            dic["system"] = systemTexts;

        // 推理配置
        var inferenceConfig = new Dictionary<String, Object>();
        if (request.MaxTokens > 0)
            inferenceConfig["maxTokens"] = request.MaxTokens;
        if (request.Temperature != null)
            inferenceConfig["temperature"] = request.Temperature.Value;
        if (request.TopP != null)
            inferenceConfig["topP"] = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0)
            inferenceConfig["stopSequences"] = request.Stop;

        if (inferenceConfig.Count > 0)
            dic["inferenceConfig"] = inferenceConfig;

        // 工具配置
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var toolList = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var toolSpec = new Dictionary<String, Object>
                {
                    ["name"] = tool.Function.Name ?? "",
                    ["description"] = tool.Function.Description ?? "",
                };
                if (tool.Function.Parameters != null)
                    toolSpec["inputSchema"] = new Dictionary<String, Object>
                    {
                        ["json"] = tool.Function.Parameters
                    };

                toolList.Add(new Dictionary<String, Object> { ["toolSpec"] = toolSpec });
            }

            if (toolList.Count > 0)
                dic["toolConfig"] = new Dictionary<String, Object> { ["tools"] = toolList };
        }

        return dic;
    }

    /// <summary>解析 Bedrock Converse API 非流式响应</summary>
    protected override ChatResponse ParseResponse(String json, ChatRequest request)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null)
            return new ChatResponse { Model = request.Model };

        var response = new ChatResponse
        {
            Model = request.Model,
            Object = "chat.completion",
        };

        // 解析 output.message
        if (dic["output"] is IDictionary<String, Object> output &&
            output["message"] is IDictionary<String, Object> message)
        {
            var role = message["role"] as String ?? "assistant";
            var contentText = "";
            String? reasoning = null;
            List<ToolCall>? toolCalls = null;

            if (message["content"] is IList<Object> contentBlocks)
            {
                foreach (var block in contentBlocks)
                {
                    if (block is not IDictionary<String, Object> blockDic) continue;

                    if (blockDic.TryGetValue("text", out var textVal))
                        contentText += textVal as String;

                    if (blockDic.TryGetValue("reasoningContent", out var reasonVal) &&
                        reasonVal is IDictionary<String, Object> reasonDic &&
                        reasonDic.TryGetValue("reasoningText", out var reasonText))
                        reasoning = reasonText as String;

                    if (blockDic.TryGetValue("toolUse", out var toolVal) &&
                        toolVal is IDictionary<String, Object> toolDic)
                    {
                        toolCalls ??= [];
                        var tc = new ToolCall
                        {
                            Id = toolDic["toolUseId"] as String ?? "",
                            Type = "function",
                            Function = new FunctionCall
                            {
                                Name = toolDic["name"] as String ?? "",
                                Arguments = toolDic.TryGetValue("input", out var inputVal)
                                    ? JsonHost.Write(inputVal!) : "{}",
                            }
                        };
                        toolCalls.Add(tc);
                    }
                }
            }

            var chatMsg = new ChatMessage
            {
                Role = role,
                Content = contentText,
                ReasoningContent = reasoning,
                ToolCalls = toolCalls,
            };

            response.Messages =
            [
                new ChatChoice
                {
                    Message = chatMsg,
                    FinishReason = MapStopReason(dic["stopReason"] as String),
                }
            ];
        }

        // 解析 usage
        if (dic["usage"] is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["inputTokens"].ToInt(),
                OutputTokens = usageDic["outputTokens"].ToInt(),
            };
        }

        return response;
    }

    /// <summary>解析流式 chunk</summary>
    protected override ChatResponse? ParseChunk(String data, ChatRequest request, String? lastEvent)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatResponse
        {
            Model = request.Model,
            Object = "chat.completion.chunk",
        };

        // Bedrock ConverseStream 事件类型
        if (dic.TryGetValue("contentBlockDelta", out var deltaObj) &&
            deltaObj is IDictionary<String, Object> deltaDic &&
            deltaDic.TryGetValue("delta", out var deltaContent) &&
            deltaContent is IDictionary<String, Object> deltaContentDic)
        {
            if (deltaContentDic.TryGetValue("text", out var textVal))
            {
                response.AddDelta(textVal as String, null, null);
                return response;
            }
            if (deltaContentDic.TryGetValue("reasoningContent", out var reasonVal) &&
                reasonVal is IDictionary<String, Object> reasonDic &&
                reasonDic.TryGetValue("text", out var reasonText))
            {
                response.AddDelta(null, reasonText as String, null);
                return response;
            }
            if (deltaContentDic.TryGetValue("toolUse", out var toolVal) &&
                toolVal is IDictionary<String, Object> toolDic)
            {
                var tc = new ToolCall
                {
                    Id = toolDic["toolUseId"] as String ?? "",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = toolDic["name"] as String ?? "",
                        Arguments = toolDic.TryGetValue("input", out var inputVal)
                            ? inputVal as String ?? "" : "",
                    }
                };
                response.AddDelta(null, null, null);
                if (response.Messages?.Count > 0)
                    response.Messages[0].Message ??= new ChatMessage { Role = "assistant", ToolCalls = [tc] };
                return response;
            }
        }

        // contentBlockStart — 工具调用开始
        if (dic.TryGetValue("contentBlockStart", out var startObj) &&
            startObj is IDictionary<String, Object> startDic &&
            startDic.TryGetValue("start", out var startContent) &&
            startContent is IDictionary<String, Object> startContentDic &&
            startContentDic.TryGetValue("toolUse", out var toolStartVal) &&
            toolStartVal is IDictionary<String, Object> toolStartDic)
        {
            var tc = new ToolCall
            {
                Id = toolStartDic["toolUseId"] as String ?? "",
                Type = "function",
                Function = new FunctionCall
                {
                    Name = toolStartDic["name"] as String ?? "",
                }
            };
            response.AddDelta(null, null, null);
            if (response.Messages?.Count > 0)
                response.Messages[0].Message ??= new ChatMessage { Role = "assistant", ToolCalls = [tc] };
            return response;
        }

        // messageStop — 包含 stopReason
        if (dic.TryGetValue("messageStop", out var stopObj) &&
            stopObj is IDictionary<String, Object> stopDic)
        {
            var stopReason = stopDic["stopReason"] as String;
            response.AddDelta(null, null, MapStopReason(stopReason));
            return response;
        }

        // metadata — 包含 usage
        if (dic.TryGetValue("metadata", out var metaObj) &&
            metaObj is IDictionary<String, Object> metaDic &&
            metaDic.TryGetValue("usage", out var usageObj) &&
            usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["inputTokens"].ToInt(),
                OutputTokens = usageDic["outputTokens"].ToInt(),
            };
            response.AddDelta(null, null, null);
            return response;
        }

        return null;
    }

    /// <summary>设置请求头。使用 AWS SigV4 签名认证</summary>
    protected override void SetHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options)
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

    /// <summary>映射 Bedrock 停止原因到 OpenAI FinishReason</summary>
    internal static String? MapStopReason(String? stopReason)
    {
        return stopReason switch
        {
            "end_turn" => "stop",
            "stop_sequence" => "stop",
            "max_tokens" => "length",
            "tool_use" => "tool_calls",
            "content_filtered" => "content_filter",
            _ => stopReason,
        };
    }
    #endregion
}
