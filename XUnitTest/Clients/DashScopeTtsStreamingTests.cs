#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.OpenAI;
using NewLife.Serialization;
using Xunit;
using Xunit.Sdk;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>DashScope 语音合成流式（WebSocket）集成测试</summary>
/// <remarks>
/// 包含 CosyVoice 与 Qwen-TTS-Realtime 两系列的 SpeechStreamAsync 测试。
/// CosyVoice WS 需 ApiKey + Organization + CustomVoiceId（v3.5 声音复刻）。
/// Qwen-TTS-Realtime WS 需 ApiKey + Organization。
/// 任一不满足时测试静默跳过。
/// </remarks>
public class DashScopeTtsStreamingTests
{
    private readonly String _apiKey;
    private readonly String _customVoiceId;
    private readonly String _organization;

    public DashScopeTtsStreamingTests()
    {
        var cfg = DashScopeKeyLoader.LoadConfig();
        _apiKey = cfg?.ApiKey ?? "";
        _customVoiceId = cfg?.CustomVoiceId ?? "";
        _organization = cfg?.Organization ?? "";

        var envKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
        if (!envKey.IsNullOrEmpty()) _apiKey = envKey;

        var envVoice = Environment.GetEnvironmentVariable("DASHSCOPE_CUSTOM_VOICE_ID");
        if (!envVoice.IsNullOrEmpty()) _customVoiceId = envVoice;

        var envOrg = Environment.GetEnvironmentVariable("DASHSCOPE_ORGANIZATION");
        if (!envOrg.IsNullOrEmpty()) _organization = envOrg;
    }



    /// <summary>构建默认连接选项（含 Organization）</summary>
    private AiClientOptions CreateOptions() => new()
    {
        ApiKey = _apiKey,
        Organization = _organization,
    };

    #region CosyVoice WebSocket 流式合成

    [Fact]
    [DisplayName("SpeechStreamAsync_cosyvoice_v3.5_flash_流式返回多个音频分片")]
    public async Task SpeechStreamAsync_CosyVoiceV35Flash_StreamingReturnsChunks()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization) || String.IsNullOrEmpty(_customVoiceId)) return;

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3.5-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "你好，欢迎使用语音合成服务。今天天气真不错，适合出去走走。",
            Voice = _customVoiceId,
            ResponseFormat = "mp3",
            SampleRate = 24000,
            Speed = 1.0,
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            Assert.NotNull(chunk);
            Assert.True(chunk.Length > 0, $"第 {chunks.Count + 1} 个音频分片不应为空");
            chunks.Add(chunk);
        }

        Assert.True(chunks.Count >= 1, "应至少返回一个音频分片");
        var totalBytes = chunks.Sum(c => c.Length);
        Assert.True(totalBytes > 100, $"总音频数据 {totalBytes} 字节，应大于 100");
        Assert.True(request.CharactersUsed > 0, $"字符用量应大于 0，实际: {request.CharactersUsed}");

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_CosyVoiceV35Flash_StreamingReturnsChunks)}.mp3");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_cosyvoice_v3.5_flash_带语速参数")]
    public async Task SpeechStreamAsync_CosyVoiceV35Flash_WithSpeed()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization) || String.IsNullOrEmpty(_customVoiceId)) return;

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3.5-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "这是一段测试文本，用来验证语速参数是否生效。",
            Voice = _customVoiceId,
            ResponseFormat = "mp3",
            Speed = 1.5,
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_CosyVoiceV35Flash_WithSpeed)}.mp3");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_cosyvoice_v3.5_flash_CancellationToken取消")]
    public async Task SpeechStreamAsync_CosyVoiceV35Flash_Cancellation()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization) || String.IsNullOrEmpty(_customVoiceId)) return;

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3.5-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "人工智能是计算机科学的一个分支，它企图了解智能的实质，并生产出一种新的能以人类智能相似的方式做出反应的智能机器。该领域的研究包括机器人、语言识别、图像识别、自然语言处理和专家系统等。",
            Voice = _customVoiceId,
            ResponseFormat = "mp3",
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var cancelled = false;
        try
        {
            await foreach (var _ in client.SpeechStreamAsync(request, cts.Token)) { }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled, "取消令牌应生效");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_cosyvoice_v3.5_flash_opus格式")]
    public async Task SpeechStreamAsync_CosyVoiceV35Flash_OpusFormat()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization) || String.IsNullOrEmpty(_customVoiceId)) return;

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3.5-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "你好世界",
            Voice = _customVoiceId,
            ResponseFormat = "opus",
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        var totalBytes = chunks.Sum(c => c.Length);
        Assert.True(totalBytes > 0, "opus 格式应生成有效音频");

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_CosyVoiceV35Flash_OpusFormat)}.opus");
    }

    #endregion

    #region Qwen-TTS-Realtime WebSocket 实时合成

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen3_tts_flash_realtime_流式返回多个音频分片")]
    public async Task SpeechStreamAsync_Qwen3TtsFlashRealtime_StreamingReturnsChunks()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        option.Model = "qwen3-tts-flash-realtime";

        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-flash-realtime",
            Input = "你好，欢迎使用千问实时语音合成服务。今天天气很不错。",
            Voice = "Cherry",
            ResponseFormat = "pcm",
            SampleRate = 24000,
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            Assert.NotNull(chunk);
            Assert.True(chunk.Length > 0, $"第 {chunks.Count + 1} 个音频分片不应为空");
            chunks.Add(chunk);
        }

        Assert.True(chunks.Count >= 1, "应至少返回一个音频分片");
        var total = chunks.Sum(c => c.Length);
        Assert.True(total > 100, $"总音频 {total} 字节，应大于 100");

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_Qwen3TtsFlashRealtime_StreamingReturnsChunks)}.pcm");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen_tts_realtime_Cherry音色")]
    public async Task SpeechStreamAsync_QwenTtsRealtime_CherryVoice()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen-tts-realtime",
            Input = "这是一段简短的测试文本。",
            Voice = "Cherry",
            ResponseFormat = "pcm",
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
            chunks.Add(chunk);

        Assert.NotEmpty(chunks);

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_QwenTtsRealtime_CherryVoice)}.pcm");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen3_tts_flash_realtime_带language_type参数")]
    public async Task SpeechStreamAsync_Qwen3TtsFlashRealtime_WithLanguageType()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-flash-realtime",
            Input = "Hello, this is a test with language type parameter.",
            Voice = "Cherry",
            ResponseFormat = "pcm",
        };
        request["language_type"] = "English";

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
            chunks.Add(chunk);

        Assert.NotEmpty(chunks);

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_Qwen3TtsFlashRealtime_WithLanguageType)}.pcm");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen3_tts_flash_realtime_CancellationToken取消")]
    public async Task SpeechStreamAsync_Qwen3TtsFlashRealtime_Cancellation()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-flash-realtime",
            Input = "人工智能是计算机科学的一个分支，它企图了解智能的实质，并生产出一种新的能以人类智能相似的方式做出反应的智能机器。该领域的研究包括机器人、语言识别、图像识别、自然语言处理和专家系统等。",
            Voice = "Cherry",
            ResponseFormat = "pcm",
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var cancelled = false;
        try
        {
            await foreach (var _ in client.SpeechStreamAsync(request, cts.Token)) { }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled, "取消令牌应生效");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen3_tts_flash_realtime_opus格式")]
    public async Task SpeechStreamAsync_Qwen3TtsFlashRealtime_OpusFormat()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        option.Model = "qwen3-tts-flash-realtime";

        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-flash-realtime",
            Input = "你好，这是opus格式流式测试。",
            Voice = "Cherry",
            ResponseFormat = "opus",
            SampleRate = 24000,
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            Assert.NotNull(chunk);
            Assert.True(chunk.Length > 0, $"第 {chunks.Count + 1} 个 opus 分片不应为空");
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        var totalBytes = chunks.Sum(c => c.Length);
        Assert.True(totalBytes > 0, "opus 格式应生成有效音频");

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_Qwen3TtsFlashRealtime_OpusFormat)}.opus");
    }

    [Fact]
    [DisplayName("SpeechStreamAsync_qwen3_tts_instruct_flash_realtime_opus格式")]
    public async Task SpeechStreamAsync_Qwen3TtsInstructFlashRealtime_OpusFormat()
    {
        if (String.IsNullOrEmpty(_apiKey) || String.IsNullOrEmpty(_organization)) return;

        var option = CreateOptions();
        option.Model = "qwen3-tts-instruct-flash-realtime";

        using var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-instruct-flash-realtime",
            Input = "你好，这是opus格式流式测试。",
            Voice = "Cherry",
            ResponseFormat = "opus",
            SampleRate = 24000,
        };

        var chunks = new List<Byte[]>();
        await foreach (var chunk in client.SpeechStreamAsync(request, CancellationToken.None))
        {
            Assert.NotNull(chunk);
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        var totalBytes = chunks.Sum(c => c.Length);
        Assert.True(totalBytes > 0, "opus 格式应生成有效音频");

        // 合并所有分片并保存音频文件到本地，供人工检查
        var combined = chunks.SelectMany(c => c).ToArray();
        await SaveOutputFileAsync(combined, $"{nameof(SpeechStreamAsync_Qwen3TtsInstructFlashRealtime_OpusFormat)}.opus");
    }

    #endregion

    #region 辅助方法
    /// <summary>将音频字节数据保存到 TestOutput/ 目录（带时间戳前缀），返回保存路径</summary>
    private static async Task<String> SaveOutputFileAsync(Byte[] data, String fileName)
    {
        var dir = "../TestOutput".GetFullPath();
        dir.EnsureDirectory(false);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var savePath = Path.Combine(dir, $"{ts}_{fileName}");
        await File.WriteAllBytesAsync(savePath, data);
        XTrace.WriteLine($"[TestOutput] 文件已保存: {savePath}");
        return savePath;
    }
    #endregion
}
