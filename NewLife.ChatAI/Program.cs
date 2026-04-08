using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Serialization;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

// 注册 ChatAI 服务
services.AddChatAI();

services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        SystemJson.Apply(options.JsonSerializerOptions, true);
    });

// 当 [FromBody] JSON 解析失败时，返回网关统一错误格式 {code, message}，而非 ProblemDetails
services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var firstError = ctx.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault() ?? "请求体格式错误";

        var body = new Dictionary<String, Object>
        {
            ["code"] = "INVALID_REQUEST",
            ["message"] = firstError,
        };
        return new ContentResult
        {
            StatusCode = 400,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(body),
        };
    };
});
services.AddCube();

var app = builder.Build();

app.UseCube(app.Environment);

app.MapDefaultControllerRoute();
app.MapControllers();

app.UseAuthorization();

// 启用 ChatAI 中间件：嵌入静态资源 + SPA 回退
// redirectToChat: true 表示独立运行，根路径 "/" 自动跳转 "/chat"
app.UseChatAI(redirectToChat: true);

app.Run();
