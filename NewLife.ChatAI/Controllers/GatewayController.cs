using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatMessage = NewLife.AI.Models.ChatMessage;

namespace NewLife.ChatAI.Controllers;

/// <summary>API 网关控制器。对外提供兼容 OpenAI / Anthropic / Gemini 标准协议的统一 API</summary>
/// <remarks>
/// 根据请求中的 model 字段自动路由到对应的模型提供商，
/// 通过 Authorization: Bearer {appkey} 进行认证。
/// </remarks>
[ApiController]
public class GatewayController(GatewayService gatewayService, IChatPipeline pipeline) : ControllerBase
{
    /// <summary>snake_case 序列化选项。用于写出符合 OpenAI / Anthropic 协议的响应体；请求体的反序列化已由 GatewayJsonInputFormatter 接管</summary>
    private static readonly JsonSerializerOptions _snakeCaseOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #region 模型列表
    /// <summary>列出当前密钥可使用的模型。兼容 OpenAI GET /v1/models 协议</summary>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("v1/models")]
    public IActionResult ListModelsAsync(CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        var models = gatewayService.GetModelsForAppKey(appKey);

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
            };
        }).ToList();

        var result = new Dictionary<String, Object>
        {
            ["object"] = "list",
            ["data"] = data,
        };

        return Content(JsonSerializer.Serialize(result, _snakeCaseOptions), "application/json");
    }
    #endregion

    #region OpenAI Chat Completions
    /// <summary>OpenAI Chat Completions 兼容接口。支持流式和非流式</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("v1/chat/completions")]
    public async Task ChatCompletionsAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(ChatRequest.From(request), cancellationToken).ConfigureAwait(false);
    #endregion

    #region OpenAI Response API
    /// <summary>OpenAI Response API 兼容接口。用于 o3/o4-mini/gpt-5 等推理模型</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>协议格式与 ChatCompletions 完全兼容，复用同一处理逻辑</remarks>
    [HttpPost("v1/responses")]
    public async Task ResponsesAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(ChatRequest.From(request), cancellationToken).ConfigureAwait(false);
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
    public async Task MessagesAsync([FromBody] AnthropicRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request.ToChatRequest(), cancellationToken).ConfigureAwait(false);
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
    public async Task GeminiAsync([FromBody] GeminiRequest request, CancellationToken cancellationToken)
        => await ProcessChatAsync(request.ToChatRequest(), cancellationToken).ConfigureAwait(false);
    #endregion

    #region 图像生成
    /// <summary>图像生成接口。按 model 字段路由到对应的图像生成服务商</summary>
    /// <param name="body">请求体，包含 model/prompt/size/n 等参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/images/generations")]
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
        var config = gatewayService.ResolveModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });
        if (!gatewayService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        var descriptor = gatewayService.GetDescriptor(config);
        if (descriptor == null)
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.GetEffectiveProvider()}'" });

        // 通过 ChatCompletions 方式请求图像生成（兼容 OpenAI DALL-E 等通过聊天接口生成图像的场景）
        var size = ChatSetting.Current.DefaultImageSize;
        if (body.TryGetValue("size", out var sizeObj) && sizeObj != null)
            size = sizeObj.ToString()!;

        try
        {
            var options = GatewayService.BuildOptions(config);
            using var imageClient = descriptor.Factory(options);
            var response = await imageClient.GetResponseAsync(
                [new ChatMessage { Role = "user", Content = $"Generate an image: {prompt}. Size: {size}" }],
                new ChatOptions { Model = config.Code },
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
        var size = form["size"].FirstOrDefault() ?? ChatSetting.Current.DefaultImageSize;
        var imageFile = form.Files.GetFile("image");

        if (String.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { code = "INVALID_REQUEST", message = "image 文件不能为空" });

        // 路由到模型
        var config = gatewayService.ResolveModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });
        if (!gatewayService.IsModelAllowed(appKey, config))
            return StatusCode(403, new { code = "MODEL_FORBIDDEN", message = $"当前密钥无权使用模型 '{modelCode}'" });

        var descriptor = gatewayService.GetDescriptor(config);
        if (descriptor == null)
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

            var options = GatewayService.BuildOptions(config);
            using var editClient = descriptor.Factory(options);
            var response = await editClient.GetResponseAsync(
                [new ChatMessage { Role = "user", Content = contentParts }],
                new ChatOptions { Model = config.Code },
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
    /// <summary>核心对话处理逻辑。认证、模型路由、流式/非流式响应，由各协议端点共用</summary>
    /// <param name="request">已转换为内部统一格式的对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        // 认证校验
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
        {
            await WriteErrorAsync(401, "INVALID_API_KEY", "AppKey 无效或已禁用").ConfigureAwait(false);
            return;
        }

        // 模型路由
        var config = gatewayService.ResolveModelByCode(request.Model);
        if (config == null)
        {
            await WriteErrorAsync(404, "MODEL_NOT_FOUND", $"未找到模型 '{request.Model}'").ConfigureAwait(false);
            return;
        }
        if (!gatewayService.IsModelAllowed(appKey, config))
        {
            await WriteErrorAsync(403, "MODEL_FORBIDDEN", $"当前密钥无权使用模型 '{request.Model}'").ConfigureAwait(false);
            return;
        }

        try
        {
            if (request.Stream)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");
                Response.Headers.Append("Connection", "keep-alive");
                Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送

                if (ChatSetting.Current.EnableGatewayPipeline)
                {
                    // 完整能力管道路径：技能注入 + 工具调用 + 提示词管理
                    var contextMessages = BuildGatewayContextMessages(request, appKey, config);
                    var pipelineContext = new ChatPipelineContext { UserId = appKey.UserId.ToString() };

                    await foreach (var evt in pipeline.StreamAsync(contextMessages, config, ThinkingMode.Auto, pipelineContext, cancellationToken).ConfigureAwait(false))
                    {
                        var evtChunk = ConvertEventToChunk(evt, request.Model ?? config.Code);
                        if (evtChunk != null)
                        {
                            var json = JsonSerializer.Serialize(evtChunk, _snakeCaseOptions);
                            await Response.WriteAsync($"data: {json}\n\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    await foreach (var chunk in gatewayService.ChatStreamAsync(request, config, appKey, cancellationToken).ConfigureAwait(false))
                    {
                        var json = JsonSerializer.Serialize(chunk, _snakeCaseOptions);
                        await Response.WriteAsync($"data: {json}\n\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await Response.WriteAsync("data: [DONE]\n\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var result = await gatewayService.ChatAsync(request, config, appKey, cancellationToken).ConfigureAwait(false);
                Response.ContentType = "application/json";
                await Response.WriteAsync(JsonSerializer.Serialize(result, _snakeCaseOptions), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
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
    }

    /// <summary>为网关请求构建上下文消息列表。注入系统提示词（用户信息+UserSetting+ModelConfig），过滤请求中原有系统消息</summary>
    /// <param name="request">网关请求</param>
    /// <param name="appKey">应用密钥</param>
    /// <param name="config">模型配置</param>
    /// <returns>上下文消息列表</returns>
    private IList<AiChatMessage> BuildGatewayContextMessages(ChatRequest request, AppKey appKey, ModelConfig config)
    {
        var messages = new List<AiChatMessage>();

        // 构建系统消息（包含用户信息 + UserSetting + ModelConfig SystemPrompt）
        var sysMsg = gatewayService.BuildSystemMessage(appKey, config);
        if (sysMsg != null) messages.Add(sysMsg);

        // 添加请求中的对话消息（跳过系统消息，已由管道注入）
        foreach (var msg in request.Messages ?? [])
        {
            if (msg.Role?.Equals("system", StringComparison.OrdinalIgnoreCase) == true) continue;
            messages.Add(msg);
        }

        return messages;
    }

    /// <summary>将 ChatStreamEvent 转换为 OpenAI 兼容的 ChatResponse 流式块</summary>
    /// <param name="evt">管道事件</param>
    /// <param name="model">模型编码</param>
    /// <returns>ChatResponse；不需要输出的事件返回 null</returns>
    private static ChatResponse? ConvertEventToChunk(ChatStreamEvent evt, String? model)
    {
        var chunk = new ChatResponse
        {
            Object = "chat.completion.chunk",
            Model = model,
            Created = DateTimeOffset.UtcNow,
        };

        switch (evt.Type)
        {
            case "content_delta":
                chunk.AddDelta(evt.Content);
                return chunk;
            case "thinking_delta":
                chunk.AddDelta(null, evt.Content);
                return chunk;
            case "message_done":
                chunk.AddDelta(null, finishReeason: "stop");
                if (evt.Usage != null) chunk.Usage = evt.Usage;
                return chunk;
            default:
                return null;
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

        await Response.WriteAsync(JsonSerializer.Serialize(error, _snakeCaseOptions), Encoding.UTF8).ConfigureAwait(false);
    }
    #endregion
}
