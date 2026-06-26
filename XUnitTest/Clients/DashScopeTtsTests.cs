#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

/// <summary>DashScope 语音合成（非实时 HTTP）集成测试</summary>
/// <remarks>
/// 包含 CosyVoice 与 Qwen-TTS 两系列的 SpeechAsync 测试。
/// 配置读取于 config/DashScope.key（可选），环境变量可覆盖。
/// 未配置 ApiKey 时测试自动跳过。
/// </remarks>
public class DashScopeTtsTests
{
    private readonly String _apiKey;

    public DashScopeTtsTests()
    {
        var cfg = LoadConfig();
        _apiKey = cfg?.ApiKey ?? "";

        var envKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
        if (!envKey.IsNullOrEmpty()) _apiKey = envKey;
    }

    /// <summary>DashScope 测试配置（JSON 文件结构）</summary>
    private class DashScopeTestConfig
    {
        public String? ApiKey { get; set; }
        public String? CustomVoiceId { get; set; }
        public String? Organization { get; set; }
    }

    /// <summary>从 config/DashScope.key 加载配置。自动识别 JSON 或纯文本格式，旧格式自动转为 JSON 写回</summary>
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

    /// <summary>从 config/DashScope.key 或环境变量加载 ApiKey（供其他测试类复用）</summary>
    public static String? LoadApiKey()
    {
        var cfg = LoadConfig();
        if (cfg != null && !cfg.ApiKey.IsNullOrEmpty())
            return cfg.ApiKey;
        return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
    }

    /// <summary>构建默认连接选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
        ApiKey = _apiKey,
    };

    /// <summary>确保已配置可用 ApiKey。未配置时跳过依赖真实服务的集成测试</summary>
    private void EnsureConfiguredApiKeyAvailable(AiClientOptions? opts = null)
    {
        var apiKey = opts?.ApiKey;
        if (String.IsNullOrWhiteSpace(apiKey)) apiKey = _apiKey;

        if (String.IsNullOrWhiteSpace(apiKey))
            throw SkipException.ForSkip("未检测到可用 API Key（config/DashScope.key 或 DASHSCOPE_API_KEY 环境变量），跳过 TTS 集成测试");
    }

    #region CosyVoice 非实时 HTTP 合成

    [Fact]
    [DisplayName("SpeechAsync_OapiVoice_映射为DashScope默认音色")]
    public async Task SpeechAsync_OapiVoice_UseDashScopeDefaultVoice()
    {
        if (String.IsNullOrEmpty(_apiKey)) return;

        var rawBody = """{"model":"cosyvoice-v3-flash","input":{"text":"你好。","voice":"longanyang","format":"wav","sample_rate":24000}}""";
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        var rawResp = await httpClient.PostAsync(
            "https://dashscope.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer",
            new StringContent(rawBody, Encoding.UTF8, "application/json"));
        var rawJson = await rawResp.Content.ReadAsStringAsync();
        if (!rawResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"原始 HTTP 请求也失败: {rawResp.StatusCode} {rawJson}");

        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "你好，欢迎使用语音合成服务。",
            Voice = "alloy",
            ResponseFormat = "wav",
        };

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "音频数据不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_DashScopeNativeVoice_直接传递")]
    public async Task SpeechAsync_DashScopeNativeVoice_Works()
    {
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "今天天气真不错。",
            Voice = "longanyang",
            ResponseFormat = "wav",
        };

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "音频数据不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_cosyvoice_v3_flash_完整音频合成并记录字符用量")]
    public async Task SpeechAsync_CosyVoiceV3Flash_ReturnsAudio()
    {
        EnsureConfiguredApiKeyAvailable();

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "你好，欢迎使用语音合成服务。今天天气真不错，适合出去走走。",
            Voice = "longxiaochun_v3",
            ResponseFormat = "mp3",
            SampleRate = 24000,
            Speed = 1.0,
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, $"总音频数据 {audioBytes.Length} 字节，应大于 100");
        Assert.True(request.CharactersUsed > 0, $"字符用量应大于 0，实际: {request.CharactersUsed}");
    }

    [Fact]
    [DisplayName("SpeechAsync_cosyvoice_v3_flash_带语速参数")]
    public async Task SpeechAsync_CosyVoiceV3Flash_WithSpeed()
    {
        EnsureConfiguredApiKeyAvailable();

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "这是一段测试文本，用来验证语速参数是否生效。",
            Voice = "longxiaochun_v3",
            ResponseFormat = "mp3",
            Speed = 1.5,
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "语速 1.5x 的合成音频不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_cosyvoice_v3_flash_CancellationToken取消")]
    public async Task SpeechAsync_CosyVoiceV3Flash_Cancellation()
    {
        EnsureConfiguredApiKeyAvailable();

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "人工智能是计算机科学的一个分支，它企图了解智能的实质，并生产出一种新的能以人类智能相似的方式做出反应的智能机器。该领域的研究包括机器人、语言识别、图像识别、自然语言处理和专家系统等。人工智能从诞生以来，理论和技术日益成熟，应用领域也不断扩大。可以设想，未来人工智能带来的科技产品，将会是人类智慧的容器。",
            Voice = "longxiaochun_v3",
            ResponseFormat = "mp3",
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var cancelled = false;
        try
        {
            await client.SpeechAsync(request, cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled, "取消令牌应生效");
    }

    [Fact]
    [DisplayName("SpeechAsync_cosyvoice_v3_flash_opus格式")]
    public async Task SpeechAsync_CosyVoiceV3Flash_OpusFormat()
    {
        EnsureConfiguredApiKeyAvailable();

        var option = CreateOptions();
        option.Endpoint = "https://dashscope.aliyuncs.com/api/v1";
        option.ApiKey = _apiKey;
        option.Model = "cosyvoice-v3-flash";

        var client = new DashScopeChatClient(option);
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "你好世界",
            Voice = "longxiaochun_v3",
            ResponseFormat = "opus",
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 0, "opus 格式应生成有效音频");
    }

    #endregion

    #region Qwen-TTS 非实时 HTTP 合成

    [Fact]
    [DisplayName("SpeechAsync_qwen_tts_合成音频并记录Token用量")]
    public async Task SpeechAsync_QwenTts_ReturnsAudio()
    {
        EnsureConfiguredApiKeyAvailable();

        using var client = new DashScopeChatClient(CreateOptions());
        var request = new SpeechRequest
        {
            Model = "qwen-tts",
            Input = "你好，欢迎使用千问语音合成服务。",
            Voice = "Cherry",
            ResponseFormat = "mp3",
            SampleRate = 24000,
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, $"音频数据 {audioBytes.Length} 字节，应大于 100");
        Assert.True(request.CharactersUsed > 0, $"用量应大于 0，实际: {request.CharactersUsed}");
    }

    [Fact]
    [DisplayName("SpeechAsync_qwen3_tts_flash_合成音频")]
    public async Task SpeechAsync_Qwen3TtsFlash_ReturnsAudio()
    {
        EnsureConfiguredApiKeyAvailable();

        using var client = new DashScopeChatClient(CreateOptions());
        var request = new SpeechRequest
        {
            Model = "qwen3-tts-flash",
            Input = "今天天气不错，适合出行。",
            Voice = "Stella",
            ResponseFormat = "wav",
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "qwen3-tts-flash 音频不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_qwen_tts_带language_type参数")]
    public async Task SpeechAsync_QwenTts_WithLanguageType()
    {
        EnsureConfiguredApiKeyAvailable();

        using var client = new DashScopeChatClient(CreateOptions());
        var request = new SpeechRequest
        {
            Model = "qwen-tts",
            Input = "Hello, welcome to Qwen TTS.",
            Voice = "Cherry",
            ResponseFormat = "mp3",
        };
        request["language_type"] = "English";

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "带 language_type 参数的合成不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_qwen_tts_OAPI音色映射为Cherry")]
    public async Task SpeechAsync_QwenTts_OapiVoiceMappedToCherry()
    {
        EnsureConfiguredApiKeyAvailable();

        using var client = new DashScopeChatClient(CreateOptions());
        var request = new SpeechRequest
        {
            Model = "qwen-tts",
            Input = "测试默认音色映射。",
            Voice = "alloy",
            ResponseFormat = "mp3",
        };

        var audioBytes = await client.SpeechAsync(request);

        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 100, "OAPI 音色映射后的合成不应为空");
    }

    [Fact]
    [DisplayName("SpeechAsync_qwen_tts_CancellationToken取消")]
    public async Task SpeechAsync_QwenTts_Cancellation()
    {
        EnsureConfiguredApiKeyAvailable();

        using var client = new DashScopeChatClient(CreateOptions());
        var request = new SpeechRequest
        {
            Model = "qwen-tts",
            Input = "人工智能是计算机科学的一个分支，它企图了解智能的实质，并生产出一种新的能以人类智能相似的方式做出反应的智能机器。该领域的研究包括机器人、语言识别、图像识别、自然语言处理和专家系统等。",
            Voice = "Cherry",
            ResponseFormat = "mp3",
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var cancelled = false;
        try
        {
            await client.SpeechAsync(request, cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(cancelled, "取消令牌应生效");
    }

    #endregion

    #region 模型能力推断（TTS 系列）

    [Fact]
    [DisplayName("能力推断_qwen-tts_SupportAudio为true")]
    public void InferCapabilities_QwenTts_AudioTrue()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("qwen-tts");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio, "qwen-tts 应返回 SupportAudio=true");
        Assert.False(cap.SupportThinking);
        Assert.False(cap.SupportImage);
    }

    [Fact]
    [DisplayName("能力推断_qwen3-tts-flash_SupportAudio为true")]
    public void InferCapabilities_Qwen3TtsFlash_AudioTrue()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("qwen3-tts-flash");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio, "qwen3-tts-flash 应返回 SupportAudio=true");
    }

    [Fact]
    [DisplayName("能力推断_qwen3-tts-flash-realtime_SupportAudio为true")]
    public void InferCapabilities_Qwen3TtsFlashRealtime_AudioTrue()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("qwen3-tts-flash-realtime");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio, "qwen3-tts-flash-realtime 应返回 SupportAudio=true");
    }

    [Fact]
    [DisplayName("能力推断_qwen-tts-realtime_SupportAudio为true")]
    public void InferCapabilities_QwenTtsRealtime_AudioTrue()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("qwen-tts-realtime");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio, "qwen-tts-realtime 应返回 SupportAudio=true");
    }

    [Fact]
    [DisplayName("能力推断_qwen3-tts-instruct-flash_SupportAudio为true")]
    public void InferCapabilities_Qwen3TtsInstructFlash_AudioTrue()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("qwen3-tts-instruct-flash");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio);
    }

    [Fact]
    [DisplayName("能力推断_cosyvoice-v3-flash_SupportAudio为true（回归验证）")]
    public void InferCapabilities_CosyVoiceV3Flash_AudioTrue_Regression()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        var cap = client.InferModelCapabilities("cosyvoice-v3-flash");
        Assert.NotNull(cap);
        Assert.True(cap!.SupportAudio, "cosyvoice-v3-flash 能力推断不应被 qwen-tts 修改影响");
    }

    #endregion

    #region QwenTtsVoiceList 音色列表

    [Fact]
    [DisplayName("QwenTtsVoiceList_GetAll_返回非空音色列表")]
    public void QwenTtsVoiceList_GetAll_ReturnsVoices()
    {
        var all = QwenTtsVoiceList.GetAll();
        Assert.NotNull(all);
        Assert.NotEmpty(all);
    }

    [Fact]
    [DisplayName("QwenTtsVoiceList_GetVoices_qwen-tts_包含Cherry")]
    public void QwenTtsVoiceList_GetVoices_QwenTts_ContainsCherry()
    {
        var voices = QwenTtsVoiceList.GetVoices("qwen-tts");
        Assert.NotEmpty(voices);
        Assert.Contains(voices, v => v.Id.EqualIgnoreCase("Cherry"));
    }

    [Fact]
    [DisplayName("QwenTtsVoiceList_IsValidVoice_已知音色返回true")]
    public void QwenTtsVoiceList_IsValidVoice_KnownVoice_ReturnsTrue()
    {
        Assert.True(QwenTtsVoiceList.IsValidVoice("qwen-tts", "Cherry"));
        Assert.True(QwenTtsVoiceList.IsValidVoice("qwen-tts", "Stella"));
        Assert.True(QwenTtsVoiceList.IsValidVoice("qwen3-tts-flash", "Brendan"));
    }

    [Fact]
    [DisplayName("QwenTtsVoiceList_IsValidVoice_OAPI兼容音色返回true")]
    public void QwenTtsVoiceList_IsValidVoice_OapiVoice_ReturnsTrue()
    {
        Assert.True(QwenTtsVoiceList.IsValidVoice("qwen-tts", "alloy"));
        Assert.True(QwenTtsVoiceList.IsValidVoice("qwen-tts", "echo"));
    }

    [Fact]
    [DisplayName("QwenTtsVoiceList_IsValidVoice_未知音色返回false")]
    public void QwenTtsVoiceList_IsValidVoice_UnknownVoice_ReturnsFalse()
    {
        Assert.False(QwenTtsVoiceList.IsValidVoice("qwen-tts", "longxiaochun_v3"));
        Assert.False(QwenTtsVoiceList.IsValidVoice("qwen-tts", "nonexistent-voice-xyz"));
    }

    #endregion

    #region SpeechRequest 扩展属性（Items）

    [Fact]
    [DisplayName("SpeechRequest_Items_索引器可读写")]
    public void SpeechRequest_Items_IndexerWorks()
    {
        var req = new SpeechRequest { Model = "qwen-tts", Input = "test", Voice = "Cherry" };

        req["language_type"] = "Chinese";
        req["instructions"] = "请用活泼的语调朗读";

        Assert.Equal("Chinese", req["language_type"] as String);
        Assert.Equal("请用活泼的语调朗读", req["instructions"] as String);
    }

    [Fact]
    [DisplayName("SpeechRequest_Items_不存在的键返回null")]
    public void SpeechRequest_Items_MissingKey_ReturnsNull()
    {
        var req = new SpeechRequest { Model = "qwen-tts", Input = "test", Voice = "Cherry" };
        Assert.Null(req["nonexistent_key"]);
    }

    [Fact]
    [DisplayName("SpeechRequest_Items_懒初始化不影响空实例")]
    public void SpeechRequest_Items_NullSafe()
    {
        var req = new SpeechRequest { Model = "qwen-tts", Input = "test", Voice = "Cherry" };
        Assert.Null(req.Items);
        _ = req["key"];
        Assert.Null(req.Items);
    }

    #endregion
}
