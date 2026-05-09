using System.ComponentModel;
using Xunit;

namespace XUnitTest.Models;

public class AiModelContractTests
{
    [Fact]
    [DisplayName("VideoModels—DTO 属性正确赋值")]
    public void VideoGenerationRequest_Properties()
    {
        var req = new NewLife.AI.Clients.OpenAI.VideoGenerationRequest
        {
            Model = "wan2.7-t2v",
            Prompt = "一只猫在太空漫步",
            Size = "1280*720",
            Duration = 5,
        };
        Assert.Equal("wan2.7-t2v", req.Model);
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
        Assert.True(caps.SupportFunction);
        Assert.False(caps.SupportVision);
        Assert.False(caps.SupportAudio);
        Assert.False(caps.SupportImage);
        Assert.False(caps.SupportVideo);
        Assert.Equal(0, caps.ContextLength);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—多模态参数位置正确")]
    public void AiProviderCapabilities_MultimodalFields()
    {
        // Vision=true, Audio=true 位于第3、4位
        var caps = new NewLife.AI.Clients.AiProviderCapabilities(false, false, true, true, true, true);
        Assert.False(caps.SupportThinking);
        Assert.False(caps.SupportFunction);
        Assert.True(caps.SupportVision);
        Assert.True(caps.SupportAudio);
        Assert.True(caps.SupportImage);
        Assert.True(caps.SupportVideo);
        Assert.Equal(0, caps.ContextLength);
    }

    [Fact]
    [DisplayName("AiProviderCapabilities—ContextLength 作为第7个参数正确存储")]
    public void AiProviderCapabilities_ContextLength()
    {
        var caps = new NewLife.AI.Clients.AiProviderCapabilities(true, true, true, false, false, false, false, 131_072);
        Assert.True(caps.SupportThinking);
        Assert.True(caps.SupportFunction);
        Assert.True(caps.SupportVision);
        Assert.Equal(131_072, caps.ContextLength);
    }
}