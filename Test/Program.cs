using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Coding;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.Log;

namespace Test;

class Program
{
    static void Main(String[] args)
    {
        XTrace.UseConsole();

        try
        {
            //Test1();
            CodingAgentDemo().Wait();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        Console.WriteLine("OK!");
        Console.ReadKey();
    }

    static async void Test1()
    {
        XTrace.WriteLine("阿里百炼测试开始……");
        var apiKey = File.ReadAllText("..\\UnitTest\\config\\DashScope.key").Trim();

        //var client = AiClientRegistry.Default.CreateClient("DashScope", new AiClientOptions { ApiKey = apiKey, Model = "qwen3.5-flash" });
        apiKey = "sk-NewLifeAI2026";
        var client = new NewLifeAIChatClient(apiKey, "qwen3.5-flash", "http://localhost:5080");

        // 发送单条消息，直接返回回复文本
        var reply = await client.ChatAsync("你好，请介绍一下你自己");
        XTrace.WriteLine(reply);

        // 以元组数组传入多角色消息，无需构造 ChatMessage 对象，每项为 (role, content)
        var reply2 = client.StreamChatAsync([
            ("system", "你是一名专业的 C# 开发助手"),
            ("user", "请解释什么是依赖注入"),
        ]);
        //XTrace.WriteLine(reply2);
        // 流式回复，边生成边输出
        await foreach (var chunk in reply2)
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?.Content is String text && !String.IsNullOrEmpty(text))
                Console.Write(text);
        }
        Console.WriteLine();

        //// 流式回复，边生成边输出
        //await foreach (var chunk in client.CompleteStreamingAsync("解释一下量子计算"))
        //{
        //    var delta = chunk.Choices?.FirstOrDefault()?.Delta;
        //    if (delta?.Content is String text && !String.IsNullOrEmpty(text))
        //        Console.Write(delta.Content);
        //}

        // 视觉理解：将图片与文字问题组合为多模态消息
        XTrace.WriteLine("多模态测试（图片分析）……");
        var message = new ChatMessage
        {
            Role = "user",
            Contents = [new ImageContent {
                Uri = "https://newlifex.com/images/7438810624576892928.jpeg" },
                new TextContent("请描述这张图片的内容"),
            ]
        };
        //var response = await client.CompleteAsync([message]);
        //XTrace.WriteLine(response.Text);
        // 流式回复，边生成边输出
        await foreach (var chunk in client.StreamChatAsync([message]))
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?.Content is String text && !String.IsNullOrEmpty(text))
                Console.Write(text);
        }
        Console.WriteLine();

        XTrace.WriteLine("阿里百炼测试完成！");
    }

    /// <summary>编程智能体示例。演示 ACP 三阶段管道</summary>
    static async Task CodingAgentDemo()
    {
        XTrace.WriteLine("编程智能体示例开始……");

        var apiKey = "sk-NewLifeAI2026";
        var baseClient = new NewLifeAIChatClient(apiKey, "qwen3.5-flash", "http://localhost:5080");

        // 使用 Test 项目自身的工作区
        var workspacePath = Environment.CurrentDirectory;
        XTrace.WriteLine($"工作区: {workspacePath}");

        var tools = new CodingTools(workspacePath);

        // 创建 CodingAgent，自动加载 Copilot 技能
        var agent = new CodingAgent(baseClient, tools, workspacePath);

        // 订阅事件
        agent.OnPhaseChanged += (phase, msg) => XTrace.WriteLine($"[{phase}] {msg}");
        agent.OnToolCall += (phase, args) =>
        {
            var icon = args.IsError ? "❌" : "🔧";
            var target = args.Arguments?.Length > 100 ? args.Arguments[..100] + "..." : args.Arguments;
            XTrace.WriteLine($"  {icon} [{phase}] {args.ToolName}({target}) → {args.ResultSummary ?? "(无摘要)"} ({args.ElapsedMs}ms)");
        };

        // 先用简单演示需求
        var requirement = "分析当前项目的 Program.cs 文件结构，并给出代码改进建议（不要实际修改文件）";

        XTrace.WriteLine($"需求: {requirement}");
        XTrace.WriteLine("开始 ACP 管道……");

        var report = await agent.RunAsync(requirement);

        if (report.Plan != null)
        {
            XTrace.WriteLine($"规划摘要: {report.Plan.Summary}");
            XTrace.WriteLine($"任务数量: {report.Plan.Tasks.Count}");
        }

        foreach (var taskResult in report.TaskResults)
        {
            var status = taskResult.Passed ? "通过" : "未通过";
            XTrace.WriteLine($"任务 [{taskResult.Task.Id}]: {status}");

            // 输出分析/实现结果
            if (!String.IsNullOrEmpty(taskResult.Code))
            {
                XTrace.WriteLine($"--- [{taskResult.Task.Id}] 输出 ---");
                XTrace.WriteLine(taskResult.Code);
                XTrace.WriteLine($"--- [{taskResult.Task.Id}] 结束 ---");
            }

            if (taskResult.Review?.Issues is { Count: > 0 })
            {
                XTrace.WriteLine($"审查问题 ({taskResult.Review.Issues.Count}):");
                foreach (var issue in taskResult.Review.Issues)
                {
                    XTrace.WriteLine($"  [{issue.Severity}] {issue.Description}");
                }
            }

            if (!String.IsNullOrEmpty(taskResult.Error))
                XTrace.WriteLine($"错误: {taskResult.Error}");
        }

        if (report.Error != null)
            XTrace.WriteLine($"错误: {report.Error}");

        XTrace.WriteLine("编程智能体示例完成！");
    }
}