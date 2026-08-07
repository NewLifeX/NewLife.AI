using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Embedding;
using NewLife.AI.Models;
using NewLife.ChatAI.Filters;

namespace NewLife.ChatAI.Controllers;

/// <summary>API 网关控制器。对外提供兼容 OpenAI / Anthropic / Gemini 标准协议的统一 API</summary>
/// <remarks>
/// 根据请求中的 model 字段自动路由到对应的模型提供商，
/// 通过 Authorization: Bearer {appkey} 进行认证。
/// </remarks>
[ApiController]
public class GatewayController(GatewayService gatewayService, ModelService modelService, ChatSetting chatSetting, MessageFlowForGateway gatewayMessageFlow) : ControllerBase
{
    #region 模型列表
    /// <summary>列出可用模型。兼容 OpenAI GET /v1/models 协议。支持可选查询参数做关键字和能力过滤</summary>
    /// <param name="key">可选。API 密钥（sk-xxx），作为 Authorization 头的替代方式。未提供时返回所有公开模型</param>
    /// <param name="keyword">可选。按关键字过滤模型 Code 或 Name（忽略大小写子串匹配）</param>
    /// <param name="capabilities">可选。逗号分隔的能力枚举（chat/thinking/function/vision/audio/speech/image/video/embedding/rerank），要求同时具备所列全部能力</param>
    /// <param name="supportThinking">可选。过滤支持思考的模型</param>
    /// <param name="supportFunction">可选。过滤支持函数调用的模型</param>
    /// <param name="supportVision">可选。过滤支持视觉的模型</param>
    /// <param name="supportAudio">可选。过滤支持音频的模型</param>
    /// <param name="supportSpeech">可选。过滤支持语音合成的模型</param>
    /// <param name="supportImage">可选。过滤支持图像生成的模型</param>
    /// <param name="supportVideo">可选。过滤支持视频生成的模型</param>
    /// <param name="supportEmbedding">可选。过滤支持嵌入向量的模型</param>
    /// <param name="supportRerank">可选。过滤支持重排序的模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("v1/models")]
    public IActionResult ListModelsAsync(
        [FromQuery(Name = "key")] String? key,
        [FromQuery(Name = "keyword")] String? keyword,
        [FromQuery(Name = "capabilities")] String? capabilities,
        [FromQuery(Name = "support_thinking")] Boolean? supportThinking,
        [FromQuery(Name = "support_function")] Boolean? supportFunction,
        [FromQuery(Name = "support_vision")] Boolean? supportVision,
        [FromQuery(Name = "support_audio")] Boolean? supportAudio,
        [FromQuery(Name = "support_speech")] Boolean? supportSpeech,
        [FromQuery(Name = "support_image")] Boolean? supportImage,
        [FromQuery(Name = "support_video")] Boolean? supportVideo,
        [FromQuery(Name = "support_embedding")] Boolean? supportEmbedding,
        [FromQuery(Name = "support_rerank")] Boolean? supportRerank,
        CancellationToken cancellationToken)
    {
        // 认证：Authorization 头为正式认证（无效则拒绝），key 查询参数为可选补充
        var hasAuthHeader = !String.IsNullOrWhiteSpace(Request.Headers.Authorization);
        AppKey? appKey = null;

        if (hasAuthHeader)
        {
            appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
            if (appKey == null)
                return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });
        }
        else if (!key.IsNullOrEmpty())
        {
            appKey = ValidateAppKeyBySecret(key!);
        }

        // 获取模型列表：有 AppKey 时按权限过滤，否则返回所有公开模型
        var models = appKey != null
            ? modelService.GetModelsForAppKey(appKey)
            : modelService.GetAllPublicModels();

        // 应用关键字和能力过滤
        models = modelService.FilterModels(models, keyword, capabilities,
            supportThinking, supportFunction, supportVision, supportAudio,
            supportSpeech, supportImage, supportVideo, supportEmbedding, supportRerank);

        var data = models.Select(m =>
        {
            var created = m.CreateTime > DateTime.MinValue
                ? new DateTimeOffset(m.CreateTime, TimeSpan.Zero).ToUnixTimeSeconds()
                : 0L;
            var ownedBy = m.ProviderInfo?.Code ?? "system";
            return new Dictionary<String, Object?>
            {
                ["id"] = m.Code,
                ["name"] = m.Name,
                ["object"] = "model",
                ["created"] = created,
                ["owned_by"] = ownedBy,
                ["context_length"] = m.ContextLength,
                ["support_thinking"] = m.SupportThinking,
                ["support_function"] = m.SupportFunction,
                ["support_vision"] = m.SupportVision,
                ["support_audio"] = m.SupportAudio,
                ["support_image"] = m.SupportImage,
                ["support_video"] = m.SupportVideo,
            };
        }).ToList();

        var result = new Dictionary<String, Object>
        {
            ["object"] = "list",
            ["data"] = data,
        };

        return Content(JsonSerializer.Serialize(result, GatewayService.SnakeCaseOptions), "application/json");
    }

    /// <summary>通过密钥字符串直接校验 AppKey（用于 key 查询参数场景）</summary>
    private static AppKey? ValidateAppKeyBySecret(String secret)
    {
        if (String.IsNullOrWhiteSpace(secret)) return null;

        var appKey = AppKey.FindBySecret(secret.Trim());
        if (appKey == null || !appKey.Enable) return null;
        if (appKey.ExpireTime.Year > 2000 && appKey.ExpireTime < DateTime.Now) return null;

        return appKey;
    }
    #endregion

    #region OpenAI Chat Completions
    /// <summary>OpenAI Chat Completions 兼容接口。支持流式和非流式</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/chat/completions")]
    [SnakeCaseBody]
    public async Task ChatCompletionsAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request, GatewayProtocol.OpenAI, cancellationToken).ConfigureAwait(false);
    #endregion

    #region OpenAI Response API
    /// <summary>OpenAI Response API 兼容接口。用于 o3/o4-mini/gpt-5 等推理模型</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>协议格式与 ChatCompletions 完全兼容，复用同一处理逻辑</remarks>
    [HttpPost("v1/responses")]
    [SnakeCaseBody]
    public async Task ResponsesAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request, GatewayProtocol.OpenAI, cancellationToken).ConfigureAwait(false);
    #endregion

    #region Anthropic Messages API
    /// <summary>Anthropic Messages API 兼容接口。接受 Anthropic 原生格式请求（snake_case）并转换为内部统一模型</summary>
    /// <param name="request">Anthropic 原生请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>
    /// 与 OpenAI 的主要差异：system 为顶级独立字段，stop_sequences 对应 stop。
    /// 认证头 x-api-key 与 Bearer Token 均被支持，由 ValidateAppKey 统一处理。
    /// </remarks>
    [HttpPost("v1/messages")]
    [SnakeCaseBody]
    public async Task MessagesAsync([FromBody] AnthropicRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request, GatewayProtocol.Anthropic, cancellationToken).ConfigureAwait(false);

    /// <summary>获取认证密钥。优先从 Authorization 头获取，回退到 x-api-key 头（Anthropic 协议兼容）</summary>
    /// <returns>认证字符串</returns>
    private String? GetAuthKey()
    {
        var auth = Request.Headers.Authorization.ToString();
        if (!String.IsNullOrWhiteSpace(auth)) return auth;

        // Anthropic 协议使用 x-api-key 头
        var xApiKey = Request.Headers["x-api-key"].ToString();
        if (!String.IsNullOrWhiteSpace(xApiKey)) return xApiKey;

        return null;
    }
    #endregion

    #region Google Gemini API
    /// <summary>Google Gemini API 兼容接口。接受 Gemini 原生格式请求（camelCase）并转换为内部统一模型</summary>
    /// <param name="request">Gemini 原生请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>
    /// 与 OpenAI 的主要差异：contents 对应 messages，角色 model 对应 assistant，generationConfig 封装生成参数。
    /// Gemini 原生字段名为 camelCase，由 CamelCaseBodyAttribute 指示 GatewayJsonInputFormatter 使用对应选项。
    /// </remarks>
    [HttpPost("v1/gemini")]
    [CamelCaseBody]
    public async Task GeminiAsync([FromBody] GeminiRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request, GatewayProtocol.Gemini, cancellationToken).ConfigureAwait(false);
    #endregion

    #region Ollama 原生协议（入站伪装）
    /// <summary>Ollama /api/chat 对话接口。接受 Ollama 原生格式请求（snake_case），流式输出 NDJSON</summary>
    /// <param name="request">Ollama 原生对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>
    /// 与 OpenAI 的差异：流式响应为 NDJSON（每行一个 JSON 对象），无 data: 前缀与 [DONE]；
    /// 消息字段为 message（非 choices[0].delta），末帧携带 done/done_reason/prompt_eval_count/eval_count。
    /// 工具透传：请求 tools 数组解析为 ChatTool 注入统一管道，由消息流执行服务端工具。
    /// </remarks>
    [HttpPost("api/chat")]
    [SnakeCaseBody]
    public async Task OllamaChatAsync([FromBody] OllamaChatRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request, GatewayProtocol.Ollama, cancellationToken).ConfigureAwait(false);

    /// <summary>Ollama /api/generate 生成接口。接受补全风格请求（prompt），流式输出 NDJSON response 帧</summary>
    /// <param name="request">Ollama 原生生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>内部将 prompt/system 包装为对话消息复用统一流程，响应按 generate 协议格式输出（response 顶级字段）</remarks>
    [HttpPost("api/generate")]
    [SnakeCaseBody]
    public async Task OllamaGenerateAsync([FromBody] OllamaGenerateRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            await WriteErrorAsync(400, "INVALID_REQUEST", "请求体不能为空").ConfigureAwait(false);
            return;
        }

        // 包装为对话请求：system → system 消息，prompt → user 消息，复用统一对话流程
        var messages = new List<OllamaChatMessage>();
        if (!request.System.IsNullOrEmpty())
            messages.Add(new OllamaChatMessage { Role = "system", Content = request.System });
        var userMsg = new OllamaChatMessage { Role = "user", Content = request.Prompt };
        if (request.Images is { Length: > 0 })
            userMsg.Images = request.Images;
        messages.Add(userMsg);

        var chatReq = new OllamaChatRequest
        {
            Model = request.Model,
            Stream = request.Stream ?? true,
            Messages = messages,
            Options = request.Options,
            Format = request.Format,
            Think = request.Think,
            KeepAlive = request.KeepAlive?.ToLong(),
        };
        await ProcessChatAsync(chatReq, GatewayProtocol.OllamaGenerate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ollama /api/tags 模型列表。兼容 Ollama GET /api/tags 协议，按 AppKey 权限返回可用模型清单</summary>
    [HttpGet("api/tags")]
    public IActionResult OllamaTagsAsync()
    {
        if (!chatSetting.EnableGateway)
            return StatusCode(503, new { code = "GATEWAY_DISABLED", message = "API 网关已关闭" });

        var appKey = gatewayService.ValidateAppKey(GetAuthKey());
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        var models = modelService.GetModelsForAppKey(appKey);
        var data = models.Select(m => new Dictionary<String, Object?>
        {
            ["name"] = m.Code,
            ["model"] = m.Code,
            ["modified_at"] = m.UpdateTime > DateTime.MinValue ? m.UpdateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") : "1970-01-01T00:00:00Z",
            ["size"] = 0L,
            ["digest"] = "",
            ["details"] = new Dictionary<String, Object?>
            {
                ["format"] = "gguf",
                ["family"] = m.ProviderInfo?.Code,
                ["families"] = m.ProviderInfo != null ? new[] { m.ProviderInfo.Code } : Array.Empty<String>(),
                ["parameter_size"] = "unknown",
                ["quantization_level"] = "unknown",
            },
        }).ToList();

        return Content(JsonSerializer.Serialize(new Dictionary<String, Object> { ["models"] = data }, GatewayService.SnakeCaseOptions), "application/json");
    }

    /// <summary>Ollama /api/version 版本探测。与 Ollama 官方行为一致不做认证，供客户端启动探测</summary>
    [HttpGet("api/version")]
    public IActionResult OllamaVersionAsync() => Ok(new { version = "0.1.0" });

    /// <summary>Ollama /api/show 模型信息。返回模型元数据（details/capabilities 等）</summary>
    /// <param name="request">模型信息请求</param>
    [HttpPost("api/show")]
    [SnakeCaseBody]
    public IActionResult OllamaShowAsync([FromBody] OllamaShowRequest request)
    {
        if (!chatSetting.EnableGateway)
            return StatusCode(503, new { code = "GATEWAY_DISABLED", message = "API 网关已关闭" });

        var appKey = gatewayService.ValidateAppKey(GetAuthKey());
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        if (request == null || request.Model.IsNullOrEmpty())
            return BadRequest(new { code = "INVALID_REQUEST", message = "model 不能为空" });

        var config = modelService.ResolveAvailableModelByCode(request.Model);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到可用模型 '{request.Model}'" });
        if (!modelService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{request.Model}'" });

        var capabilities = new List<String> { "completion", "chat" };
        if (config.SupportFunction) capabilities.Add("tools");
        if (config.SupportVision) capabilities.Add("vision");
        if (config.SupportThinking) capabilities.Add("reasoning");
        if (config.SupportEmbedding) capabilities.Add("embedding");

        var result = new Dictionary<String, Object?>
        {
            ["license"] = "",
            ["modelfile"] = "",
            ["parameters"] = "",
            ["template"] = "",
            ["details"] = new Dictionary<String, Object?>
            {
                ["format"] = "gguf",
                ["family"] = config.ProviderInfo?.Code,
                ["families"] = config.ProviderInfo != null ? new[] { config.ProviderInfo.Code } : Array.Empty<String>(),
                ["parameter_size"] = "unknown",
                ["quantization_level"] = "unknown",
            },
            ["model_info"] = new Dictionary<String, Object?>(),
            ["capabilities"] = capabilities,
        };
        return Content(JsonSerializer.Serialize(result, GatewayService.SnakeCaseOptions), "application/json");
    }

    /// <summary>Ollama /api/embed 嵌入接口（新版）。input 可为字符串或字符串数组，返回 embeddings 数组</summary>
    /// <param name="request">Ollama 嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("api/embed")]
    [SnakeCaseBody]
    public async Task<IActionResult> OllamaEmbedAsync([FromBody] OllamaEmbedRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { code = "INVALID_REQUEST", message = "请求体不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        // input 支持字符串或字符串数组两种形式（反序列化后为 String 或 JsonElement）
        IList<String> inputs;
        if (request.Input is String s)
            inputs = [s];
        else if (request.Input is JsonElement je && je.ValueKind == JsonValueKind.Array)
            inputs = je.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        else
            return BadRequest(new { code = "INVALID_REQUEST", message = "input 不能为空" });

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IEmbeddingClient ec)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持嵌入向量" });

            var resp = await ec.GenerateAsync(new EmbeddingRequest
            {
                Model = request.Model,
                Input = inputs,
                Dimensions = request.Dimensions,
            }, cancellationToken).ConfigureAwait(false);

            var embeddings = resp.Data.OrderBy(e => e.Index).Select(e => e.Embedding).ToList();
            return Ok(new Dictionary<String, Object?>
            {
                ["model"] = request.Model,
                ["embeddings"] = embeddings,
                ["total_duration"] = 0L,
                ["load_duration"] = 0L,
                ["prompt_eval_count"] = resp.Usage?.PromptTokens ?? 0,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "EMBEDDING_FAILED", message = ex.Message });
        }
    }

    /// <summary>Ollama /api/embeddings 嵌入接口（旧版兼容）。输入单个 prompt，返回单条 embedding</summary>
    /// <param name="request">Ollama 旧版嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("api/embeddings")]
    [SnakeCaseBody]
    public async Task<IActionResult> OllamaEmbeddingsAsync([FromBody] OllamaEmbeddingsRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.Prompt.IsNullOrEmpty())
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IEmbeddingClient ec)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持嵌入向量" });

            var resp = await ec.GenerateAsync(new EmbeddingRequest
            {
                Model = request.Model,
                Input = [request.Prompt],
            }, cancellationToken).ConfigureAwait(false);

            var embedding = resp.Data.OrderBy(e => e.Index).FirstOrDefault()?.Embedding;
            return Ok(new Dictionary<String, Object?>
            {
                ["embedding"] = embedding,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "EMBEDDING_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 图像生成
    /// <summary>图像生成接口。按 model 字段路由到对应的图像生成服务商</summary>
    /// <param name="body">请求体，包含 model/prompt/size/n 等参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/images/generations")]
    [SnakeCaseBody]
    public async Task<IActionResult> ImageGenerationsAsync([FromBody] IDictionary<String, Object> body, CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        // 解析请求参数
        body.TryGetValue("model", out var modelObj);
        body.TryGetValue("prompt", out var promptObj);
        var modelCode = modelObj?.ToString();
        var prompt = promptObj?.ToString();

        if (String.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        // 路由到模型
        var config = modelService.ResolveAvailableModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到可用模型 '{modelCode}'" });
        if (!modelService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        // 通过 ChatCompletions 方式请求图像生成（兼容 OpenAI DALL-E 等通过聊天接口生成图像的场景）
        var size = chatSetting.DefaultImageSize;
        if (body.TryGetValue("size", out var sizeObj) && sizeObj != null)
            size = sizeObj.ToString()!;

        try
        {
            using var imageClient = modelService.CreateClient(config)!;
            var response = await imageClient.GetResponseAsync(
                [new AiChatMessage { Role = "user", Content = $"Generate an image: {prompt}. Size: {size}" }],
                null,
                cancellationToken).ConfigureAwait(false);

            return Ok(new
            {
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = response.Messages?.FirstOrDefault()?.Message?.Content,
                    }
                }
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { code = "IMAGE_GENERATION_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 图像编辑
    /// <summary>图像编辑接口。解析 multipart/form-data，按 model 字段路由到对应的图像编辑服务商</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/images/edits")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImageEditsAsync(CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        // 从 multipart/form-data 中解析参数
        var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var modelCode = form["model"].FirstOrDefault();
        var prompt = form["prompt"].FirstOrDefault();
        var size = form["size"].FirstOrDefault() ?? chatSetting.DefaultImageSize;
        var imageFile = form.Files.GetFile("image");
        var maskFile = form.Files.GetFile("mask");

        if (String.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { code = "INVALID_REQUEST", message = "image 文件不能为空" });

        // 路由到模型
        var model = modelService.ResolveAvailableModelByCode(modelCode);
        if (model == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到可用模型 '{modelCode}'" });
        if (!modelService.IsModelAllowed(appKey, model))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        try
        {
            using var editClient = modelService.CreateClient(model)!;
            if (editClient is not IImageClient imageClient)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{model.Code}' 不支持图像编辑" });

            using var imageStream = imageFile.OpenReadStream();
            using var maskStream = maskFile != null && maskFile.Length > 0 ? maskFile.OpenReadStream() : null;

            var response = await imageClient.EditImageAsync(new ImageEditsRequest
            {
                Model = model.GetEffectiveModelCode(),
                Prompt = prompt!,
                Size = size,
                ImageStream = imageStream,
                ImageFileName = String.IsNullOrWhiteSpace(imageFile.FileName) ? "image.png" : imageFile.FileName,
                MaskStream = maskStream,
                MaskFileName = maskFile != null && !String.IsNullOrWhiteSpace(maskFile.FileName) ? maskFile.FileName : "mask.png",
            }, cancellationToken).ConfigureAwait(false);

            return Ok(NormalizeImageEditResponse(response, prompt!));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { code = "MODEL_UNSUPPORTED", message = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(502, new { code = "IMAGE_EDIT_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 嵌入向量
    /// <summary>嵌入向量接口。兼容 OpenAI POST /v1/embeddings 协议</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/embeddings")]
    [SnakeCaseBody]
    public async Task<IActionResult> EmbeddingsAsync([FromBody] EmbeddingRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { code = "INVALID_REQUEST", message = "请求体不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IEmbeddingClient ec)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持嵌入向量" });

            var resp = await ec.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "EMBEDDING_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 语音合成（TTS）
    /// <summary>语音合成接口。兼容 OpenAI POST /v1/audio/speech 协议，返回 audio/mpeg 流</summary>
    /// <param name="request">合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/audio/speech")]
    [SnakeCaseBody]
    public async Task<IActionResult> AudioSpeechAsync([FromBody] SpeechRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { code = "INVALID_REQUEST", message = "请求体不能为空" });
        if (String.IsNullOrEmpty(request.Input)) return BadRequest(new { code = "INVALID_REQUEST", message = "input 不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not ISpeechClient sc)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持语音合成" });

            var bytes = await sc.SpeechAsync(request, cancellationToken).ConfigureAwait(false);
            var contentType = (request.ResponseFormat ?? "mp3") switch
            {
                "wav" => "audio/wav",
                "opus" => "audio/opus",
                "aac" => "audio/aac",
                "flac" => "audio/flac",
                "pcm" => "audio/pcm",
                _ => "audio/mpeg",
            };
            return File(bytes, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "SPEECH_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 语音识别（STT）
    /// <summary>语音识别接口。兼容 OpenAI POST /v1/audio/transcriptions 协议（multipart/form-data）</summary>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/audio/transcriptions")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AudioTranscriptionsAsync(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var modelCode = form["model"].FirstOrDefault();
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
            return BadRequest(new { code = "INVALID_REQUEST", message = "file 不能为空" });

        var (forbid, config) = ValidateAndResolve(modelCode);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not ITranscriptionClient tc)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{modelCode}' 不支持语音识别" });

            using var stream = file.OpenReadStream();
            var req = new TranscriptionRequest
            {
                Model = modelCode,
                File = stream,
                FileName = file.FileName,
                Language = form["language"].FirstOrDefault(),
                Prompt = form["prompt"].FirstOrDefault(),
                ResponseFormat = form["response_format"].FirstOrDefault(),
                Temperature = Double.TryParse(form["temperature"].FirstOrDefault(), out var t) ? t : null,
            };
            var resp = await tc.TranscribeAsync(req, cancellationToken).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "TRANSCRIPTION_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 视频生成
    /// <summary>提交视频生成任务。POST /v1/video/generations，返回 task_id</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/video/generations")]
    [SnakeCaseBody]
    public async Task<IActionResult> VideoGenerationsAsync([FromBody] VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { code = "INVALID_REQUEST", message = "请求体不能为空" });
        if (String.IsNullOrEmpty(request.Prompt)) return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IVideoClient vc)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持视频生成" });

            var resp = await vc.SubmitVideoGenerationAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "VIDEO_GENERATION_FAILED", message = ex.Message });
        }
    }

    /// <summary>查询视频生成任务状态。GET /v1/video/generations/{taskId}?model=xxx</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="model">模型编码（用于定位服务商）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("v1/video/generations/{taskId}")]
    public async Task<IActionResult> VideoTaskStatusAsync(String taskId, [FromQuery] String? model, CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(taskId)) return BadRequest(new { code = "INVALID_REQUEST", message = "taskId 不能为空" });

        var (forbid, config) = ValidateAndResolve(model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IVideoClient vc)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{model}' 不支持视频生成" });

            var resp = await vc.GetVideoTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "VIDEO_TASK_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 重排序
    /// <summary>文档重排序。POST /v1/reranks（DashScope 兼容格式）</summary>
    /// <param name="request">重排请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/reranks")]
    [SnakeCaseBody]
    public async Task<IActionResult> RerankAsync([FromBody] RerankRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { code = "INVALID_REQUEST", message = "请求体不能为空" });
        if (String.IsNullOrEmpty(request.Query)) return BadRequest(new { code = "INVALID_REQUEST", message = "query 不能为空" });

        var (forbid, config) = ValidateAndResolve(request.Model);
        if (forbid != null) return forbid;

        try
        {
            using var client = modelService.CreateClient(config!);
            if (client is not IRerankClient rc)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{request.Model}' 不支持重排序" });

            var resp = await rc.RerankAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { code = "RERANK_FAILED", message = ex.Message });
        }
    }
    #endregion

    #region 通用校验
    /// <summary>校验 AppKey 与模型可用性。返回 (错误响应, 模型配置)，错误响应不为空时调用方应直接 return</summary>
    /// <param name="modelCode">模型编码</param>
    /// <returns>错误响应或模型配置</returns>
    private (IActionResult? error, NewLife.ChatAI.Entity.ModelConfig? config) ValidateAndResolve(String? modelCode)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return (Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" }), null);

        var config = modelService.ResolveAvailableModelByCode(modelCode);
        if (config == null)
            return (NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到可用模型 '{modelCode}'" }), null);

        if (!modelService.IsModelAllowed(appKey, config))
            return (StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" }), null);

        return (null, config);
    }
    #endregion

    #region 辅助
    /// <summary>网关统一化：将协议专用请求转换为内部统一 ChatRequest。统一请求原样返回；各协议请求经 ToChatRequest 转换</summary>
    /// <param name="request">入站请求（可为各协议原生请求）</param>
    /// <returns>统一 ChatRequest，无法识别的类型原样返回</returns>
    private static IChatRequest NormalizeGatewayRequest(IChatRequest request)
    {
        if (request is ChatRequest) return request;
        if (request is ChatCompletionRequest cc) return cc.ToChatRequest();
        if (request is AnthropicRequest ar) return ar.ToChatRequest();
        if (request is GeminiRequest gr) return gr.ToChatRequest();
        if (request is OllamaChatRequest oc) return oc.ToChatRequest();
        return request;
    }

    /// <summary>核心对话处理逻辑。认证、模型路由、根据协议格式化流式/非流式响应，由各协议端点共用</summary>
    /// <param name="request">对话请求（可以是各协议原生请求，均实现 IChatRequest）</param>
    /// <param name="protocol">目标响应协议（OpenAI / Anthropic / Gemini）</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ProcessChatAsync(IChatRequest request, GatewayProtocol protocol, CancellationToken cancellationToken)
    {
        // 认证校验（支持 Authorization: Bearer 和 x-api-key 两种方式）
        var appKey = gatewayService.ValidateAppKey(GetAuthKey());
        if (appKey == null)
        {
            await WriteErrorAsync(401, "INVALID_API_KEY", "AppKey 无效或已禁用").ConfigureAwait(false);
            return;
        }

        // 网关统一化：协议专用请求（Anthropic/Gemini/Ollama 等）先转为内部统一 ChatRequest，
        // 使过滤器链与消息流工作于统一对象——协议请求的 Messages 适配器修改不再被静默丢弃（A-94 方案）
        request = NormalizeGatewayRequest(request);

        // 模型路由
        var config = modelService.ResolveAvailableModelByCode(request.Model);
        if (config == null)
        {
            await WriteErrorAsync(404, "MODEL_NOT_FOUND", $"未找到可用模型 '{request.Model}'").ConfigureAwait(false);
            return;
        }
        if (!modelService.IsModelAllowed(appKey, config))
        {
            await WriteErrorAsync(403, "MODEL_FORBIDDEN", $"当前密钥无权使用模型 '{request.Model}'").ConfigureAwait(false);
            return;
        }

        // 网关对话记录：收集流式输出内容
        var enableRecording = chatSetting.EnableGatewayRecording;
        var contentBuilder = enableRecording ? new StringBuilder() : null;
        var thinkingBuilder = enableRecording ? new StringBuilder() : null;

        try
        {
            // 开启对话记录时预创建会话，确保 UsageRecord 可关联到对应会话
            if (enableRecording)
            {
                var conversation = gatewayService.CreateGatewayConversation(request, config, appKey);
                if (conversation != null)
                {
                    conversation.Insert();

                    request.ConversationId = conversation.Id.ToString();
                }
            }

            var messages = gatewayService.BuildContextMessages(request, appKey, config);
            var convId = request.ConversationId.ToLong();

            if (request.Stream)
            {
                // Ollama 协议使用 NDJSON 流式（每行一个 JSON 对象），其余协议使用 SSE
                var isOllama = protocol is GatewayProtocol.Ollama or GatewayProtocol.OllamaGenerate;
                Response.Headers.Append("Content-Type", isOllama ? "application/x-ndjson" : "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");
                Response.Headers.Append("Connection", "keep-alive");
                Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送

                // 输出流式开始事件（Anthropic 需要 message_start + content_block_start）
                foreach (var sseEvent in GatewayService.FormatStreamStart(request.Model ?? config.Code, protocol))
                {
                    await Response.WriteAsync(sseEvent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                UsageDetails? lastUsage = null;
                // 记录本轮是否发出过工具调用事件，供 ConvertEventToChunk 决定 message_done 是否输出 finish_reason
                var hasToolCalls = false;
                await foreach (var ev in gatewayMessageFlow.StreamGatewayAsync(messages, config, appKey.UserId, convId, request, cancellationToken).ConfigureAwait(false))
                {
                    if (enableRecording)
                    {
                        if (ev.Type == "content_delta")
                            contentBuilder!.Append(ev.Content);
                        else if (ev.Type == "thinking_delta")
                            thinkingBuilder!.Append(ev.Content);
                    }
                    if (ev.Usage != null) lastUsage = ev.Usage;

                    if (ev.Type == "tool_call_start" || ev.Type == "tool_call_done" || ev.Type == "tool_call_error")
                        hasToolCalls = true;

                    var chunk = GatewayService.ConvertEventToChunk(ev, request.Model ?? config.Code, hasToolCalls);
                    if (chunk != null)
                        await WriteStreamChunkAsync(chunk, protocol, cancellationToken).ConfigureAwait(false);
                }

                // 输出流式结束标记
                var endMarker = GatewayService.FormatStreamEnd(protocol);
                if (endMarker != null)
                    await Response.WriteAsync(endMarker, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                // 用量记录 + 网关对话记录
                gatewayService.RecordUsage(appKey, config, convId, lastUsage);
                if (enableRecording)
                    await gatewayService.RecordGatewayConversationAsync(request, config, appKey, contentBuilder!.ToString(), thinkingBuilder!.ToString(), lastUsage).ConfigureAwait(false);
            }
            else
            {
                // 非流式：聚合完整响应 → 写出 JSON → 用量/对话记录
                var result = await gatewayMessageFlow.CompletionGatewayAsync(messages, config, appKey.UserId, convId, request, cancellationToken).ConfigureAwait(false);
                Response.ContentType = "application/json";
                await Response.WriteAsync(GatewayService.FormatResponse(result, protocol), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                // 用量记录 + 网关对话记录
                gatewayService.RecordUsage(appKey, config, convId, result.Usage);
                if (enableRecording)
                {
                    var thinking = result.Messages?.FirstOrDefault()?.Message?.ReasoningContent;
                    await gatewayService.RecordGatewayConversationAsync(request, config, appKey, result.Text, thinking, result.Usage).ConfigureAwait(false);
                }
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("限流"))
        {
            await WriteErrorAsync(429, "RATE_LIMITED", ex.Message).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            var statusCode = (Int32?)ex.StatusCode ?? 502;
            await WriteErrorAsync(statusCode, "MODEL_UNAVAILABLE", ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 兜底：捕获来自后端的 ApiException 等非 HttpRequestException 异常，统一返回 502
            await WriteErrorAsync(502, "MODEL_UNAVAILABLE", ex.Message).ConfigureAwait(false);
        }
    }

    /// <summary>将流式块按协议格式写入 SSE 响应</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <param name="protocol">目标协议</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task WriteStreamChunkAsync(ChatResponse chunk, GatewayProtocol protocol, CancellationToken cancellationToken)
    {
        foreach (var sseEvent in GatewayService.FormatStreamEvents(chunk, protocol))
        {
            await Response.WriteAsync(sseEvent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>写入错误响应</summary>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="code">错误码</param>
    /// <param name="message">错误描述</param>
    private async Task WriteErrorAsync(Int32 statusCode, String code, String message)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";

        var error = new Dictionary<String, Object>
        {
            ["code"] = code,
            ["message"] = message,
        };
        var traceId = HttpContext.TraceIdentifier;
        if (!String.IsNullOrEmpty(traceId))
            error["traceId"] = traceId;

        await Response.WriteAsync(JsonSerializer.Serialize(error, GatewayService.SnakeCaseOptions), Encoding.UTF8).ConfigureAwait(false);
    }

    private static Object NormalizeImageEditResponse(ImageGenerationResponse? response, String prompt)
    {
        var created = response?.Created > DateTime.MinValue
            ? new DateTimeOffset(response.Created.ToUniversalTime()).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var data = response?.Data?.Select(item => new
        {
            revised_prompt = item.RevisedPrompt ?? prompt,
            content = GetImageContent(item),
            url = item.Url,
            b64_json = item.B64Json,
        }).ToArray();

        return new
        {
            created,
            data = data is { Length: > 0 }
                ? data
                : new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = (String?)null,
                        url = (String?)null,
                        b64_json = (String?)null,
                    }
                }
        };
    }

    private static String? GetImageContent(ImageData item)
    {
        if (!String.IsNullOrWhiteSpace(item.Content)) return item.Content;
        if (!String.IsNullOrWhiteSpace(item.Url)) return item.Url;
        if (!String.IsNullOrWhiteSpace(item.B64Json)) return $"data:image/png;base64,{item.B64Json}";

        return null;
    }
    #endregion
}
