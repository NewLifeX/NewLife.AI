using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

services.AddSingleton<IChatApplicationService, InMemoryChatApplicationService>();

services.AddControllersWithViews();
services.AddCube();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCube(app.Environment);
app.UseCubeHome();

app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
