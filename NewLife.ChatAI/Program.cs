using System.Text.Json.Serialization;
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
