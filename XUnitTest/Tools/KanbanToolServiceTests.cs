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

/// <summary>show_kanban 工具服务测试：类型化参数解析与修复兜底</summary>
[DisplayName("任务看板工具测试")]
public class KanbanToolServiceTests
{
    private static KanbanToolService NewService() => new(XTrace.Log);

    [Fact]
    [DisplayName("ShowKanban—合法 columns 渲染成功且嵌套结构完整")]
    public void ShowKanban_ValidColumns_RendersKanban()
    {
        var service = NewService();
        var columns = new List<KanbanColumn>
        {
            new()
            {
                Id = "todo", Title = "待办", Color = "#94a3b8", WipLimit = 5,
                Cards = new List<KanbanCard>
                {
                    new() { Id = "1", Title = "需求分析", Priority = "high", Tags = new List<String> { "设计" }, Progress = 60 },
                },
            },
            new() { Id = "done", Title = "已完成" },
        };
        var result = service.ShowKanban("Sprint 1", columns, "board");

        Assert.False(result.IsError);
        var node = JsonNode.Parse((String)result)!;
        var cols = node["columns"]!.AsArray();
        Assert.Equal(2, cols.Count);
        Assert.Equal("todo", cols[0]!["id"]!.GetValue<String>());
        Assert.Equal(5, cols[0]!["wipLimit"]!.GetValue<Int32>());
        Assert.Equal("high", cols[0]!["cards"]![0]!["priority"]!.GetValue<String>());
        Assert.Equal("设计", cols[0]!["cards"]![0]!["tags"]![0]!.GetValue<String>());
        Assert.Equal(60, cols[0]!["cards"]![0]!["progress"]!.GetValue<Int32>());
        // 第二列未填可选字段不输出
        Assert.Null(cols[1]!["color"]);
    }

    [Fact]
    [DisplayName("InvokeAsync—原生 JSON 数组 columns 成功渲染")]
    public async Task InvokeAsync_NativeArray_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"看板","columns":[{"id":"todo","title":"待办","cards":[{"id":"1","title":"任务A","checklist":[{"title":"步骤","done":true}]}]}]}""";
        var llm = await registry.InvokeAsync("show_kanban", args);
        Assert.Contains("已渲染看板", llm);
    }

    [Fact]
    [DisplayName("InvokeAsync—畸形 columns（元素间多余引号）自动修复并成功")]
    public async Task InvokeAsync_MalformedColumns_RepairedAndSucceeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"看板","columns":"[{\"id\":\"todo\",\"title\":\"待办\"},\"{\"id\":\"done\",\"title\":\"完成\"}]"}""";
        var llm = await registry.InvokeAsync("show_kanban", args);
        Assert.Contains("已渲染看板", llm);
    }

    [Fact]
    [DisplayName("ShowKanban—columns 为空抛 ToolException")]
    public void ShowKanban_EmptyColumns_Throws()
    {
        var service = NewService();
        var ex = Assert.Throws<ToolException>(() => service.ShowKanban("标题", []));
        Assert.Contains("columns 不能为空", ex.ForUser);
    }
}
