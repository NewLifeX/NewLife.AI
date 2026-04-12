using System.ComponentModel;
using System.Linq;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

public class ArtifactDetectorTests
{
    [Fact]
    [DisplayName("普通文本—无代码块不触发 Artifact")]
    public void PlainText_NoArtifact()
    {
        var detector = new ArtifactDetector();
        var events = detector.Process("Hello world, this is plain text.");
        Assert.Single(events);
        Assert.Equal(ArtifactEventKind.Normal, events[0].Kind);
        Assert.Equal("Hello world, this is plain text.", events[0].Content);
    }

    [Fact]
    [DisplayName("HTML代码块—触发 ArtifactStart 和 ArtifactEnd")]
    public void HtmlCodeBlock_TriggersArtifact()
    {
        var detector = new ArtifactDetector();

        // 开头栅栏
        var events1 = detector.Process("```html\n");
        Assert.True(events1.Any(e => e.Kind == ArtifactEventKind.ArtifactStart));
        var start = events1.First(e => e.Kind == ArtifactEventKind.ArtifactStart);
        Assert.Equal("html", start.Language);

        // 代码内容
        var events2 = detector.Process("<div>Hello</div>");
        Assert.True(events2.Any(e => e.Kind == ArtifactEventKind.ArtifactDelta));
        var delta = events2.First(e => e.Kind == ArtifactEventKind.ArtifactDelta);
        Assert.Equal("<div>Hello</div>", delta.Content);

        // 关闭栅栏
        var events3 = detector.Process("\n```");
        Assert.True(events3.Any(e => e.Kind == ArtifactEventKind.ArtifactEnd));
    }

    [Fact]
    [DisplayName("SVG代码块—正确识别为可预览")]
    public void SvgCodeBlock_Previewable()
    {
        var detector = new ArtifactDetector();

        var events = detector.Process("```svg\n<svg></svg>\n```");
        Assert.True(events.Any(e => e.Kind == ArtifactEventKind.ArtifactStart && e.Language == "svg"));
        Assert.True(events.Any(e => e.Kind == ArtifactEventKind.ArtifactEnd));
    }

    [Fact]
    [DisplayName("Mermaid代码块—正确识别为可预览")]
    public void MermaidCodeBlock_Previewable()
    {
        var detector = new ArtifactDetector();

        var events = detector.Process("```mermaid\ngraph TD\n  A-->B\n```");
        Assert.True(events.Any(e => e.Kind == ArtifactEventKind.ArtifactStart && e.Language == "mermaid"));
        Assert.True(events.Any(e => e.Kind == ArtifactEventKind.ArtifactEnd));
    }

    [Fact]
    [DisplayName("Python代码块—非可预览不触发 Artifact")]
    public void PythonCodeBlock_NotPreviewable()
    {
        var detector = new ArtifactDetector();

        var events = detector.Process("```python\nprint('hello')\n```");
        // 不应产生 ArtifactStart
        Assert.DoesNotContain(events, e => e.Kind == ArtifactEventKind.ArtifactStart);
    }

    [Fact]
    [DisplayName("流式分割—反引号跨 delta 仍能识别")]
    public void StreamingSplit_BackticksAcrossDelta()
    {
        var detector = new ArtifactDetector();

        // 第一个 delta 只有部分反引号
        var e1 = detector.Process("``");
        // 第二个 delta 完成栅栏
        var e2 = detector.Process("`html\n");

        var allEvents = e1.Concat(e2).ToList();
        Assert.True(allEvents.Any(e => e.Kind == ArtifactEventKind.ArtifactStart && e.Language == "html"));
    }

    [Fact]
    [DisplayName("VideoModels—DTO 属性正确赋值")]
    public void VideoGenerationRequest_Properties()
    {
        var req = new NewLife.AI.Clients.OpenAI.VideoGenerationRequest
        {
            Model = "wan2.1-t2v-turbo",
            Prompt = "一只猫在太空漫步",
            Size = "1280*720",
            Duration = 5,
        };
        Assert.Equal("wan2.1-t2v-turbo", req.Model);
        Assert.Equal("一只猫在太空漫步", req.Prompt);
        Assert.Equal("1280*720", req.Size);
        Assert.Equal(5, req.Duration);
    }

    [Fact]
    [DisplayName("VideoTaskSubmitResponse—属性默认 null")]
    public void VideoTaskSubmitResponse_DefaultsNull()
    {
        var resp = new NewLife.AI.Clients.OpenAI.VideoTaskSubmitResponse();
        Assert.Null(resp.TaskId);
        Assert.Null(resp.Status);
        Assert.Null(resp.RequestId);
    }

    [Fact]
    [DisplayName("VideoTaskStatusResponse—属性赋值正确")]
    public void VideoTaskStatusResponse_Properties()
    {
        var resp = new NewLife.AI.Clients.OpenAI.VideoTaskStatusResponse
        {
            TaskId = "task-123",
            Status = "SUCCEEDED",
            VideoUrls = ["https://example.com/video.mp4"],
        };
        Assert.Equal("task-123", resp.TaskId);
        Assert.Equal("SUCCEEDED", resp.Status);
        Assert.Single(resp.VideoUrls!);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—新字段顺序：核心在前多模态在后")]
    public void AiProviderCapabilities_FieldOrder()
    {
        // 位置参数按新顺序：Thinking, FunctionCalling, Vision, Audio, ImageGen, VideoGen
        var caps = new NewLife.AI.Clients.AiProviderCapabilities(true, true, false, false, false, false);
        Assert.True(caps.SupportThinking);
        Assert.True(caps.SupportFunctionCalling);
        Assert.False(caps.SupportVision);
        Assert.False(caps.SupportAudio);
        Assert.False(caps.SupportImageGeneration);
        Assert.False(caps.SupportVideoGeneration);
        Assert.Equal(0, caps.ContextLength);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—多模态参数位置正确")]
    public void AiProviderCapabilities_MultimodalFields()
    {
        // Vision=true, Audio=true 位于第3、4位
        var caps = new NewLife.AI.Clients.AiProviderCapabilities(false, false, true, true, true, true);
        Assert.False(caps.SupportThinking);
        Assert.False(caps.SupportFunctionCalling);
        Assert.True(caps.SupportVision);
        Assert.True(caps.SupportAudio);
        Assert.True(caps.SupportImageGeneration);
        Assert.True(caps.SupportVideoGeneration);
        Assert.Equal(0, caps.ContextLength);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—ContextLength 作为第7个参数正确存储")]
    public void AiProviderCapabilities_ContextLength()
    {
        var caps = new NewLife.AI.Clients.AiProviderCapabilities(true, true, true, false, false, false, 131_072);
        Assert.True(caps.SupportThinking);
        Assert.True(caps.SupportFunctionCalling);
        Assert.True(caps.SupportVision);
        Assert.Equal(131_072, caps.ContextLength);
    }
}
