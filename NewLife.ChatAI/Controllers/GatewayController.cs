using System.Text;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;
using NewLife.Serialization;

namespace NewLife.ChatAI.Controllers;

/// <summary>API 网关控制器。对外提供兼容 OpenAI / Anthropic / Gemini 标准协议的统一 API</summary>
/// <remarks>
/// 根据请求中的 model 字段自动路由到对应的模型提供商，
/// 通过 Authorization: Bearer {appkey} 进行认证。
/// </remarks>
[ApiController]
public class GatewayController(GatewayService gatewayService) : ControllerBase
{
    #region OpenAI Chat Completions
    /// <summary>OpenAI Chat Completions 兼容接口。支持流式和非流式</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/chat/completions")]
    public async Task ChatCompletionsAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        // 认证校验
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
        {
            await WriteErrorAsync(401, "INVALID_API_KEY", "AppKey 无效或已禁用").ConfigureAwait(false);
            return;
        }

        // 模型路由
        var config = gatewayService.ResolveModel(request.Model);
        if (config == null)
        {
            await WriteErrorAsync(404, "MODEL_NOT_FOUND", $"未找到模型 '{request.Model}'").ConfigureAwait(false);
            return;
        }

        try
        {
            if (request.Stream)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");
                Response.Headers.Append("Connection", "keep-alive");

                await foreach (var chunk in gatewayService.ChatStreamAsync(request, config, appKey, cancellationToken).ConfigureAwait(false))
                {
                    var json = chunk.ToJson();
                    await Response.WriteAsync($"data: {json}\n\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                await Response.WriteAsync("data: [DONE]\n\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var result = await gatewayService.ChatAsync(request, config, appKey, cancellationToken).ConfigureAwait(false);
                Response.ContentType = "application/json";
                var json = result.ToJson();
                await Response.WriteAsync(json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
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
    #endregion

    #region OpenAI Response API
    /// <summary>OpenAI Response API 兼容接口。用于 o3/o4-mini/gpt-5 等推理模型</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/responses")]
    public async Task ResponsesAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        // 复用 Chat Completions 逻辑
        await ChatCompletionsAsync(request, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Anthropic Messages API
    /// <summary>Anthropic Messages API 兼容接口</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/messages")]
    public async Task MessagesAsync([FromBody] ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        // 内部走统一的 ChatCompletionRequest 模型，由 Provider 负责协议转换
        await ChatCompletionsAsync(request, cancellationToken).ConfigureAwait(false);
    }
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
        var config = gatewayService.ResolveModel(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });

        var provider = gatewayService.GetProvider(config);
        if (provider == null)
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.Provider}'" });

        // 通过 ChatCompletions 方式请求图像生成（兼容 OpenAI DALL-E 等通过聊天接口生成图像的场景）
        var size = ChatSetting.Current.DefaultImageSize;
        if (body.TryGetValue("size", out var sizeObj) && sizeObj != null)
            size = sizeObj.ToString()!;

        try
        {
            var request = new ChatCompletionRequest
            {
                Model = config.Code,
                Messages =
                [
                    new AI.Models.ChatMessage { Role = "user", Content = $"Generate an image: {prompt}. Size: {size}" }
                ],
            };
            var options = GatewayService.BuildOptions(config);
            var response = await provider.ChatAsync(request, options, cancellationToken).ConfigureAwait(false);

            return Ok(new
            {
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = response.Choices?.FirstOrDefault()?.Message?.Content,
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
        var config = gatewayService.ResolveModel(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });

        var provider = gatewayService.GetProvider(config);
        if (provider == null)
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.Provider}'" });

        try
        {
            // 读取图片并编码为 base64 data URI
            using var ms = new System.IO.MemoryStream();
            await imageFile.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var imageBase64 = Convert.ToBase64String(ms.ToArray());
            var mimeType = imageFile.ContentType ?? "image/png";
            var dataUri = $"data:{mimeType};base64,{imageBase64}";

            // 读取 mask 文件（可选）
            var maskFile = form.Files.GetFile("mask");
            String? maskInfo = null;
            if (maskFile != null && maskFile.Length > 0)
            {
                using var maskMs = new System.IO.MemoryStream();
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

            var request = new ChatCompletionRequest
            {
                Model = config.Code,
                Messages =
                [
                    new AI.Models.ChatMessage { Role = "user", Content = contentParts }
                ],
            };
            var options = GatewayService.BuildOptions(config);
            var response = await provider.ChatAsync(request, options, cancellationToken).ConfigureAwait(false);

            return Ok(new
            {
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = response.Choices?.FirstOrDefault()?.Message?.Content,
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
    /// <summary>写入错误响应</summary>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="code">错误码</param>
    /// <param name="message">错误描述</param>
    /// <returns></returns>
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

        var json = error.ToJson();
        await Response.WriteAsync(json, Encoding.UTF8).ConfigureAwait(false);
    }
    #endregion
}
