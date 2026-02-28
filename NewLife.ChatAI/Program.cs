using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

services.AddCube();
services.AddControllers();
services.AddSingleton<IChatApplicationService, InMemoryChatApplicationService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.UseCube(app.Environment);

app.Run();
