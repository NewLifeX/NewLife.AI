using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Tools;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>build_doc/build_excel/build_ppt 类型化参数测试：
/// 归档步骤依赖附件库（DB），故用「回显工具」验证类型化数组转换机制，另验证参数缺失错误路径</summary>
[DisplayName("文档生成工具测试")]
public class BuildToolTests
{
    /// <summary>回显工具：接收类型化数组参数并返回摘要，验证 ConvertValue 的数组→Model 转换</summary>
    private sealed class BuildModelEchoTool
    {
        [ToolDescription("echo_sheets")]
        public String EchoSheets(ExcelSheetModel[]? sheets)
            => sheets == null ? "null" : $"{sheets.Length}:{sheets[0].Name}";

        [ToolDescription("echo_sections")]
        public String EchoSections(DocSectionModel[]? sections)
            => sections == null ? "null" : $"{sections.Length}:{sections[0].Heading}";

        [ToolDescription("echo_slides")]
        public String EchoSlides(PptPageModel[]? slides)
            => slides == null ? "null" : $"{slides.Length}:{slides[0].Title}";
    }

    private static ToolRegistry NewRegistry() => new ToolRegistry();

    [Fact]
    [DisplayName("InvokeAsync—原生数组转为 ExcelSheetModel[]")]
    public async Task InvokeAsync_SheetsArray_Converts()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildModelEchoTool());

        var result = await registry.InvokeAsync("echo_sheets", """{"sheets":[{"name":"Q1","headers":["a"],"rows":[["1"]]}]}""");
        Assert.Equal("1:Q1", result);
    }

    [Fact]
    [DisplayName("InvokeAsync—原生数组转为 DocSectionModel[]")]
    public async Task InvokeAsync_SectionsArray_Converts()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildModelEchoTool());

        var result = await registry.InvokeAsync("echo_sections", """{"sections":[{"heading":"总结","content":"正文"}]}""");
        Assert.Equal("1:总结", result);
    }

    [Fact]
    [DisplayName("InvokeAsync—原生数组转为 PptPageModel[]（含嵌套元素）")]
    public async Task InvokeAsync_SlidesArray_Converts()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildModelEchoTool());

        var result = await registry.InvokeAsync("echo_slides",
            """{"slides":[{"title":"封面","elements":[{"type":"text","role":"title","content":"Q2"}]}]}""");
        Assert.Equal("1:封面", result);
    }

    [Fact]
    [DisplayName("InvokeAsync—LLM 传 JSON 字符串数组（Qwen 兼容）也能转换")]
    public async Task InvokeAsync_StringWrappedArray_Converts()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildModelEchoTool());

        var result = await registry.InvokeAsync("echo_sheets", """{"sheets":"[{\"name\":\"Q1\"}]"}""");
        Assert.Equal("1:Q1", result);
    }

    // ── 真实工具：参数缺失错误路径（不触达归档/DB）──────────────────────────

    [Fact]
    [DisplayName("build_excel—缺少 sheets 抛 ToolException")]
    public async Task BuildExcel_MissingSheets_Throws()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildExcelToolService(XTrace.Log));

        var ex = await Assert.ThrowsAsync<ToolException>(() => registry.InvokeAsync("build_excel", """{"title":"报表"}"""));
        Assert.Contains("sheets 不能为空", ex.ForUser);
    }

    [Fact]
    [DisplayName("build_doc—缺少 sections 抛 ToolException")]
    public async Task BuildDoc_MissingSections_Throws()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildDocToolService(XTrace.Log));

        var ex = await Assert.ThrowsAsync<ToolException>(() => registry.InvokeAsync("build_doc", """{"title":"文档"}"""));
        Assert.Contains("sections 不能为空", ex.ForUser);
    }

    [Fact]
    [DisplayName("build_ppt—缺少 slides 与 widgetSrc 抛 ToolException")]
    public async Task BuildPpt_MissingBoth_Throws()
    {
        var registry = NewRegistry();
        registry.AddTools(new BuildPptToolService(XTrace.Log));

        var ex = await Assert.ThrowsAsync<ToolException>(() => registry.InvokeAsync("build_ppt", """{"title":"汇报"}"""));
        Assert.Contains("slides 与 widgetSrc 必须提供其一", ex.ForUser);
    }
}
