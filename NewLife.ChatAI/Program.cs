using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Log;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddStardust();

services.AddSingleton<IChatApplicationService, DbChatApplicationService>();

services.AddControllersWithViews();
services.AddCube();

var app = builder.Build();

//app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCube(app.Environment);
//app.UseCubeHome();
app.MapDefaultControllerRoute();
//app.MapControllers();

app.UseAuthorization();

app.MapFallbackToFile("chat.html");

app.Run();
