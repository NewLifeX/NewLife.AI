using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace NewLife.ChatAI;

/// <summary>请求语言中间件扩展</summary>
/// <remarks>
/// 语言优先级：<c>X-Language</c>（前端用户设置）→ <c>?lang=</c>（EventSource 无法自定义请求头）→ <c>Accept-Language</c>。
/// 仅切换当前请求的 UI 文化，不影响数字与日期格式；<see cref="Localization.Culture"/> 必须保持为空，否则会固定全局语言。
/// </remarks>
public static class RequestLanguageExtensions
{
    /// <summary>按请求语言设置界面文化，供异常与消息翻译使用</summary>
    /// <param name="app">应用构建器</param>
    /// <returns></returns>
    public static IApplicationBuilder UseRequestLanguage(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var raw = context.Request.Headers["X-Language"].FirstOrDefault()
                ?? context.Request.Query["lang"].FirstOrDefault()
                ?? context.Request.Headers.AcceptLanguage.FirstOrDefault();

            var language = LanguageHelper.NormalizeCulture(raw) ?? raw;

            using var scope = RequestCultureScope.Create(language);
            await next();
        });
    }
}
