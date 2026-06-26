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
        var cfg = LoadConfig();
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

    /// <summary>DashScope 测试配置（JSON 文件结构）</summary>
    private class DashScopeTestConfig
    {
        public String? ApiKey { get; set; }
        public String? CustomVoiceId { get; set; }
        public String? Organization { get; set; }
    }

    /// <summary>从 config/DashScope.key 加载配置。自动识别 JSON 或纯文本格式</summary>
    private static DashScopeTestConfig? LoadConfig()
    {
        var path = "config/DashScope.key".GetFullPath();
        var dir = Path.GetDirectoryName(path);
        if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
        {
            var empty = new DashScopeTestConfig();
            File.WriteAllText(path, empty.ToJson());
            return empty;
        }

        var content = File.ReadAllText(path).Trim();

        if (content.StartsWith('{'))
        {
            try
            {
                var cfg = content.ToJsonEntity<DashScopeTestConfig>();
                if (cfg != null) return cfg;
            }
            catch { }
        }

        var apiKey = content;
        if (!apiKey.IsNullOrEmpty())
        {
            var cfg = new DashScopeTestConfig { ApiKey = apiKey };
            File.WriteAllText(path, cfg.ToJson());
            return cfg;
        }

        return null;
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

    #endregion
}
