using NewLife;
using NewLife.ChatAI;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

// 注册 ChatAI 服务
services.AddChatAI();

services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
services.AddCube();

// CORS：允许前端开发服务器跨域访问
services.AddCors(options =>
{
    options.AddPolicy("DevCors", builder =>
    {
        builder.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("DevCors");

app.UseCube(app.Environment);

app.MapDefaultControllerRoute();
app.MapControllers();

app.UseAuthorization();

// 启用 ChatAI 中间件：嵌入静态资源 + SPA 回退
// redirectToChat: true 表示独立运行，根路径 "/" 自动跳转 "/chat"
app.UseChatAI(redirectToChat: true);

app.Run();
