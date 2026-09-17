using System.ComponentModel;
using NewLife.ChatAI.Entity;
using Xunit;

namespace XUnitTest.Services;

/// <summary>模型可选性判定测试。IsChatModel 决定对话链路与对话候选，IsMainModel 决定主模型选择器（/chat 首页）可见性</summary>
public class ModelConfigEligibilityTests
{
    [Fact]
    [DisplayName("模型可选性: 纯文本模型既是对话模型也是主模型")]
    public void PlainTextModel_IsChatAndMain()
    {
        var model = new ModelConfig();

        Assert.True(model.IsChatModel);
        Assert.True(model.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 思考/工具/视觉模型为对话模型")]
    public void ReasoningModels_AreChat()
    {
        Assert.True(new ModelConfig { SupportThinking = true }.IsChatModel);
        Assert.True(new ModelConfig { SupportFunction = true }.IsChatModel);
        Assert.True(new ModelConfig { SupportVision = true }.IsChatModel);
    }

    [Fact]
    [DisplayName("模型可选性: 语音识别模型不是对话模型也不进主列表（Qwen Audio Realtime 回归）")]
    public void AudioModel_Excluded()
    {
        var model = new ModelConfig { SupportAudio = true };

        Assert.False(model.IsChatModel);
        Assert.False(model.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 语音合成模型不是对话模型也不进主列表")]
    public void SpeechModel_Excluded()
    {
        var model = new ModelConfig { SupportSpeech = true };

        Assert.False(model.IsChatModel);
        Assert.False(model.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: Omni 全模态（视觉+语音）不进主列表")]
    public void OmniModel_Excluded()
    {
        var model = new ModelConfig { SupportVision = true, SupportAudio = true, SupportSpeech = true };

        Assert.False(model.IsChatModel);
        Assert.False(model.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 嵌入模型（含 VL 嵌入）不是对话模型也不进主列表")]
    public void EmbeddingModel_Excluded()
    {
        var model = new ModelConfig { SupportEmbedding = true };
        Assert.False(model.IsChatModel);
        Assert.False(model.IsMainModel);

        // VL 嵌入（视觉+嵌入）不得因视觉位误入主列表
        var vlEmbedding = new ModelConfig { SupportVision = true, SupportEmbedding = true };
        Assert.False(vlEmbedding.IsChatModel);
        Assert.False(vlEmbedding.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 重排序模型（含 VL 重排）不是对话模型也不进主列表")]
    public void RerankModel_Excluded()
    {
        var model = new ModelConfig { SupportRerank = true };
        Assert.False(model.IsChatModel);
        Assert.False(model.IsMainModel);

        var vlRerank = new ModelConfig { SupportVision = true, SupportRerank = true };
        Assert.False(vlRerank.IsChatModel);
        Assert.False(vlRerank.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 文生图/文生视频模型进主列表但不走对话链路")]
    public void MediaModels_AreMainNotChat()
    {
        var image = new ModelConfig { SupportImage = true };
        Assert.False(image.IsChatModel);
        Assert.True(image.IsMainModel);

        var video = new ModelConfig { SupportVideo = true };
        Assert.False(video.IsChatModel);
        Assert.True(video.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 图生视频模型（带视觉位）仍可进主列表")]
    public void ImageToVideoModel_IsMain()
    {
        // 对话链路判定沿用“视觉即对话”既有语义，此处仅约束主列表可见性
        var model = new ModelConfig { SupportVision = true, SupportVideo = true };

        Assert.True(model.IsMainModel);
    }

    [Fact]
    [DisplayName("模型可选性: 对话模型误配媒体能力时仍按对话模型处理")]
    public void ChatModelWithMediaFlag_StaysChat()
    {
        var model = new ModelConfig { SupportThinking = true, SupportImage = true };

        Assert.True(model.IsChatModel);
        Assert.True(model.IsMainModel);
    }
}
