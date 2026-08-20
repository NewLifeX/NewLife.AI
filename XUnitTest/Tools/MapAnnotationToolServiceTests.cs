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

/// <summary>show_china_map 工具服务测试：markers/highlightProvinces 类型化解析与修复兜底</summary>
[DisplayName("中国地图工具测试")]
public class MapAnnotationToolServiceTests
{
    private static MapAnnotationToolService NewService() => new(XTrace.Log);

    [Fact]
    [DisplayName("AnnotateChinaMap—类型化 markers 渲染 SVG 且坐标有效")]
    public void AnnotateChinaMap_TypedMarkers_RendersSvg()
    {
        var service = NewService();
        var markers = new List<MapMarker>
        {
            new() { Name = "北京", Lat = 39.9, Lng = 116.4, Label = "北京" },
            new() { Name = "深圳", Lat = 22.5, Lng = 114.0 },
        };
        var result = service.AnnotateChinaMap("科技城市", markers);

        Assert.False(result.IsError);
        var node = JsonNode.Parse((String)result)!;
        Assert.Equal("svg", node["kind"]!.GetValue<String>());
        var svg = node["code"]!.GetValue<String>();
        Assert.Contains("北京", svg);
        Assert.Contains("深圳", svg);
    }

    [Fact]
    [DisplayName("AnnotateChinaMap—color 为空时按配色方案轮转")]
    public void AnnotateChinaMap_MissingColor_FallbackColor()
    {
        var service = NewService();
        var markers = new List<MapMarker> { new() { Name = "北京", Lat = 39.9, Lng = 116.4 } };
        var result = service.AnnotateChinaMap("测试", markers, colorScheme: "blue");
        Assert.False(result.IsError);
    }

    [Fact]
    [DisplayName("InvokeAsync—原生 JSON 数组 markers 成功渲染")]
    public async Task InvokeAsync_NativeArray_Succeeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"城市","markers":[{"name":"北京","lat":39.9,"lng":116.4},{"name":"上海","lat":31.2,"lng":121.5}]}""";
        var llm = await registry.InvokeAsync("show_china_map", args);
        Assert.Contains("已渲染中国地图", llm);
    }

    [Fact]
    [DisplayName("InvokeAsync—畸形 markers（元素间多余引号）自动修复并成功")]
    public async Task InvokeAsync_MalformedMarkers_RepairedAndSucceeds()
    {
        var registry = new ToolRegistry();
        registry.AddTools(NewService());

        var args = """{"title":"城市","markers":"[{\"name\":\"北京\",\"lat\":39.9,\"lng\":116.4},\"{\"name\":\"上海\",\"lat\":31.2,\"lng\":121.5}]"}""";
        var llm = await registry.InvokeAsync("show_china_map", args);
        Assert.Contains("已渲染中国地图", llm);
    }
}
