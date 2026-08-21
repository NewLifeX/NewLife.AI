using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using NewLife.ChatAI.Tools;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>show_timeline 工具服务测试：类型化参数解析、JSON 修复兜底与错误路径</summary>
[DisplayName("时间轴工具测试")]
public class TimelineToolServiceTests
{
    private static TimelineToolService NewService() => new(XTrace.Log);

    private static IList<TimelineItem> BuildItems() => new List<TimelineItem>
    {
        new() { Date = "2017", Title = "Transformer 架构诞生", Description = "自注意力机制", Color = "#6366F1", Category = "里程碑" },
        new() { Date = "2022", Title = "ChatGPT 引爆热潮" },
    };

    // ── 直接调用 ────────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("ShowTimeline—合法 items 渲染成功且输出结构完整")]
    public void ShowTimeline_ValidItems_RendersTimeline()
    {
        var service = NewService();
        var result = service.ShowTimeline("AI 里程碑", BuildItems(), "vertical", ["#6366F1", "#0EA5E9"], "relaxed");

        Assert.False(result.IsError);
        String json = result;
        var node = JsonNode.Parse(json)!;
        Assert.NotNull(node["timelineId"]);
        Assert.Equal("AI 里程碑", node["title"]!.GetValue<String>());
        var items = node["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.Equal("2017", items[0]!["date"]!.GetValue<String>());
        Assert.Equal("Transformer 架构诞生", items[0]!["title"]!.GetValue<String>());
        Assert.Equal("#6366F1", items[0]!["color"]!.GetValue<String>());
        Assert.Equal("里程碑", items[0]!["category"]!.GetValue<String>());
        // 未填的可选字段不写入
        Assert.Null(items[1]!["color"]);
        Assert.Equal("vertical", node["layout"]!.GetValue<String>());
        Assert.Equal(2, node["palette"]!.AsArray().Count);
    }

    [Fact]
    [DisplayName("ShowTimeline—items 为空抛 ToolException")]
    public void ShowTimeline_EmptyItems_Throws()
    {
        var service = NewService();
        var ex = Assert.Throws<ToolException>(() => service.ShowTimeline("标题", []));
        Assert.Contains("items 不能为空", ex.ForUser);
    }

    // ── ToolRegistry 全链路：类型化参数解析 ─────────────────────────────────

    [Fact]
    [DisplayName("InvokeAsync—原生 JSON 数组 items 成功渲染并透传 toolCallId")]
    public async Task InvokeAsync_NativeArray_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var ctx = new ToolCallContext { ToolCallId = "call_t1" };
        var args = """{"title":"测试","items":[{"date":"2024","title":"里程碑A","description":"说明"}],"layout":"vertical"}""";
        var llm = await registry.InvokeAsync("show_timeline", args, ctx);

        Assert.Contains("已渲染时间轴", llm);
        var forUser = ctx.ToolResult!.Contents.First(c => c.Audience.HasFlag(ToolAudience.User)).Data;
        var node = JsonNode.Parse(forUser)!;
        Assert.Equal("call_t1", node["timelineId"]!.GetValue<String>());
        Assert.Equal("2024", node["items"]![0]!["date"]!.GetValue<String>());
        Assert.Equal("里程碑A", node["items"]![0]!["title"]!.GetValue<String>());
    }

    [Fact]
    [DisplayName("InvokeAsync—LLM 传 JSON 字符串 items（Qwen 兼容）也能解析")]
    public async Task InvokeAsync_StringWrappedArray_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"测试","items":"[{\"date\":\"2024\",\"title\":\"里程碑A\"}]"}""";
        var llm = await registry.InvokeAsync("show_timeline", args);
        Assert.Contains("已渲染时间轴", llm);
    }

    [Fact]
    [DisplayName("InvokeAsync—畸形 items（元素间多余引号）自动修复并成功")]
    public async Task InvokeAsync_MalformedItems_RepairedAndSucceeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        // 事故场景还原：第二个元素前多了引号 },"{
        var args = """{"title":"测试","items":"[{\"date\":\"2024\",\"title\":\"A\"},\"{\"date\":\"2025\",\"title\":\"B\"}]"}""";
        var llm = await registry.InvokeAsync("show_timeline", args);
        Assert.Contains("已渲染时间轴", llm);
    }

    [Fact]
    [DisplayName("InvokeAsync—items 缺失走工具校验抛 ToolException")]
    public async Task InvokeAsync_MissingItems_Throws()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var ex = await Assert.ThrowsAsync<ToolException>(() => registry.InvokeAsync("show_timeline", """{"title":"测试"}"""));
        Assert.Contains("items 不能为空", ex.ForUser);
    }

    // ── Schema ─────────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("工具 Schema—items 参数升级为 array 且含元素属性")]
    public void Schema_Items_IsArray()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var tool = registry.Tools.First(t => t.Function!.Name == "show_timeline");
        var parameters = tool.Function!.Parameters as Dictionary<String, Object>;
        var props = parameters!["properties"] as Dictionary<String, Object>;
        var itemsSchema = props!["items"] as Dictionary<String, Object>;
        Assert.Equal("array", itemsSchema!["type"]);

        var itemSchema = itemsSchema["items"] as Dictionary<String, Object>;
        var itemProps = itemSchema!["properties"] as Dictionary<String, Object>;
        Assert.Contains("date", itemProps!.Keys);
        Assert.Contains("title", itemProps.Keys);
        Assert.Contains("description", itemProps.Keys);
    }

    // ── JsonSerializerOptions TypeInfoResolver 回归（生产事故：show_timeline 可选字段触发） ──

    [Fact]
    [DisplayName("工具序列化选项—含 JsonValueCustomized 节点时 ToJsonString 不抛 TypeInfoResolver 错")]
    public void ToolJsonOptions_HasTypeInfoResolver_NoThrowOnCustomJsonValue()
    {
        // 复现 2026-08-21 生产事故：JsonValue.Create(非原始类型) 产生 JsonValueCustomized，
        // 若 ToJsonString 使用无 TypeInfoResolver 的 JsonSerializerOptions，会抛
        // "JsonSerializerOptions instance must specify a TypeInfoResolver setting before being marked as read-only."
        var custom = JsonValue.Create(new Dictionary<String, Object> { ["k"] = "v" })!;
        var result = new JsonObject
        {
            ["timelineId"] = "tl_test",
            ["title"] = "测试",
            ["extra"] = custom,
        };

        // 工具服务同款写法：从 JsonSerializerOptions.Default 派生（自带 TypeInfoResolver）
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var json = result.ToJsonString(writeOptions);
        Assert.Contains("tl_test", json);
        Assert.Contains("k", json);
    }

    [Fact]
    [DisplayName("InvokeAsync—完整生产参数（category+color+palette）渲染成功")]
    public async Task InvokeAsync_FullProductionArgs_Succeeds()
    {
        // 还原 2026-08-21 事故请求：items 携带 category/color，另带 palette 数组
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"AI大模型与产业演进里程碑","items":[{"date":"2017-06","title":"Transformer架构诞生","description":"自注意力机制","category":"技术奠基","color":"#2563eb"},{"date":"2022-11","title":"ChatGPT 横空出世","description":"AI走向大众","category":"应用爆发","color":"#059669"}],"layout":"alternating-bottom","density":"relaxed","palette":["#2563eb","#0891b2","#059669"]}""";
        var llm = await registry.InvokeAsync("show_timeline", args);

        Assert.Contains("已渲染时间轴", llm);
    }
}
