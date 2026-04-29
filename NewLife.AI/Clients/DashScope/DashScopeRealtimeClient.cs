using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

/// <summary>DashScope Omni 实时对话客户端（WebSocket）。用于 qwen-omni-*-realtime 系列模型的低延迟语音+视觉对话</summary>
/// <remarks>
/// 实时 API 协议基于 WebSocket 事件驱动，与非实时 Omni 模型的 HTTP 流式 API 完全不同：
/// <list type="bullet">
/// <item>连接端点：wss://dashscope.aliyuncs.com/api-ws/v1/realtime?model={model}</item>
/// <item>认证方式：Authorization: Bearer {apiKey} 请求头</item>
/// <item>通信模式：全双工，客户端持续发送音频/图像帧，服务端实时推送文本+音频响应块</item>
/// <item>会话限制：最大 120 分钟，各模型有独立的最大音频轮次限制</item>
/// </list>
/// 可用模型：qwen3.5-omni-plus-realtime / qwen3.5-omni-flash-realtime / qwen3-omni-flash-realtime<br/>
/// 官方文档：https://help.aliyun.com/zh/model-studio/realtime
/// </remarks>
public sealed class DashScopeRealtimeClient : IDisposable
{
    #region 属性
    /// <summary>API 密钥</summary>
    private readonly String _apiKey;

    /// <summary>实时 API WebSocket 基础地址</summary>
    public static String RealtimeEndpoint { get; set; } = "wss://dashscope.aliyuncs.com/api-ws/v1/realtime";

    /// <summary>当前连接的模型标识</summary>
    public String? Model { get; private set; }

    /// <summary>底层 WebSocket 实例</summary>
    private ClientWebSocket? _ws;

    /// <summary>接收循环取消令牌</summary>
    private CancellationTokenSource? _cts;
    #endregion

    #region 构造
    /// <summary>以 API 密钥初始化 DashScope 实时客户端</summary>
    /// <param name="apiKey">阿里云 API Key</param>
    public DashScopeRealtimeClient(String apiKey) => _apiKey = apiKey;
    #endregion

    #region 连接管理
    /// <summary>连接到指定模型的实时对话端点</summary>
    /// <param name="model">实时模型标识，如 qwen3.5-omni-plus-realtime</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ConnectAsync(String model, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));
        if (!model.Contains("-realtime", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"模型 '{model}' 不是实时模型，实时模型名称应包含 '-realtime'", nameof(model));

        _ws?.Dispose();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", "Bearer " + _apiKey);

        var uri = new Uri($"{RealtimeEndpoint}?model={Uri.EscapeDataString(model)}");
        await ws.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

        _ws = ws;
        Model = model;
    }

    /// <summary>断开 WebSocket 连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_ws is { State: WebSocketState.Open })
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region 事件发送
    /// <summary>更新会话配置。可设置语音、VAD 模式、输出格式等参数</summary>
    /// <param name="session">会话配置对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task UpdateSessionAsync(RealtimeSessionConfig session, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<String, Object>
        {
            ["type"] = "session.update",
            ["session"] = session.ToDict(),
        };
        await SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>追加音频数据到缓冲区。连续调用以流式传输音频帧</summary>
    /// <param name="audioBase64">Base64 编码的 PCM16 音频数据（16kHz 单声道）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AppendAudioAsync(String audioBase64, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<String, Object>
        {
            ["type"] = "input_audio_buffer.append",
            ["audio"] = audioBase64,
        };
        await SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>追加图像帧到缓冲区。用于多模态输入场景</summary>
    /// <param name="imageBase64">Base64 编码的图像数据（JPEG/PNG）</param>
    /// <param name="mediaType">媒体类型，如 image/jpeg（默认）或 image/png</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AppendImageAsync(String imageBase64, String mediaType = "image/jpeg", CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<String, Object>
        {
            ["type"] = "input_image_buffer.append",
            ["image"] = imageBase64,
            ["media_type"] = mediaType,
        };
        await SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>提交音频缓冲区，触发模型推理。在非 VAD 模式下需手动调用</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CommitAudioAsync(CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<String, Object> { ["type"] = "input_audio_buffer.commit" };
        await SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>发送原始事件 JSON。适用于协议扩展或自定义事件</summary>
    /// <param name="payload">事件字典，将序列化为 JSON</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SendEventAsync(IDictionary<String, Object> payload, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var json = payload.ToJson();
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(new ArraySegment<Byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 事件接收
    /// <summary>持续接收服务端推送的实时事件。使用 <c>await foreach</c> 迭代直至连接关闭</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实时事件的异步枚举</returns>
    public async IAsyncEnumerable<RealtimeEvent> ReceiveEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var buffer = new Byte[16 * 1024];
        var sb = new StringBuilder();

        while (_ws!.State == WebSocketState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WebSocketReceiveResult result;
            try
            {
                result = await _ws.ReceiveAsync(new ArraySegment<Byte>(buffer), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (WebSocketException)
            {
                yield break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                yield break;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (!result.EndOfMessage) continue;

            var json = sb.ToString();
            sb.Clear();

            if (String.IsNullOrWhiteSpace(json)) continue;

            var evt = RealtimeEvent.Parse(json);
            if (evt != null) yield return evt;
        }
    }
    #endregion

    #region 辅助
    private void EnsureConnected()
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket 未连接，请先调用 ConnectAsync");
    }
    #endregion

    #region 资源释放
    /// <summary>释放 WebSocket 连接及相关资源</summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _ws?.Dispose();
        _cts = null;
        _ws = null;
    }
    #endregion
}

/// <summary>DashScope 实时会话配置。通过 session.update 事件发送给服务端</summary>
public class RealtimeSessionConfig
{
    /// <summary>语音合成音色。如 "Chelsie"（en-US）/ "Cherry"（zh-CN）等</summary>
    public String? Voice { get; set; }

    /// <summary>语音活动检测（VAD）模式。server_vad（服务端检测，默认）或 semantic_vad（语义感知检测）</summary>
    public String? TurnDetectionType { get; set; }

    /// <summary>TTS 音频输出格式。pcm16（默认）/ g711_ulaw / g711_alaw</summary>
    public String? OutputAudioFormat { get; set; }

    /// <summary>最大响应 Token 数</summary>
    public Int32? MaxResponseOutputTokens { get; set; }

    /// <summary>系统提示词。设置 AI 的角色和行为指引</summary>
    public String? Instructions { get; set; }

    /// <summary>转换为事件字典</summary>
    /// <returns>可直接序列化的字典，仅包含非空字段</returns>
    public IDictionary<String, Object> ToDict()
    {
        var dic = new Dictionary<String, Object>();
        if (!String.IsNullOrEmpty(Voice)) dic["voice"] = Voice!;
        if (!String.IsNullOrEmpty(OutputAudioFormat)) dic["output_audio_format"] = OutputAudioFormat!;
        if (!String.IsNullOrEmpty(Instructions)) dic["instructions"] = Instructions!;
        if (MaxResponseOutputTokens != null) dic["max_response_output_tokens"] = MaxResponseOutputTokens.Value;
        if (!String.IsNullOrEmpty(TurnDetectionType))
            dic["turn_detection"] = new Dictionary<String, Object> { ["type"] = TurnDetectionType! };
        return dic;
    }
}

/// <summary>实时 API 服务端推送事件</summary>
public class RealtimeEvent
{
    /// <summary>事件类型。如 session.created、response.audio.delta、response.done 等</summary>
    public String Type { get; set; } = null!;

    /// <summary>事件 ID</summary>
    public String? EventId { get; set; }

    /// <summary>会话 ID</summary>
    public String? SessionId { get; set; }

    /// <summary>响应 ID</summary>
    public String? ResponseId { get; set; }

    /// <summary>选择项索引</summary>
    public Int32 ItemIndex { get; set; }

    /// <summary>增量文本内容（response.text.delta）</summary>
    public String? TextDelta { get; set; }

    /// <summary>增量音频数据，Base64 编码（response.audio.delta）</summary>
    public String? AudioDelta { get; set; }

    /// <summary>音频转录文本（response.audio_transcript.delta）</summary>
    public String? TranscriptDelta { get; set; }

    /// <summary>原始事件字典。包含所有字段，用于访问未映射的属性</summary>
    public IDictionary<String, Object?>? Raw { get; set; }

    /// <summary>解析服务端推送的事件 JSON</summary>
    /// <param name="json">事件 JSON 字符串</param>
    /// <returns>解析后的事件对象；解析失败时返回 null</returns>
    public static RealtimeEvent? Parse(String json)
    {
        IDictionary<String, Object?>? dic;
        try { dic = JsonParser.Decode(json) as IDictionary<String, Object?>; }
        catch { return null; }

        if (dic == null) return null;

        var evt = new RealtimeEvent
        {
            Type = dic["type"] as String ?? "",
            EventId = dic["event_id"] as String,
            Raw = dic,
        };

        if (dic["session"] is IDictionary<String, Object?> session)
            evt.SessionId = session["id"] as String;

        if (dic["response"] is IDictionary<String, Object?> response)
            evt.ResponseId = response["id"] as String;

        // response.text.delta / response.audio.delta / response.audio_transcript.delta
        if (dic["delta"] is String delta)
        {
            if (evt.Type == "response.text.delta") evt.TextDelta = delta;
            else if (evt.Type == "response.audio.delta") evt.AudioDelta = delta;
            else if (evt.Type == "response.audio_transcript.delta") evt.TranscriptDelta = delta;
        }

        evt.ItemIndex = (dic["item_index"] ?? dic["output_index"])?.ToInt() ?? 0;

        return evt;
    }
}
