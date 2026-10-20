// NewLife.AI.Extensions + ASP.NET Core 的 NativeAOT 冒烟：Kestrel 起真服务，走完整 MCP HTTP 往返。
// 关注点：MapMcp 的 RequestDelegate 路由、DI 解析工具实例、JSON-RPC 请求反序列化在 AOT 下是否可用。
// 同一工程可 JIT 运行（dotnet run）与 NativeAOT 发布运行，任一断言失败以非 0 退出码结束。
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NewLife.AI.Tools;

var failures = new List<String>();

void Check(String name, Boolean ok, String? detail = null)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(detail == null ? null : $" ({detail})")}");
    if (!ok) failures.Add(name);
}

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddMcp<SmokeTools>();

var app = builder.Build();
app.MapMcp<SmokeTools>("/mcp");

await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
Check("1.Kestrel 已监听", !String.IsNullOrEmpty(address), address);

using var http = new HttpClient { BaseAddress = new Uri(address!) };
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

async Task<String> PostAsync(String name, String payload)
{
    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
    using var response = await http.PostAsync("/mcp", content);
    var body = await response.Content.ReadAsStringAsync();
    Check(name, response.IsSuccessStatusCode, $"{(Int32)response.StatusCode}");
    return body;
}

// 1. initialize 握手
var init = await PostAsync("2.MCP initialize", """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"aot-web-smoke","version":"1.0"}}}""");
Check("2.MCP initialize 响应", init.Contains("protocolVersion") && init.Contains("serverInfo"));

// 2. tools/list 反射枚举工具方法
var list = await PostAsync("3.MCP tools/list", """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
Check("3.MCP tools/list 含 echo", list.Contains("echo"));

// 3. tools/call 经 DI 解析工具实例并反射调用
var call = await PostAsync("4.MCP tools/call", """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"aot"}}}""");
Check("4.MCP tools/call 返回结果", call.Contains("echo:aot"));

// 4. 未知方法返回 JSON-RPC 错误而不是崩溃
var unknown = await PostAsync("5.未知方法", """{"jsonrpc":"2.0","id":4,"method":"no/such","params":{}}""");
Check("5.未知方法返回错误", unknown.Contains("error") || unknown.Contains("-32601"));

await app.StopAsync();

Console.WriteLine();
if (failures.Count > 0)
{
    Console.WriteLine($"FAILED {failures.Count}: {String.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("ALL PASS");
return 0;

/// <summary>MCP 工具。通过 DI 注册，工具实例由请求作用域解析</summary>
public sealed class SmokeTools
{
    /// <summary>回显文本</summary>
    /// <param name="text">待回显文本</param>
    /// <returns>带前缀的回显结果</returns>
    [ToolDescription("echo")]
    [Description("Echo text")]
    public String Echo([Description("Text to echo")] String text) => $"echo:{text}";

    /// <summary>探活</summary>
    /// <returns>固定字符串</returns>
    [ToolDescription("ping")]
    [Description("Health probe")]
    public Task<String> PingAsync() => Task.FromResult("pong");
}
