using System.Runtime.CompilerServices;
using System.Net.WebSockets;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

public partial class DashScopeChatClient
{
    #region 语音合成（TTS）
    /// <summary>语音合成（TTS）。根据模型前缀自动路由：CosyVoice 系列走 SpeechSynthesizer 端点；Qwen-TTS 系列走 multimodal-generation 端点</summary>
    /// <remarks>
    /// CosyVoice 端点：POST /api/v1/services/audio/tts/SpeechSynthesizer<br/>
    /// 请求格式：{"model":"...","input":{"text":"...","voice":"...","format":"wav","sample_rate":24000}}<br/>
    /// Qwen-TTS 端点：POST /api/v1/services/aigc/multimodal-generation/generation<br/>
    /// 请求格式：{"model":"...","input":{"text":"...","voice":"..."},"parameters":{}}<br/>
    /// 两者响应相同：JSON → output.audio.url → 下载音频字节流<br/>
    /// CosyVoice 文档：https://help.aliyun.com/zh/model-studio/cosyvoice-tts-http-api<br/>
    /// Qwen-TTS 文档：https://help.aliyun.com/zh/model-studio/qwen-tts-api
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流</returns>
    public override async Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Input)) throw new ArgumentException("合成文本不能为空", nameof(request));

        var format = request.ResponseFormat switch
        {
            "mp3" => "mp3",
            "opus" => "opus",
            "pcm" => "pcm",
            "wav" or null => "wav",
            var f => f,
        };

        var modelCode = request.Model ?? _options.Model ?? "cosyvoice-v3-flash";

        String voice;
        Dictionary<String, Object> input;
        String ttsUrl;
        Dictionary<String, Object?> body;

        if (IsQwenTtsModel(modelCode))
        {
            // Qwen-TTS 系列：走 multimodal-generation 端点，默认音色 Cherry
            ttsUrl = GetNativeBaseUrl() + "/services/aigc/multimodal-generation/generation";
            voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (QwenTtsVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "Cherry";
            }
            if (!QwenTtsVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");
            input = BuildQwenTtsInput(request, voice, format, request.SampleRate ?? 24000);
            body = new Dictionary<String, Object?>
            {
                ["model"] = modelCode,
                ["input"] = input,
                ["parameters"] = new Dictionary<String, Object>(),  // Qwen-TTS 必须携带空 parameters
            };
        }
        else
        {
            // CosyVoice 系列：走 SpeechSynthesizer 端点，默认音色 longxiaochun_v3
            ttsUrl = GetNativeBaseUrl() + "/services/audio/tts/SpeechSynthesizer";
            voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (CosyVoiceVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "longxiaochun_v3";
            }
            if (!CosyVoiceVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中。可用音色请参见 GET /api/audio/voices");
            input = BuildCosyVoiceTtsInput(request, voice, format, request.SampleRate ?? 24000);
            body = new Dictionary<String, Object?>
            {
                ["model"] = modelCode,
                ["input"] = input,
            };
        }

        using var span = Tracer?.NewSpan("ai:DashScopeTts", new { model = modelCode, format, voice = request.Voice });
        try
        {
            var json = await PostAsync(ttsUrl, body, null, _options, cancellationToken).ConfigureAwait(false);

            var dic = JsonParser.Decode(json);
            if (dic == null)
                throw new InvalidOperationException("无法解析 DashScope TTS 响应");

            var code = dic["code"] as String;
            if (!String.IsNullOrEmpty(code))
            {
                var message = dic["message"] as String ?? code;
                throw new HttpRequestException($"[DashScope] TTS 错误 {code}: {message}");
            }

            var output = dic["output"] as IDictionary<String, Object>
                ?? throw new InvalidOperationException("DashScope TTS 响应缺少 output 字段");
            var audio = output["audio"] as IDictionary<String, Object>
                ?? throw new InvalidOperationException("DashScope TTS 响应缺少 output.audio 字段");
            var audioUrl = audio["url"] as String;
            if (String.IsNullOrWhiteSpace(audioUrl))
                throw new InvalidOperationException("DashScope TTS 响应缺少 output.audio.url");

            // 解析用量：Qwen-TTS 用 total_tokens，CosyVoice 用 characters，两者兼容
            if (dic["usage"] is IDictionary<String, Object> usage)
            {
                var chars = 0;
                if (chars == 0 && usage.TryGetValue("total_tokens", out var tt)) chars = tt.ToInt();
                if (chars == 0 && usage.TryGetValue("characters", out var ch)) chars = ch.ToInt();
                if (chars == 0 && usage.TryGetValue("input_characters", out var ic)) chars = ic.ToInt();
                request.CharactersUsed = chars;
            }

            // 池化 HttpMessageHandler：TTS 音频下载按主机复用连接，避免每次新建连接池（A-01/A-02 同类反模式）
            var handler = HttpClientPool.GetHandler(audioUrl);
            using var httpClient = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
#if NET5_0_OR_GREATER
            var audioBytes = await httpClient.GetByteArrayAsync(audioUrl, cancellationToken).ConfigureAwait(false);
#else
            var audioBytes = await httpClient.GetByteArrayAsync(audioUrl).ConfigureAwait(false);
#endif
            return audioBytes;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>判断是否为 Qwen-TTS 非实时 HTTP 合成模型（qwen-tts* / qwen3-tts*，不含 -realtime）</summary>
    private static Boolean IsQwenTtsModel(String modelId) =>
        !modelId.IsNullOrEmpty()
        && modelId.StartsWithIgnoreCase("qwen-tts", "qwen3-tts")
        && !modelId.Contains("-realtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断是否为 Qwen-TTS-Realtime WebSocket 实时合成模型（含 -realtime 后缀）</summary>
    private static Boolean IsQwenTtsRealtimeModel(String modelId) =>
        !modelId.IsNullOrEmpty()
        && modelId.StartsWithIgnoreCase("qwen-tts", "qwen3-tts")
        && modelId.Contains("-realtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断指定模型是否支持流式语音合成</summary>
    /// <remarks>
    /// 支持流式合成的模型类型：
    /// <list type="bullet">
    /// <item>CosyVoice 全系列（cosyvoice-*：v2/v3/v3.5）：通过 run-task WebSocket 实现</item>
    /// <item>Qwen-TTS-Realtime 系列（*-realtime）：通过 session.* WebSocket 实现</item>
    /// </list>
    /// 以上两类均需要所在提供商配置了 Organization（业务空间 ID）才能构建 WebSocket 端点。
    /// </remarks>
    /// <param name="modelId">模型编码，null 时取客户端默认模型</param>
    /// <returns>支持流式合成返回 true</returns>
    public override Boolean SupportsSpeechStreaming(String? modelId)
    {
        var id = modelId ?? _options.Model;
        if (id.IsNullOrEmpty()) return false;

        // CosyVoice 全系列和 Qwen-TTS-Realtime 系列支持 WebSocket 流式合成
        if (!id.StartsWithIgnoreCase("cosyvoice")
            && !id.EndsWith("-realtime", StringComparison.OrdinalIgnoreCase))
            return false;

        // WebSocket 端点需要 Organization（业务空间 ID）
        return !_options.Organization.IsNullOrEmpty();
    }

    /// <summary>构建 CosyVoice TTS HTTP API 的 input 参数字典</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="voice">已解析的音色</param>
    /// <param name="format">音频格式</param>
    /// <param name="sampleRate">采样率</param>
    /// <returns>input 字典</returns>
    private static Dictionary<String, Object> BuildCosyVoiceTtsInput(SpeechRequest request, String voice, String format, Int32 sampleRate)
    {
        var input = new Dictionary<String, Object>
        {
            ["text"] = request.Input,
            ["voice"] = voice,
            ["format"] = format,
            ["sample_rate"] = sampleRate,
        };

        // 语速倍率。CosyVoice HTTP API 参数名为 rate，默认 1.0（正常语速）
        if (request.Speed is > 0 and not 1.0)
            input["rate"] = request.Speed;

        // 音量。CosyVoice HTTP API 参数名为 volume，默认 50
        if (request.Volume is > 0 and not 50)
            input["volume"] = request.Volume;

        // 音调。CosyVoice HTTP API 参数名为 pitch，默认 1.0
        if (request.Pitch is > 0 and not 1.0)
            input["pitch"] = request.Pitch;

        return input;
    }

    /// <summary>构建 Qwen-TTS HTTP API 的 input 参数字典</summary>
    /// <remarks>
    /// Qwen-TTS 与 CosyVoice 参数不同：Qwen-TTS 仅支持 text / voice / language_type，不支持 format / sample_rate。
    /// 携带 CosyVoice 专属参数会导致 DashScope 路由层误判模型类型，返回 url error。
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="voice">已解析的音色</param>
    /// <param name="format">音频格式（Qwen-TTS 不支持，忽略）</param>
    /// <param name="sampleRate">采样率（Qwen-TTS 不支持，忽略）</param>
    /// <returns>input 字典</returns>
    private static Dictionary<String, Object> BuildQwenTtsInput(SpeechRequest request, String voice, String format, Int32 sampleRate)
    {
        var input = new Dictionary<String, Object>
        {
            ["text"] = request.Input,
            ["voice"] = voice,
        };

        // Qwen-TTS 不支持 format/sample_rate 参数，仅 CosyVoice 支持
        // 这些参数会误导 DashScope 路由层将请求识别为 CosyVoice 类型，导致 url error

        // 语速倍率
        if (request.Speed is > 0 and not 1.0)
            input["rate"] = request.Speed;

        // Qwen-TTS 特有参数：通过 request.Items 字典传入
        var languageType = request["language_type"] as String;
        if (!languageType.IsNullOrEmpty())
            input["language_type"] = languageType!;

        // Qwen3-TTS-Instruct-Flash 指令控制（通过指令自然语言控制语音表现力）
        var instructions = request["instructions"] as String;
        if (!instructions.IsNullOrEmpty())
            input["instructions"] = instructions!;

        return input;
    }

    /// <summary>流式语音合成。根据模型自动路由：CosyVoice 使用 run-task/continue-task WebSocket 协议；Qwen-TTS-Realtime 使用 session.*/input_text_buffer.* 实时协议</summary>
    /// <remarks>
    /// WebSocket 端点：wss://dashscope.aliyuncs.com/api-ws/v1/inference<br/>
    /// 流程：握手 → run-task → task-started → continue-task（分片发文本）→ result-generated + binary frame（音频）→ finish-task → task-finished<br/>
    /// 文本按 ≤500 字符分片发送。详见 Doc/《CosyVoice WebSocket 流式合成.md》。
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐段返回音频字节分片</returns>
    public override async IAsyncEnumerable<Byte[]> SpeechStreamAsync(SpeechRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Input)) throw new ArgumentException("合成文本不能为空", nameof(request));

        var format = request.ResponseFormat ?? "mp3";
        format = format switch { "mp3" => "mp3", "wav" => "wav", "opus" => "opus", "pcm" => "pcm", _ => format };

        var modelCode = request.Model ?? _options.Model ?? "cosyvoice-v3-flash";

        if (IsQwenTtsRealtimeModel(modelCode))
        {
            // Qwen-TTS-Realtime：session.*/input_text_buffer.* 协议，音频在 response.audio.delta JSON 事件内（base64）
            var voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (QwenTtsVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "Cherry";
            }
            if (!QwenTtsVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");

            using var ws = new ClientWebSocket();
            if (!_options.ApiKey.IsNullOrEmpty())
                ws.Options.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            ws.Options.SetRequestHeader("user-agent", "NewLife.AI");

            // RunQwenTtsRealtimeWebSocketAsync 现在返回 IAsyncEnumerable<Byte[]>，每收到 WebSocket 音频帧即实时 yield
            // yield return 在所有 try-catch 块之外，符合 C# 异步迭代器规则
            await foreach (var chunk in RunQwenTtsRealtimeWebSocketAsync(ws, modelCode, voice, format, request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
        }
        else
        {
            // CosyVoice：run-task/continue-task 协议，音频以独立 binary frame 传输
            var voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (CosyVoiceVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "longxiaochun_v3";
            }
            if (!CosyVoiceVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");

            var sampleRate = request.SampleRate ?? 24000;
            var rate = request.Speed ?? 1.0;

            using var ws = new ClientWebSocket();
            if (!_options.ApiKey.IsNullOrEmpty())
                ws.Options.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            ws.Options.SetRequestHeader("user-agent", "NewLife.AI");

            var taskId = Guid.NewGuid().ToString("D");
            await foreach (var chunk in RunWebSocketTtsAsync(ws, taskId, modelCode, voice, format, sampleRate, rate, request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
        }
    }

    #endregion
}
