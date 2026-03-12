using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>ChatAI API 控制器基类。统一校验登录状态，提供当前用户信息</summary>
[ApiController]
public abstract class ChatApiControllerBase : ControllerBase, IActionFilter
{
    /// <summary>获取当前登录用户编号</summary>
    /// <returns></returns>
    protected static Int32 GetCurrentUserId() => ManageProvider2.User?.ID ?? 0;

    /// <summary>Action 执行前校验登录状态。未标记 AllowAnonymous 的接口要求已登录</summary>
    /// <param name="context">上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 标记了 AllowAnonymous 的接口跳过校验
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()) return;

        if (ManageProvider2.User == null)
        {
            context.Result = new ObjectResult(new { code = "UNAUTHORIZED", message = "未登录，请先登录" })
            {
                StatusCode = 401
            };
        }
    }

    /// <summary>Action 执行后处理</summary>
    /// <param name="context">上下文</param>
    public void OnActionExecuted(ActionExecutedContext context) { }
}
