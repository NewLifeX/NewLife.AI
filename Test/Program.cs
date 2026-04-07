using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Log;

namespace Test;

class Program
{
    static void Main(String[] args)
    {
        XTrace.UseConsole();

        try
        {
            Test1();
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
}