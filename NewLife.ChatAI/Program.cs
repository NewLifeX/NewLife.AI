using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

services.AddSingleton<IChatApplicationService, DbChatApplicationService>();
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

//app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCube(app.Environment);
//app.UseCubeHome();
app.MapDefaultControllerRoute();
app.MapControllers();

app.UseAuthorization();

app.MapFallbackToFile("chat.html");

app.Run();
