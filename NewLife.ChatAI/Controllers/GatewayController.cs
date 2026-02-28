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
    /// <summary>图像生成接口</summary>
    /// <param name="body">请求体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/images/generations")]
    public async Task<IActionResult> ImageGenerationsAsync([FromBody] IDictionary<String, Object> body, CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        // 图像生成暂返回 501，待后续实现
        return StatusCode(501, new { code = "NOT_IMPLEMENTED", message = "图像生成功能尚未实现" });
    }
    #endregion

    #region 图像编辑
    /// <summary>图像编辑接口</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("v1/images/edits")]
    public async Task<IActionResult> ImageEditsAsync(CancellationToken cancellationToken)
    {
        var appKey = gatewayService.ValidateAppKey(Request.Headers.Authorization);
        if (appKey == null)
            return Unauthorized(new { code = "INVALID_API_KEY", message = "AppKey 无效或已禁用" });

        // 图像编辑暂返回 501，待后续实现
        return StatusCode(501, new { code = "NOT_IMPLEMENTED", message = "图像编辑功能尚未实现" });
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
