using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using NewLife.ChatAI.Tools;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>show_mindmap 工具服务测试：branchColors/collapsed 类型化解析</summary>
[DisplayName("思维导图工具测试")]
public class MindmapToolServiceTests
{
    private static MindmapToolService NewService() => new(XTrace.Log);

    [Fact]
    [DisplayName("ShowMindmap—类型化 branchColors/collapsed 写入返回 JSON")]
    public void ShowMindmap_TypedColorsAndCollapsed_Written()
    {
        var service = NewService();
        var result = service.ShowMindmap(
            "AI 体系",
            "# AI\n## 机器学习",
            "tree",
            ["#3b82f6", "#10b981"],
            ["n2", "n5"],
            maxDepth: 2);

        Assert.False(result.IsError);
        var node = JsonNode.Parse((String)result)!;
        Assert.Equal("AI 体系", node["title"]!.GetValue<String>());
        Assert.Equal(2, node["branchColors"]!.AsArray().Count);
        Assert.Equal("#3b82f6", node["branchColors"]![0]!.GetValue<String>());
        Assert.Equal("n5", node["collapsed"]![1]!.GetValue<String>());
    }

    [Fact]
    [DisplayName("InvokeAsync—原生 JSON 数组 branchColors 成功渲染")]
    public async Task InvokeAsync_NativeArray_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"AI","outline":"# AI\n## 机器学习","branchColors":["#3b82f6","#10b981"],"collapsed":["n2"]}""";
        var llm = await registry.InvokeAsync("show_mindmap", args);
        Assert.Contains("已渲染思维导图", llm);
    }

    [Fact]
    [DisplayName("InvokeAsync—LLM 传 JSON 字符串 branchColors（Qwen 兼容）也能解析")]
    public async Task InvokeAsync_StringWrappedColors_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"AI","outline":"# AI","branchColors":"[\"#3b82f6\",\"#10b981\"]"}""";
        var llm = await registry.InvokeAsync("show_mindmap", args);
        Assert.Contains("已渲染思维导图", llm);
    }

    [Fact]
    [DisplayName("ShowMindmap—outline 为空抛 ToolException")]
    public void ShowMindmap_EmptyOutline_Throws()
    {
        var service = NewService();
        var ex = Assert.Throws<ToolException>(() => service.ShowMindmap("标题", ""));
        Assert.Contains("outline 不能为空", ex.ForUser);
    }
}
