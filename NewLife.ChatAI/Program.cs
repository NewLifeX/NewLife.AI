using System.Reflection;
using Microsoft.Extensions.FileProviders;
using NewLife;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

services.AddScoped<ChatApplicationService>();
services.AddSingleton<UsageService>();
services.AddSingleton<GatewayService>();
services.AddSingleton<McpClientService>();
services.AddSingleton<ToolCallService>();
services.AddSingleton<BackgroundGenerationService>();
services.AddHttpClient("McpClient");

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

// 设置嵌入的静态文件提供程序
// MapFallbackToFile 需要使用 env.WebRootFileProvider，所以必须设置它
var env = app.Environment;
var embeddedProvider = new CubeEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "NewLife.ChatAI.wwwroot");

// 合并 UseCube 设置的文件提供者和嵌入资源提供者
// 只有 WebRootPath 存在时才混合，否则只用嵌入资源
if (!env.WebRootPath.IsNullOrEmpty() && Directory.Exists(env.WebRootPath) && env.WebRootFileProvider != null)
{
    env.WebRootFileProvider = new CompositeFileProvider(
        embeddedProvider,           // 优先使用嵌入资源
        env.WebRootFileProvider     // 再使用 Cube 的文件提供者
    );
}
else
{
    env.WebRootFileProvider = embeddedProvider;
}

// 使用默认静态文件中间件（会自动使用 env.WebRootFileProvider）
app.UseStaticFiles();

//app.UseCubeHome();
app.MapDefaultControllerRoute();
app.MapControllers();

app.UseAuthorization();

// 所有未匹配的路由返回 chat.html（现在可以从嵌入资源中找到了）
app.MapFallbackToFile("chat.html");

app.Run();
