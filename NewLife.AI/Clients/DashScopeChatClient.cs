using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>阿里百炼（DashScope）对话客户端。支持 DashScope 原生协议与 OpenAI 兼容协议双模式</summary>
/// <remarks>
/// 通过 <see cref="AiClientOptions.Protocol"/> 控制协议模式：
/// <list type="bullet">
/// <item>"DashScope"（默认/空）：使用阿里云 DashScope 原生协议，走 /api/v1 端点</item>
/// <item>"ChatCompletions"：使用 OpenAI 兼容协议，走 /compatible-mode 端点，复用基类逻辑</item>
/// </list>
/// 官方文档：https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope
/// </remarks>
/// <remarks>用连接选项初始化 DashScope 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model、Protocol 等）</param>
/// <param name="httpClient">外部管理的 HttpClient，传 null 时自动创建</param>
[AiClient("DashScope", "阿里百炼", "https://dashscope.aliyuncs.com/api/v1", Protocol = "DashScope", Description = "阿里云百炼大模型平台，支持 Qwen/通义千问全系列商业版模型")]
[AiClientModel("qwen3-max", "Qwen3 Max", Thinking = true)]
[AiClientModel("qwen3.5-plus", "Qwen3.5 Plus", Thinking = true, Vision = true)]
[AiClientModel("qwen3.5-flash", "Qwen3.5 Flash", Vision = true)]
[AiClientModel("qwq-plus", "QwQ Plus", Thinking = true)]
public class DashScopeChatClient(AiClientOptions options, HttpClient? httpClient = null) : OpenAiChatClient(options, httpClient)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "阿里百炼";

    /// <summary>原生 DashScope API 基础地址（/api/v1）</summary>
    protected virtual String NativeEndpoint => "https://dashscope.aliyuncs.com/api/v1";

    /// <summary>兼容模式基础地址。Embedding、重排序等沿用此端点</summary>
    protected virtual String CompatibleEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <inheritdoc/>
    public override String DefaultEndpoint
    {
        get => IsNativeProtocol ? NativeEndpoint : CompatibleEndpoint;
        set => base.DefaultEndpoint = value;
    }

    /// <summary>是否使用 DashScope 原生协议。Protocol 为空或 "DashScope" 时为原生模式</summary>
    protected Boolean IsNativeProtocol =>
        String.IsNullOrEmpty(_options.Protocol) || _options.Protocol == "DashScope";
    #endregion

    #region 对话（重写）
    /// <summary>非流式对话。原生协议走 DashScope 格式，兼容模式委托基类</summary>
    protected override async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
            return await base.ChatAsync(request, cancellationToken).ConfigureAwait(false);

        var url = BuildChatUrl(_options);
        var isMultimodal = IsMultimodalModel(_options.Model);
        var body = BuildDashScopeRequestBody(request, isMultimodal, false);
        var json = await PostAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        var response = ParseDashScopeResponse(json);
        // 原生响应无顶层 model 字段，从请求回填
        response.Model ??= request.Model;
        return response;
    }

    /// <summary>流式对话。原生协议走 DashScope SSE 格式，兼容模式委托基类</summary>
    protected override async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
        {
            await foreach (var chunk in base.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var url = BuildChatUrl(_options);
        var isMultimodal = IsMultimodalModel(_options.Model);
        var body = BuildDashScopeRequestBody(request, isMultimodal, true);

        using var httpResponse = await PostStreamAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (line.StartsWith("id:")) continue;

            if (line.StartsWith("event:"))
            {
                lastEvent = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            if (lastEvent == "error")
            {
                var errDic = JsonParser.Decode(data);
                var code = errDic?["code"] as String ?? "error";
                var message = errDic?["message"] as String ?? data;
                throw new HttpRequestException($"[{Name}] 流式错误 {code}: {message}");
            }

            ChatResponse? chunk = null;
            try { chunk = ParseDashScopeStreamChunk(data); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }
    #endregion

    #region 模型列表
    /// <summary>获取可用模型列表。使用兼容模式端点以保证返回完整模型目录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public override async Task<OpenAiModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/models";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new OpenAiModelListResponse { Object = dic["object"] as String };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<OpenAiModelObject>(dataList.Count);
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new OpenAiModelObject
                {
                    Id = d["id"] as String,
                    Object = d["object"] as String,
                    OwnedBy = d["owned_by"] as String,
                    Created = d["created"].ToLong().ToDateTime(),
                });
            }
            response.Data = [.. items];
        }
        return response;
    }
    #endregion

    #region 重排序（Rerank）
    /// <summary>文档重排序。对 RAG 检索召回的候选文档按语义相关度重新排序</summary>
    /// <param name="request">重排序请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重排序响应</returns>
    public async Task<RerankResponse> RerankAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        var url = CompatibleEndpoint.TrimEnd('/') + "/v1/reranks";
        var body = new Dictionary<String, Object?>
        {
            ["model"] = !String.IsNullOrEmpty(request.Model) ? request.Model : "gte-rerank-v2",
            ["input"] = new Dictionary<String, Object> { ["query"] = request.Query, ["documents"] = request.Documents },
            ["parameters"] = BuildRerankParameters(request),
        };
        var json = await PostAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        return ParseRerankResponse(json);
    }

    private static Dictionary<String, Object> BuildRerankParameters(RerankRequest request)
    {
        var p = new Dictionary<String, Object> { ["return_documents"] = request.ReturnDocuments };
        if (request.TopN != null) p["top_n"] = request.TopN.Value;
        return p;
    }

    private static RerankResponse ParseRerankResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析重排序响应");

        var resp = new RerankResponse { RequestId = dic["request_id"] as String };

        if (dic["output"] is IDictionary<String, Object> output &&
            output["results"] is IList<Object> resultList)
        {
            var results = new List<RerankResult>(resultList.Count);
            foreach (var item in resultList)
            {
                if (item is not IDictionary<String, Object> r) continue;
                var result = new RerankResult
                {
                    Index = r["index"].ToInt(),
                    RelevanceScore = r["relevance_score"].ToDouble(),
                };
                var docVal = r["document"];
                if (docVal != null)
                    result.Document = docVal is IDictionary<String, Object> docDic
                        ? docDic["text"] as String
                        : docVal as String;
                results.Add(result);
            }
            resp.Results = results;
        }

        if (dic["usage"] is IDictionary<String, Object> usage)
            resp.Usage = new RerankUsage { TotalTokens = usage["total_tokens"].ToInt() };

        return resp;
    }
    #endregion

    #region 辅助
    // 原生对话路径（纯文本）
    private const String ChatGenerationPath = "/services/aigc/text-generation/generation";

    // 原生对话路径（多模态：含视觉/音频/视频输入）
    private const String MultimodalGenerationPath = "/services/aigc/multimodal-generation/generation";

    /// <summary>构建 DashScope 原生对话 URL。根据模型特性选择 text-generation 或 multimodal-generation 路径</summary>
    private String BuildChatUrl(AiClientOptions options)
    {
        var path = IsMultimodalModel(options.Model) ? MultimodalGenerationPath : ChatGenerationPath;

        // 原生协议只能对接 /api/v1 端点；若用户配置了兼容模式地址则忽略并回退到原生端点
        var endpoint = options.Endpoint;
        if (String.IsNullOrWhiteSpace(endpoint) ||
            endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
            endpoint = NativeEndpoint;
        return endpoint.TrimEnd('/') + path;
    }

    /// <summary>判断指定模型是否为多模态模型（需走 multimodal-generation 端点）</summary>
    /// <remarks>
    /// 命名规律：
    /// <list type="bullet">
    /// <item>含 -vl：Vision-Language 系列</item>
    /// <item>qvq- 前缀：视觉推理系列（区别于纯文本推理 qwq-）</item>
    /// <item>qwen3.5- 前缀：内置多模态能力</item>
    /// </list>
    /// </remarks>
    private static Boolean IsMultimodalModel(String? model)
    {
        if (String.IsNullOrEmpty(model)) return false;
        if (model.IndexOf("-vl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (model.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase)) return true;
        if (model.StartsWith("qwen3.5-", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>构建 DashScope 原生请求体</summary>
    private static Object BuildDashScopeRequestBody(ChatRequest request, Boolean isMultimodal, Boolean stream)
    {
        var messages = BuildDashScopeMessages(request.Messages, isMultimodal);

        var parameters = new Dictionary<String, Object> { ["result_format"] = "message" };
        if (request.Temperature != null) parameters["temperature"] = request.Temperature.Value;
        if (request.TopP != null) parameters["top_p"] = request.TopP.Value;
        if (request.TopK != null) parameters["top_k"] = request.TopK.Value;
        if (request.MaxTokens != null) parameters["max_tokens"] = request.MaxTokens.Value;
        if (request.Stop != null && request.Stop.Count > 0) parameters["stop"] = request.Stop;
        if (request.PresencePenalty != null) parameters["presence_penalty"] = request.PresencePenalty.Value;
        if (request.FrequencyPenalty != null) parameters["frequency_penalty"] = request.FrequencyPenalty.Value;
        if (request.EnableThinking != null) parameters["enable_thinking"] = request.EnableThinking.Value;
        if (request.ResponseFormat != null) parameters["response_format"] = request.ResponseFormat;
        if (request.Tools != null && request.Tools.Count > 0)
            parameters["tools"] = BuildDashScopeTools(request.Tools);
        if (request.ToolChoice != null) parameters["tool_choice"] = request.ToolChoice;
        if (request.ParallelToolCalls != null) parameters["parallel_tool_calls"] = request.ParallelToolCalls.Value;

        ApplyDashScopeItems(parameters, request);

        if (stream)
        {
            parameters["stream"] = true;
            parameters["incremental_output"] = true;
        }
        else
        {
            // 显式传 stream=false，避免多模态端点默认进入流式模式
            parameters["stream"] = false;
        }

        return new Dictionary<String, Object>
        {
            ["model"] = request.Model ?? "",
            ["input"] = new Dictionary<String, Object> { ["messages"] = messages },
            ["parameters"] = parameters,
        };
    }

    /// <summary>从 request 扩展项读取 DashScope 专属参数</summary>
    private static void ApplyDashScopeItems(Dictionary<String, Object> parameters, ChatRequest request)
    {
        var seed = request["Seed"] as Int32?;
        if (seed != null) parameters["seed"] = seed.Value;
        var repetitionPenalty = request["RepetitionPenalty"] as Double?;
        if (repetitionPenalty != null) parameters["repetition_penalty"] = repetitionPenalty.Value;
        var n = request["N"] as Int32?;
        if (n != null) parameters["n"] = n.Value;
        var thinkingBudget = request["ThinkingBudget"] as Int32?;
        if (thinkingBudget != null) parameters["thinking_budget"] = thinkingBudget.Value;
        var enableCodeInterpreter = request["EnableCodeInterpreter"] as Boolean?;
        if (enableCodeInterpreter != null) parameters["enable_code_interpreter"] = enableCodeInterpreter.Value;
        var logprobs = request["Logprobs"] as Boolean?;
        if (logprobs != null) parameters["logprobs"] = logprobs.Value;
        var topLogprobs = request["TopLogprobs"] as Int32?;
        if (topLogprobs != null) parameters["top_logprobs"] = topLogprobs.Value;
        var enableSearch = request["EnableSearch"] as Boolean?;
        if (enableSearch != null) parameters["enable_search"] = enableSearch.Value;

        var searchOptions = new Dictionary<String, Object>();
        var searchStrategy = request["SearchStrategy"] as String;
        if (!String.IsNullOrEmpty(searchStrategy)) searchOptions["search_strategy"] = searchStrategy;
        var enableSource = request["EnableSource"] as Boolean?;
        if (enableSource != null) searchOptions["enable_source"] = enableSource.Value;
        var forcedSearch = request["ForcedSearch"] as Boolean?;
        if (forcedSearch != null) searchOptions["forced_search"] = forcedSearch.Value;
        if (searchOptions.Count > 0) parameters["search_options"] = searchOptions;
    }

    /// <summary>构建原生协议 messages 数组</summary>
    private static IList<Object> BuildDashScopeMessages(IList<ChatMessage> messages, Boolean isMultimodal)
    {
        var result = new List<Object>(messages.Count);
        foreach (var msg in messages)
        {
            var m = new Dictionary<String, Object?> { ["role"] = msg.Role };

            if (msg.Contents != null && msg.Contents.Count > 0)
                m["content"] = BuildDashScopeContent(msg.Contents, isMultimodal);
            else if (isMultimodal)
                m["content"] = new List<Object> { new { text = msg.Content ?? "" } };
            else
                m["content"] = msg.Content;

            if (msg.Name != null) m["name"] = msg.Name;
            if (msg.ToolCallId != null) m["tool_call_id"] = msg.ToolCallId;

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<Object>(msg.ToolCalls.Count);
                foreach (var tc in msg.ToolCalls)
                {
                    var tcDic = new Dictionary<String, Object?> { ["id"] = tc.Id, ["type"] = tc.Type };
                    if (tc.Function != null)
                    {
                        tcDic["function"] = new Dictionary<String, Object?>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = String.IsNullOrEmpty(tc.Function.Arguments) ? "{}" : tc.Function.Arguments,
                        };
                    }
                    toolCalls.Add(tcDic);
                }
                m["tool_calls"] = toolCalls;
            }

            result.Add(m);
        }
        return result;
    }

    /// <summary>构建多模态内容数组（DashScope 原生格式）</summary>
    private static Object BuildDashScopeContent(IList<AIContent> contents, Boolean isMultimodal)
    {
        if (!isMultimodal && contents.Count == 1 && contents[0] is TextContent singleText)
            return singleText.Text;

        var parts = new List<Object>(contents.Count);
        foreach (var item in contents)
        {
            if (item is TextContent text)
            {
                parts.Add(isMultimodal
                    ? (Object)new { text = text.Text }
                    : new { type = "text", text = text.Text });
            }
            else if (item is ImageContent img)
            {
                String url;
                if (img.Data != null && img.Data.Length > 0)
                    url = $"data:{img.MediaType ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                else
                    url = img.Uri ?? "";

                parts.Add(isMultimodal
                    ? (Object)new { image = url }
                    : new { type = "image_url", image_url = new { url } });
            }
        }
        return parts;
    }

    /// <summary>构建原生协议 tools 参数数组，支持 function/mcp/web_search/code_interpreter</summary>
    private static IList<Object> BuildDashScopeTools(IList<ChatTool> tools)
    {
        var result = new List<Object>(tools.Count);
        foreach (var tool in tools)
        {
            var t = new Dictionary<String, Object?> { ["type"] = tool.Type };

            if (tool.Type == "function" && tool.Function != null)
            {
                var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                t["function"] = fn;
            }
            else if (tool.Type == "mcp" && tool.Mcp != null)
            {
                var mcp = new Dictionary<String, Object>();
                if (tool.Mcp.ServerUrl != null) mcp["server_url"] = tool.Mcp.ServerUrl;
                if (tool.Mcp.ServerId != null) mcp["server_id"] = tool.Mcp.ServerId;
                if (tool.Mcp.AllowedTools != null) mcp["allowed_tools"] = tool.Mcp.AllowedTools;
                if (tool.Mcp.Authorization != null)
                {
                    mcp["authorization"] = new Dictionary<String, Object?>
                    {
                        ["type"] = tool.Mcp.Authorization.Type,
                        ["token"] = tool.Mcp.Authorization.Token,
                    };
                }
                t["mcp"] = mcp;
            }
            else if (tool.Config != null)
                t[tool.Type ?? "config"] = tool.Config;

            result.Add(t);
        }
        return result;
    }

    /// <summary>解析 DashScope 原生非流式响应</summary>
    private ChatResponse ParseDashScopeResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 DashScope 响应");

        var errCode = dic["code"] as String;
        var errMsg = dic["message"] as String;
        if (!String.IsNullOrEmpty(errCode))
            throw new HttpRequestException($"[{Name}] 错误 {errCode}: {errMsg}");

        var response = new ChatResponse
        {
            Object = "chat.completion",
            Id = dic["request_id"] as String,
        };

        if (dic["output"] is IDictionary<String, Object> output &&
            output["choices"] is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>(choicesList.Count);
            for (var i = 0; i < choicesList.Count; i++)
            {
                if (choicesList[i] is not IDictionary<String, Object> choiceDic) continue;
                choices.Add(new DashScopeChoice
                {
                    Index = i,
                    FinishReason = choiceDic["finish_reason"] as String,
                    Message = ParseChatMessage(choiceDic["message"] as IDictionary<String, Object>),
                    Logprobs = choiceDic.TryGetValue("logprobs", out var lp) ? lp : null,
                });
            }
            response.Messages = choices;
        }

        if (dic["usage"] is IDictionary<String, Object> usageDic)
            response.Usage = ParseDashScopeUsage(usageDic);

        return response;
    }

    /// <summary>解析 DashScope 原生流式 SSE chunk</summary>
    private ChatResponse? ParseDashScopeStreamChunk(String data)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatResponse
        {
            Object = "chat.completion.chunk",
            Id = dic["request_id"] as String,
        };

        if (dic["output"] is IDictionary<String, Object> output &&
            output["choices"] is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>(choicesList.Count);
            for (var i = 0; i < choicesList.Count; i++)
            {
                if (choicesList[i] is not IDictionary<String, Object> choiceDic) continue;
                var choice = new DashScopeChoice
                {
                    Index = i,
                    FinishReason = choiceDic["finish_reason"] as String,
                };

                // incremental_output=true 时，delta/message 含增量文本
                IDictionary<String, Object>? incrementalField = null;
                if (choiceDic["delta"] is IDictionary<String, Object> dd)
                    incrementalField = dd;
                else if (choiceDic["message"] is IDictionary<String, Object> md)
                    incrementalField = md;
                choice.Delta = ParseChatMessage(incrementalField);

                if (choiceDic.TryGetValue("logprobs", out var lp)) choice.Logprobs = lp;
                choices.Add(choice);
            }
            response.Messages = choices;
        }

        if (dic["usage"] is IDictionary<String, Object> usageDic)
            response.Usage = ParseDashScopeUsage(usageDic);

        return response;
    }

    /// <summary>解析 DashScope 原生用量统计</summary>
    private static DashScopeUsage ParseDashScopeUsage(IDictionary<String, Object> usageDic) => new()
    {
        InputTokens = usageDic["input_tokens"].ToInt(),
        OutputTokens = usageDic["output_tokens"].ToInt(),
        TotalTokens = usageDic["total_tokens"].ToInt(),
        ImageTokens = usageDic.TryGetValue("image_tokens", out var img) ? img.ToInt() : 0,
        VideoTokens = usageDic.TryGetValue("video_tokens", out var vid) ? vid.ToInt() : 0,
        AudioTokens = usageDic.TryGetValue("audio_tokens", out var aud) ? aud.ToInt() : 0,
    };

    /// <inheritdoc/>
    /// <remarks>多模态响应中 content 为数组格式（[{"text":"..."}]），归一化为字符串</remarks>
    protected override void OnParseChatMessage(ChatMessage msg, IDictionary<String, Object> dic)
    {
        if (msg.Content is not IList<Object> contentList) return;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in contentList)
        {
            if (item is IDictionary<String, Object> d && d["text"] is String t)
                sb.Append(t);
        }
        msg.Content = sb.Return(true);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 原生协议对聊天路径按模型类型注入 SSE 相关头：多模态模型走 Accept: text/event-stream，
    /// 文本模型走 X-DashScope-SSE: enable。非聊天路径（rerank/models 等）只注入 Bearer 认证。
    /// </remarks>
    protected override void SetHeaders(HttpRequestMessage request, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!IsNativeProtocol) return;

        var path = request.RequestUri?.AbsolutePath;
        if (String.IsNullOrEmpty(path)) return;

        if (!path.EndsWith(ChatGenerationPath, StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(MultimodalGenerationPath, StringComparison.OrdinalIgnoreCase)) return;

        if (IsMultimodalModel(options.Model))
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        else
            request.Headers.TryAddWithoutValidation("X-DashScope-SSE", "enable");
    }
    #endregion
}

/// <summary>DashScope 专属对话选项。扩展 <see cref="ChatOptions"/> 支持 DashScope 高级参数</summary>
/// <remarks>
/// 通过 <see cref="DashScopeChatClient"/> 创建对话时可传入此选项以设置 DashScope 专属参数。
/// <code>
/// var opts = new DashScopeChatOptions { EnableSearch = true, ThinkingBudget = -1 };
/// var response = await client.GetResponseAsync(request);
/// </code>
/// </remarks>
public class DashScopeChatOptions : ChatOptions
{
    /// <summary>随机种子。固定种子可在相同参数下复现输出，范围 0~2^31-1</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚。大于 1 则抑制已出现的 Token，小于 1 则鼓励重复，默认 1.1</summary>
    public Double? RepetitionPenalty { get; set; }

    /// <summary>返回候选数量。同一输入独立生成 N 条不同输出，默认 1</summary>
    public Int32? N { get; set; }

    /// <summary>思考预算（Token 数）。0=关闭深度思考，-1=不限制，仅思考模型（QwQ/Qwen3）有效</summary>
    public Int32? ThinkingBudget { get; set; }

    /// <summary>是否启用代码解释器。qwen3.5 及思考模式模型可在对话中执行代码</summary>
    public Boolean? EnableCodeInterpreter { get; set; }

    /// <summary>是否返回对数概率。开启后响应携带每个输出 Token 的 logprob 信息</summary>
    public Boolean? Logprobs { get; set; }

    /// <summary>返回对数概率的 top-K Token 数。需同时设置 Logprobs=true，范围 0~20</summary>
    public Int32? TopLogprobs { get; set; }

    /// <summary>是否启用高分辨率图像（VL 专属）。开启后 VL 模型以更高分辨率理解图像细节</summary>
    public Boolean? VlHighResolutionImages { get; set; }

    /// <summary>是否在响应中输出图像宽高（VL 专属）</summary>
    public Boolean? VlEnableImageHwOutput { get; set; }

    /// <summary>图像最大像素数（VL 专属）。限制输入图像分辨率以控制 Token 消耗</summary>
    public Int32? MaxPixels { get; set; }

    /// <summary>是否启用联网搜索。开启后模型可实时检索互联网信息以增强回复</summary>
    public Boolean? EnableSearch { get; set; }

    /// <summary>搜索策略。intelligent（智能，默认）/ force（每次强制搜索）/ prohibited（禁止搜索）</summary>
    public String? SearchStrategy { get; set; }

    /// <summary>是否在回复中展示来源引用链接</summary>
    public Boolean? EnableSource { get; set; }

    /// <summary>是否强制搜索，即使模型判断无需搜索时仍执行</summary>
    public Boolean? ForcedSearch { get; set; }
}

/// <summary>DashScope 专属选择项。继承 <see cref="ChatChoice"/> 并扩展 logprobs 字段</summary>
public class DashScopeChoice : ChatChoice
{
    /// <summary>对数概率信息。当请求参数 logprobs=true 时返回，包含输出 Token 的概率分布</summary>
    public Object? Logprobs { get; set; }
}

/// <summary>DashScope 专属用量统计。继承 <see cref="UsageDetails"/> 并扩展多模态 Token 字段</summary>
public class DashScopeUsage : UsageDetails
{
    /// <summary>图像 Token 数。多模态请求中图像输入消耗的 Token 数</summary>
    public Int32 ImageTokens { get; set; }

    /// <summary>视频 Token 数。多模态请求中视频输入消耗的 Token 数</summary>
    public Int32 VideoTokens { get; set; }

    /// <summary>音频 Token 数。多模态请求中音频输入消耗的 Token 数</summary>
    public Int32 AudioTokens { get; set; }
}



