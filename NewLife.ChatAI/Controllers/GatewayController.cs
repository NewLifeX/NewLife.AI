using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.ChatAI.Filters;
using NewLife.ChatAI.Services;
using ChatMessage = NewLife.AI.Models.ChatMessage;

namespace NewLife.ChatAI.Controllers;

/// <summary>API 网关控制器。对外提供兼容 OpenAI / Anthropic / Gemini 标准协议的统一 API</summary>
/// <remarks>
/// 根据请求中的 model 字段自动路由到对应的模型提供商，
/// 通过 Authorization: Bearer {appkey} 进行认证。
/// </remarks>
[ApiController]
public class GatewayController(GatewayService gatewayService, ModelService modelService, IChatPipeline pipeline, ChatSetting chatSetting) : ControllerBase
{
    #region 模型列表
    /// <summary>列出当前密钥可使用的模型。兼容 OpenAI GET /v1/models 协议</summary>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("v1/models")]
    public IActionResult ListModelsAsync(CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        var models = modelService.GetModelsForAppKey(appKey);

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
                ["support_function_calling"] = m.SupportFunctionCalling,
                ["support_vision"] = m.SupportVision,
                ["support_audio"] = m.SupportAudio,
                ["support_image_generation"] = m.SupportImageGeneration,
                ["support_video_generation"] = m.SupportVideoGeneration,
            };
        }).ToList();

        var result = new Dictionary<String, Object>
        {
            ["object"] = "list",
            ["data"] = data,
        };

        return Content(JsonSerializer.Serialize(result, GatewayService.SnakeCaseOptions), "application/json");
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
        var config = modelService.ResolveModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });
        if (!modelService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        if (!modelService.IsAvailable(config))
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.GetEffectiveProvider()}'" });

        // 通过 ChatCompletions 方式请求图像生成（兼容 OpenAI DALL-E 等通过聊天接口生成图像的场景）
        var size = chatSetting.DefaultImageSize;
        if (body.TryGetValue("size", out var sizeObj) && sizeObj != null)
            size = sizeObj.ToString()!;

        try
        {
            using var imageClient = modelService.CreateClient(config)!;
            var response = await imageClient.GetResponseAsync(
                [new ChatMessage { Role = "user", Content = $"Generate an image: {prompt}. Size: {size}" }],
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

        if (String.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { code = "INVALID_REQUEST", message = "image 文件不能为空" });

        // 路由到模型
        var config = modelService.ResolveModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });
        if (!modelService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        if (!modelService.IsAvailable(config))
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.GetEffectiveProvider()}'" });

        try
        {
            // 读取图片并编码为 base64 data URI
            using var ms = new MemoryStream();
            await imageFile.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var imageBase64 = Convert.ToBase64String(ms.ToArray());
            var mimeType = imageFile.ContentType ?? "image/png";
            var dataUri = $"data:{mimeType};base64,{imageBase64}";

            // 读取 mask 文件（可选）
            var maskFile = form.Files.GetFile("mask");
            String? maskInfo = null;
            if (maskFile != null && maskFile.Length > 0)
            {
                using var maskMs = new MemoryStream();
                await maskFile.CopyToAsync(maskMs, cancellationToken).ConfigureAwait(false);
                maskInfo = $"data:{maskFile.ContentType ?? "image/png"};base64,{Convert.ToBase64String(maskMs.ToArray())}";
            }

            // 构建多模态消息
            var contentParts = new List<Object>
            {
                new { type = "text", text = $"Edit this image: {prompt}. Size: {size}" },
                new { type = "image_url", image_url = new { url = dataUri } },
            };
            if (maskInfo != null)
                contentParts.Add(new { type = "image_url", image_url = new { url = maskInfo } });

            using var editClient = modelService.CreateClient(config)!;
            var response = await editClient.GetResponseAsync(
                [new ChatMessage { Role = "user", Content = contentParts }],
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

    #region 辅助
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

        // 模型路由
        var config = modelService.ResolveModelByCode(request.Model);
        if (config == null)
        {
            await WriteErrorAsync(404, "MODEL_NOT_FOUND", $"未找到模型 '{request.Model}'").ConfigureAwait(false);
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
        UsageDetails? lastUsage = null;

        try
        {
            // 开启对话记录时预创建会话，确保 UsageRecord 可关联到对应会话
            if (enableRecording)
            {
                var conversationId = gatewayService.CreateGatewayConversation(request, config, appKey);
                if (conversationId > 0) request.ConversationId = conversationId.ToString();
            }

            if (request.Stream)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");
                Response.Headers.Append("Connection", "keep-alive");
                Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送

                // 输出流式开始事件（Anthropic 需要 message_start + content_block_start）
                foreach (var sseEvent in GatewayService.FormatStreamStart(request.Model ?? config.Code, protocol))
                {
                    await Response.WriteAsync(sseEvent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                if (chatSetting.EnableGatewayPipeline)
                {
                    // 完整能力管道路径：技能注入 + 工具调用 + 提示词管理
                    var contextMessages = gatewayService.BuildContextMessages(request, appKey, config);
                    var pipelineContext = new ChatPipelineContext { UserId = appKey.UserId.ToString(), ConversationId = request.ConversationId };

                    await foreach (var evt in pipeline.StreamAsync(contextMessages, config, ThinkingMode.Auto, pipelineContext, cancellationToken).ConfigureAwait(false))
                    {
                        // 收集内容用于网关对话记录
                        if (enableRecording)
                        {
                            if (evt.Type == "content_delta")
                                contentBuilder!.Append(evt.Content);
                            else if (evt.Type == "thinking_delta")
                                thinkingBuilder!.Append(evt.Content);
                        }

                        // 收集最后一次用量
                        if (evt.Usage != null) lastUsage = evt.Usage;

                        var evtChunk = GatewayService.ConvertEventToChunk(evt, request.Model ?? config.Code);
                        if (evtChunk != null)
                            await WriteStreamChunkAsync(evtChunk, protocol, cancellationToken).ConfigureAwait(false);
                    }

                    // 管道路径：在此写入用量记录（非管道路径由 ChatStreamAsync 内部写入）
                    if (enableRecording)
                        gatewayService.RecordUsage(appKey, config.Id, request.ConversationId.ToLong(), lastUsage);
                }
                else
                {
                    await foreach (var chunk in gatewayService.ChatStreamAsync(request, config, appKey, cancellationToken).ConfigureAwait(false))
                    {
                        // 收集内容用于网关对话记录
                        if (enableRecording)
                        {
                            var text = chunk.Text;
                            if (text != null) contentBuilder!.Append(text);
                            var thinking = chunk.Messages?.FirstOrDefault()?.Delta?.ReasoningContent;
                            if (thinking != null) thinkingBuilder!.Append(thinking);
                        }

                        // 收集最后一次用量
                        if (chunk.Usage != null) lastUsage = chunk.Usage;

                        await WriteStreamChunkAsync(chunk, protocol, cancellationToken).ConfigureAwait(false);
                    }
                }

                // 输出流式结束标记
                var endMarker = GatewayService.FormatStreamEnd(protocol);
                if (endMarker != null)
                    await Response.WriteAsync(endMarker, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                // 网关对话记录
                if (enableRecording)
                    gatewayService.RecordGatewayConversation(request, config, appKey, contentBuilder!.ToString(), thinkingBuilder!.ToString(), lastUsage);
            }
            else
            {
                var result = await gatewayService.ChatAsync(request, config, appKey, cancellationToken).ConfigureAwait(false);
                Response.ContentType = "application/json";
                await Response.WriteAsync(GatewayService.FormatResponse(result, protocol), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                // 网关对话记录
                if (enableRecording)
                {
                    var thinking = result.Messages?.FirstOrDefault()?.Message?.ReasoningContent;
                    gatewayService.RecordGatewayConversation(request, config, appKey, result.Text, thinking, result.Usage);
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
    #endregion
}
