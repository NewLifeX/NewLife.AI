using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

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
[AiClient("DashScope", "阿里百炼", "https://dashscope.aliyuncs.com/api/v1", Protocol = "DashScope", Description = "阿里云百炼大模型平台，支持 Qwen/通义千问全系列商业版模型")]
public partial class DashScopeChatClient : OpenAIChatClient, IRerankClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "阿里百炼";

    /// <summary>原生 API 路径前缀（/api/v1）</summary>
    protected virtual String NativePath => "/api/v1";

    /// <summary>兼容模式 API 路径前缀（/compatible-mode）。Embedding、重排序等沿用此路径</summary>
    protected virtual String CompatiblePath => "/compatible-mode";

    /// <summary>从配置地址中提取 scheme+host，用于构建完整 API 地址</summary>
    /// <remarks>
    /// 优先使用显式配置的 Endpoint；未配置时，若已设置 Organization（百炼业务空间 ID），
    /// 使用 MaaS 专属域名 https://{Organization}.cn-beijing.maas.aliyuncs.com；
    /// 否则回退到公共域名 https://dashscope.aliyuncs.com。
    /// Qwen-TTS 系列模型必须使用 MaaS 专属域名才能正常调用。
    /// </remarks>
    private String GetHost()
    {
        var endpoint = _options.Endpoint;
        if (!endpoint.IsNullOrWhiteSpace() && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

        // 已配置业务空间 ID 时使用 MaaS 专属域名（Qwen-TTS 必须，CosyVoice 亦适用）
        if (!_options.Organization.IsNullOrEmpty())
            return $"https://{_options.Organization}.cn-beijing.maas.aliyuncs.com";

        return "https://dashscope.aliyuncs.com";
    }

    /// <summary>获取原生协议完整基础地址</summary>
    private String GetNativeBaseUrl() => GetHost() + NativePath;

    /// <summary>获取兼容模式完整基础地址</summary>
    private String GetCompatibleBaseUrl() => GetHost() + CompatiblePath;

    /// <inheritdoc/>
    public override String DefaultEndpoint
    {
        get => IsNativeProtocol ? GetNativeBaseUrl() : GetCompatibleBaseUrl();
        set => base.DefaultEndpoint = value;
    }

    /// <summary>是否使用 DashScope 原生协议。Protocol 为空或 "DashScope" 时为原生模式</summary>
    protected Boolean IsNativeProtocol => _options.Protocol.IsNullOrEmpty() || _options.Protocol == "DashScope";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DashScopeDefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.SnakeCaseLower,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model、Protocol 等）</param>
    public DashScopeChatClient(AiClientOptions options) : base(options) => JsonOptions = DashScopeDefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建阿里百炼客户端</summary>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public DashScopeChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 对话（重写）
    /// <summary>判断是否为 Omni 全模态非实时模型。Omni 模型强制走兼容模式接口，stream=true 为必填</summary>
    /// <param name="model">模型标识</param>
    /// <returns>是则返回 true</returns>
    private static Boolean IsOmniModel(String? model)
    {
        if (model.IsNullOrEmpty()) return false;
        return model.Contains("-omni", StringComparison.OrdinalIgnoreCase) &&
               !model.Contains("-realtime", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>判断是否为 qwen3.5-omni 系列（支持联网搜索）</summary>
    /// <param name="model">模型标识</param>
    /// <returns>是则返回 true</returns>
    private static Boolean IsQwen35OmniModel(String? model) =>
        !model.IsNullOrEmpty() && model!.StartsWith("qwen3.5-omni", StringComparison.OrdinalIgnoreCase);

    /// <summary>构建兼容模式请求体。在标准 OpenAI 请求体基础上注入 DashScope 专属字段（联网搜索、内置工具等）</summary>
    /// <param name="request">统一请求</param>
    /// <returns>可直接序列化的请求字典</returns>
    protected override Object BuildRequest(IChatRequest request)
    {
        AutoDetectSearchIntent(request);
        var dic = ChatCompletionRequest.BuildBody(request);
        AppendDashScopeFields(dic, request);
        return dic;
    }

    /// <summary>注入 DashScope 兼容模式专属字段：联网搜索、图文混合输出、web_extractor / code_interpreter 内置工具</summary>
    /// <param name="dic">已构建的请求字典</param>
    /// <param name="request">统一请求</param>
    private static void AppendDashScopeFields(IDictionary<String, Object> dic, IChatRequest request)
    {
        // ===== 联网搜索 =====
        var enableSearch = request["EnableSearch"];
        if (enableSearch != null && enableSearch.ToBoolean())
        {
            dic["enable_search"] = true;
            var searchOpts = new Dictionary<String, Object>();
            var strategy = request["SearchStrategy"] as String;
            var forcedSearch = request["ForcedSearch"];
            var enableSource = request["EnableSource"];
            var enableSearchExt = request["EnableSearchExtension"];
            if (!strategy.IsNullOrEmpty()) searchOpts["search_strategy"] = strategy!;
            if (forcedSearch != null && forcedSearch.ToBoolean()) searchOpts["forced_search"] = true;
            if (enableSource != null && enableSource.ToBoolean()) searchOpts["enable_source"] = true;
            if (enableSearchExt != null && enableSearchExt.ToBoolean()) searchOpts["enable_search_extension"] = true;
            if (searchOpts.Count > 0) dic["search_options"] = searchOpts;
        }

        // 图文混合输出（部分 Qwen 搜索增强功能）
        var enableMixed = request["EnableTextImageMixed"];
        if (enableMixed != null && enableMixed.ToBoolean())
            dic["enable_text_image_mixed"] = true;

        // ===== 内置工具：web_search / web_extractor / code_interpreter =====
        // 与 Function Calling 不同：内置工具只有 {"type":"xxx"}，不含 "function" 子对象，插入至 tools 数组头部
        var enableWebExtractor = request["EnableWebExtractor"];
        var enableCodeInterp = request["EnableCodeInterpreter"];
        if ((enableWebExtractor != null && enableWebExtractor.ToBoolean()) ||
            (enableCodeInterp != null && enableCodeInterp.ToBoolean()))
        {
            var existingTools = dic.ContainsKey("tools") ? dic["tools"] as IList<Object> : null;
            var allTools = new List<Object>();
            // web_extractor 需同时开启 web_search 与 web_extractor
            if (enableWebExtractor != null && enableWebExtractor.ToBoolean())
            {
                allTools.Add(new Dictionary<String, Object> { ["type"] = "web_search" });
                allTools.Add(new Dictionary<String, Object> { ["type"] = "web_extractor" });
            }
            if (enableCodeInterp != null && enableCodeInterp.ToBoolean())
                allTools.Add(new Dictionary<String, Object> { ["type"] = "code_interpreter" });
            if (existingTools != null)
            {
                foreach (var t in existingTools) allTools.Add(t);
            }
            dic["tools"] = allTools;
        }
    }

    // 搜索意图关键词：触发时激活 enable_search + enable_source
    private static readonly String[] _searchKeywords =
    [
        "搜索", "查一下", "查询", "查找", "找一找", "最新", "实时", "当前", "今天", "今日",
        "新闻", "资讯", "股价", "股票", "天气", "汇率", "价格", "排行", "榜单",
        "什么时候", "发布了吗", "有没有", "最近", "目前", "现在",
        "search", "latest", "current", "today", "news", "price",
    ];

    // 爬取意图关键词：触发时激活 web_extractor（隐含 web_search）
    private static readonly String[] _extractKeywords =
    [
        "抓取", "爬取", "爬虫", "爬一下", "读取网页", "访问网址", "访问链接", "打开链接",
        "分析这个链接", "分析这个网址", "分析这个页面", "看一下这个链接", "看一下这个网页",
        "fetch", "crawl", "scrape",
    ];

    /// <summary>自动推断联网意图。仅当外部未显式设置 EnableSearch / EnableWebExtractor 时，
    /// 从最后一条用户消息中检测 URL 或关键词，自动激活对应的 DashScope 能力。</summary>
    /// <param name="request">统一请求，结果写回 request["EnableSearch"] / request["EnableWebExtractor"]</param>
    private static void AutoDetectSearchIntent(IChatRequest request)
    {
        // 已显式设置则尊重调用方决定，不覆盖
        if (request["EnableSearch"] != null || request["EnableWebExtractor"] != null) return;

        // 取最后一条 user 消息文本
        var lastMsg = request.Messages?.LastOrDefault(m =>
            String.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content as String;
        if (lastMsg.IsNullOrEmpty()) return;

        // 检测 URL（以 http:// 或 https:// 开头的片段）→ 触发 web_extractor
        if (lastMsg.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            lastMsg.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            request["EnableWebExtractor"] = true;
            return;
        }

        // 检测爬取类关键词 → 触发 web_extractor
        foreach (var kw in _extractKeywords)
        {
            if (lastMsg.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                request["EnableWebExtractor"] = true;
                return;
            }
        }

        // 检测搜索类关键词 → 触发 enable_search + enable_source
        foreach (var kw in _searchKeywords)
        {
            if (lastMsg.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                request["EnableSearch"] = true;
                request["EnableSource"] = true;
                return;
            }
        }
    }

    /// <summary>构建 Omni 兼容模式请求体。在标准 OpenAI 请求体基础上注入 modalities、audio 等 Omni 专属字段</summary>
    /// <param name="request">统一请求</param>
    /// <returns>可直接序列化的请求字典</returns>
    private IDictionary<String, Object> BuildOmniBody(IChatRequest request)
    {
        AutoDetectSearchIntent(request);
        var dic = ChatCompletionRequest.BuildBody(request);
        AppendDashScopeFields(dic, request);

        // Omni 模型 API 强制要求 stream=true
        dic["stream"] = true;
        dic["stream_options"] = new Dictionary<String, Object> { ["include_usage"] = true };

        // modalities：默认纯文本输出；调用方通过 request["OmniModalities"] 传入 string[] 可启用音频输出
        var modalities = request["OmniModalities"] as String[] ?? request["Modalities"] as String[];
        if (modalities == null)
        {
            var omniVoice = request["OmniVoice"] as String;
            modalities = String.IsNullOrEmpty(omniVoice) ? ["text"] : ["text", "audio"];
        }
        dic["modalities"] = modalities;

        // audio output config（voice + format）：当 modalities 含 "audio" 时生效
        if (Array.IndexOf(modalities, "audio") >= 0)
        {
            var voice = request["OmniVoice"] as String ?? "Tina";
            var format = request["OmniAudioFormat"] as String ?? "wav";
            dic["audio"] = new Dictionary<String, Object> { ["voice"] = voice, ["format"] = format };
        }

        return dic;
    }

    /// <summary>第三方托管模型流式对话。走兼容模式端点，使用 OpenAI 协议请求与 SSE 解析</summary>
    private async IAsyncEnumerable<IChatResponse> ChatThirdPartyStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var body = ChatCompletionRequest.BuildBody(request);
        var url = CombineApiUrl(GetCompatibleBaseUrl(), "/v1/chat/completions");

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;
            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0 || data == "[DONE]") continue;

            IChatResponse? chunk = null;
            // base.ParseChunk 调用 AiClientBase.ParseChunk → ParseResponse（OpenAI 格式），不走 DashScope 原生解析
            try { chunk = base.ParseChunk(data, request, null); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }

    /// <summary>Omni 模型流式对话。走兼容模式端点，强制 stream=true，使用 OpenAI 协议解析 SSE 块</summary>
    private async IAsyncEnumerable<IChatResponse> ChatOmniStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var body = BuildOmniBody(request);
        var url = CombineApiUrl(GetCompatibleBaseUrl(), "/v1/chat/completions");

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;
            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0 || data == "[DONE]") continue;

            IChatResponse? chunk = null;
            try { chunk = base.ParseChunk(data, request, null); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }

    /// <summary>Omni 模型非流式对话。内部通过 <see cref="ChatOmniStreamAsync"/> 流式收集后聚合返回</summary>
    private async Task<IChatResponse> ChatOmniAggregateAsync(IChatRequest request, CancellationToken cancellationToken)
    {
        var sb = Pool.StringBuilder.Get();
        var reasoningSb = Pool.StringBuilder.Get();
        IChatResponse? last = null;

        await foreach (var chunk in ChatOmniStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            last = chunk;
            foreach (var choice in chunk.Messages ?? [])
            {
                if (choice.Delta?.Content is String text && text.Length > 0)
                    sb.Append(text);
                if (choice.Delta?.ReasoningContent is String reasoning && reasoning.Length > 0)
                    reasoningSb.Append(reasoning);
            }
        }

        var content = sb.Return(true);
        var reasoningContent = reasoningSb.Return(true);

        return new ChatCompletionResponse
        {
            Object = "chat.completion",
            Id = last?.Id,
            Model = last?.Model ?? request.Model,
            Created = DateTimeOffset.UtcNow.ToLong(),
            Choices =
            [
                new CompletionChoice
                {
                    Index = 0,
                    FinishReason = "stop",
                    Message = new ChatMessage { Role = "assistant", Content = content, ReasoningContent = reasoningContent.Length > 0 ? reasoningContent : null },
                }
            ],
            Usage = last?.Usage is { } u ? new CompletionUsage { PromptTokens = u.InputTokens, CompletionTokens = u.OutputTokens, TotalTokens = u.TotalTokens } : null,
        };
    }

    /// <summary>非流式对话。原生协议走 DashScope 格式，兼容模式委托基类</summary>
    protected override async Task<IChatResponse> ChatAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        // Omni 全模态模型强制走兼容模式（API 要求 stream=true）；内部流式聚合为非流式响应
        if (IsOmniModel(request.Model ?? _options.Model))
            return await ChatOmniAggregateAsync(request, cancellationToken).ConfigureAwait(false);

        // 第三方托管模型（GLM/Kimi/MiniMax 等）不支持 DashScope 原生端点，强制走兼容模式
        if (IsNativeProtocol && IsThirdPartyModel(request.Model ?? _options.Model))
        {
            var compatUrl = CombineApiUrl(GetCompatibleBaseUrl(), "/v1/chat/completions");
            var compatBody = ChatCompletionRequest.BuildBody(request);
            var compatJson = await PostAsync(compatUrl, compatBody, request, _options, cancellationToken).ConfigureAwait(false);
            return ParseResponse(compatJson, request);
        }

        if (!IsNativeProtocol)
            return await base.ChatAsync(request, cancellationToken).ConfigureAwait(false);

        var model = request.Model ?? _options.Model;
        var url = BuildUrl(request);
        AutoDetectSearchIntent(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));
        var json = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        var dashResp = json.ToJsonEntity<DashScopeResponse>(JsonOptions)!;
        if (!dashResp.Code.IsNullOrEmpty())
            throw new HttpRequestException($"[DashScope] 错误 {dashResp.Code}: {dashResp.Message}");

        // 原生响应无顶层 model 字段，从请求回填
        dashResp.Model = model;
        if (dashResp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion";

        return dashResp;
    }

    /// <summary>流式对话。原生协议走 DashScope SSE 格式，兼容模式委托基类</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Omni 全模态模型强制走兼容模式流式接口
        if (IsOmniModel(request.Model ?? _options.Model))
        {
            await foreach (var chunk in ChatOmniStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        // 第三方托管模型（GLM/Kimi/MiniMax 等）不支持 DashScope 原生端点，强制走兼容模式
        if (IsNativeProtocol && IsThirdPartyModel(request.Model ?? _options.Model))
        {
            await foreach (var chunk in ChatThirdPartyStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        if (!IsNativeProtocol)
        {
            await foreach (var chunk in base.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var url = BuildUrl(request);
        AutoDetectSearchIntent(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
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

            IChatResponse? chunk = null;
            try { chunk = ParseChunk(data, request, null); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }
    #endregion

    #region 辅助
    // 原生对话路径（纯文本）
    private const String ChatGenerationPath = "/services/aigc/text-generation/generation";

    // 原生对话路径（多模态：含视觉/音频/视频输入）
    private const String MultimodalGenerationPath = "/services/aigc/multimodal-generation/generation";

    // 原生视频生成路径（Wan2.x 文生视频/图生视频）
    private const String VideoSynthesisPath = "/services/aigc/video-generation/video-synthesis";

    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var path = IsMultimodalModel(request.Model) ? MultimodalGenerationPath : ChatGenerationPath;

        // 原生协议只能对接 /api/v1 端点；若用户配置了兼容模式地址则忽略并回退到原生端点
        var endpoint = _options.Endpoint;
        if (endpoint.IsNullOrWhiteSpace() ||
            endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
            endpoint = GetNativeBaseUrl();
        return endpoint.TrimEnd('/') + path;
    }

    /// <summary>判断是否为第三方托管模型。采用白名单策略：阿里自有模型以 qwen/qwq-/wan 开头，其余均视为第三方，强制走兼容模式端点</summary>
    private static Boolean IsThirdPartyModel(String? model)
    {
        if (model.IsNullOrEmpty()) return false;
        // 阿里自有模型白名单前缀：通义千问系列、QwQ 推理、万相图像/视频
        if (model!.StartsWithIgnoreCase("qwen", "qwq-", "wan")) return false;
        return true;
    }

    /// <summary>判断指定模型是否为多模态模型（需走 multimodal-generation 端点）</summary>
    /// <remarks>
    /// 命名规律：
    /// <list type="bullet">
    /// <item>含 -vl / -ocr：Vision-Language 系列，走多模态端点</item>
    /// <item>qvq- 前缀：视觉推理系列（区别于纯文本推理 qwq-），走多模态端点</item>
    /// <item>qwen3.5/3.6/3.7 -plus/-flash/-turbo：支持文本+视觉，走多模态端点</item>
    /// <item>qwen3.5/3.6/3.7 -max（含 -max-preview）：纯文本旗舰，走 text-generation 端点</item>
    /// <item>音频理解模型（qwen-audio-*、qwen2-audio-*）：走多模态端点</item>
    /// </list>
    /// </remarks>
    private static Boolean IsMultimodalModel(String? model)
    {
        if (model.IsNullOrEmpty()) return false;
        // Omni 全模态模型不走原生多模态端点，走兼容模式
        if (IsOmniModel(model)) return false;
        // -vl / -ocr 标识符（视觉语言/OCR）
        if (model.IndexOf("-vl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (model.IndexOf("-ocr", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        // qvq- 前缀：视觉推理系列
        if (model.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase)) return true;
        // qwen3.5/3.6/3.7：-plus/-flash/-turbo 含视觉能力，走多模态端点；-max（含 -max-preview）为纯文本
        if (Regex.IsMatch(model, @"^qwen\d+\.\d+-", RegexOptions.IgnoreCase))
        {
            if (model.Contains("-plus", StringComparison.OrdinalIgnoreCase) ||
                model.Contains("-flash", StringComparison.OrdinalIgnoreCase) ||
                model.Contains("-turbo", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
        // 音频理解模型（qwen-audio-chat、qwen2-audio-instruct 等）使用多模态端点
        if (model.StartsWith("qwen-audio", StringComparison.OrdinalIgnoreCase)) return true;
        if (model.StartsWith("qwen2-audio", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>解析 DashScope 原生流式 SSE chunk，DashScopeResponse 适配器同时设置 Delta</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
    {
        var chunk = data.ToJsonEntity<DashScopeResponse>(JsonOptions);
        chunk?.Model = request.Model;
        if (chunk is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion.chunk";
        return chunk;
    }

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
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        var path = request.RequestUri?.AbsolutePath;
        if (path.IsNullOrEmpty()) return;

        // 视频生成接口仅支持异步调用，必须携带该请求头
        if (path.EndsWith(VideoSynthesisPath, StringComparison.OrdinalIgnoreCase))
            request.Headers.TryAddWithoutValidation("X-DashScope-Async", "enable");

        if (!IsNativeProtocol) return;
        if (chatRequest == null || !chatRequest.Stream) return;

        if (!path.EndsWith(ChatGenerationPath, StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(MultimodalGenerationPath, StringComparison.OrdinalIgnoreCase)) return;

        // qwen-plus 不能识别为多模态，得使用文本完成地址，但是accept需要text/event-stream
        //var model = chatRequest?.Model ?? options.Model;
        //if (IsMultimodalModel(model) || model.EndsWithIgnoreCase("-max", "-plus", "-turbo"))
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        //else
        request.Headers.TryAddWithoutValidation("X-DashScope-SSE", "enable");
    }

    #endregion
}
