using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.OpenAI;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

public partial class DashScopeChatClient
{
    #region WebSocket 辅助方法

    /// <summary>构建 CosyVoice WebSocket 地址。仅支持华北2（北京）MaaS 业务空间端点</summary>
    /// <remarks>
    /// 官方文档：CosyVoice WebSocket 实时语音合成仅在北京地域可用，必须使用 MaaS 业务空间端点：
    /// wss://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/api-ws/v1/inference
    /// 标准端点（dashscope.aliyuncs.com）不支持 CosyVoice WebSocket。
    /// </remarks>
    private String BuildWebSocketUrl()
    {
        // CosyVoice WebSocket 仅限北京 MaaS 端点，必须提供 Organization（工作空间 ID）
        if (_options.Organization.IsNullOrEmpty())
            throw new InvalidOperationException("CosyVoice WebSocket 实时语音合成需北京地域 MaaS 业务空间。请在 AiClientOptions.Organization 中设置工作空间 ID（阿里云百炼控制台 → 业务空间 → 复制空间ID）");

        return $"wss://{_options.Organization}.cn-beijing.maas.aliyuncs.com/api-ws/v1/inference";
    }

    /// <summary>构建 Qwen-TTS-Realtime WebSocket 连接地址。模型通过 URL 查询参数指定</summary>
    /// <remarks>
    /// 官方文档：wss://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/api-ws/v1/realtime?model={modelCode}<br/>
    /// 与 CosyVoice 不同，模型通过 URL query string 传递，不在消息体内。
    /// </remarks>
    private String BuildQwenTtsRealtimeWebSocketUrl(String modelCode)
    {
        if (_options.Organization.IsNullOrEmpty())
            throw new InvalidOperationException("Qwen-TTS Realtime WebSocket 实时语音合成需北京地域 MaaS 业务空间。请在 AiClientOptions.Organization 中设置工作空间 ID");

        return $"wss://{_options.Organization}.cn-beijing.maas.aliyuncs.com/api-ws/v1/realtime?model={Uri.EscapeDataString(modelCode)}";
    }

    /// <summary>执行 Qwen-TTS-Realtime WebSocket 全流程，逐帧产出音频块</summary>
    /// <remarks>
    /// 流程：连接 → session.created → session.update → session.updated → input_text_buffer.append（分片）
    /// → input_text_buffer.commit → input_text_buffer.committed → response.created → response.audio.delta（base64 音频，即时 yield）
    /// → response.done → session.finish → session.finished<br/>
    /// 改造要点：setup 阶段（连接+握手+发文本）使用 try-catch 跟踪错误；接收阶段使用 try-finally（无 catch），
    /// 两段独立 try 块均符合 C# 异步迭代器对 yield 的约束（yield 不得在含 catch 子句的 try 内）。
    /// </remarks>
    private async IAsyncEnumerable<Byte[]> RunQwenTtsRealtimeWebSocketAsync(ClientWebSocket ws, String modelCode, String voice, String format, SpeechRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var wsUrl = BuildQwenTtsRealtimeWebSocketUrl(modelCode);
        using var span = Tracer?.NewSpan("ai:QwenTtsRealtimeStream", new { model = modelCode, format, voice, textLength = request.Input.Length });

        // Setup 阶段：连接 + 握手 + 发送文本。此 try-catch 块内无 yield，符合 C# 规则
        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);

            // 1. 等待 session.created
            var sessionCreated = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
            if (GetEventType(sessionCreated) != "session.created")
            {
                var evType = GetEventType(sessionCreated);
                var errDetail = evType == "error" ? ExtractErrorDetail(sessionCreated) : null;
                var suffix = errDetail != null ? $"，错误详情: {errDetail}" : "";
                throw new InvalidOperationException($"Qwen-TTS Realtime 期望 session.created，实际收到 {evType ?? "(null/Close)"}{suffix}");
            }

            // 2. 发送 session.update 配置音色/格式/模式
            var sampleRate = request.SampleRate ?? 24000;
            var mode = request["mode"] as String ?? "commit";
            var languageType = request["language_type"] as String;
            var instructions = request["instructions"] as String;
            var sessionUpdateId = Guid.NewGuid().ToString("N")[..20];

            var sessionConfig = new Dictionary<String, Object>
            {
                ["voice"] = voice,
                ["mode"] = mode,
                ["response_format"] = format,
                ["sample_rate"] = sampleRate,
            };
            if (!languageType.IsNullOrEmpty()) sessionConfig["language_type"] = languageType!;
            if (!instructions.IsNullOrEmpty()) sessionConfig["instructions"] = instructions!;

            await SendRealtimeEventAsync(ws, new
            {
                event_id = sessionUpdateId,
                type = "session.update",
                session = sessionConfig,
            }, cancellationToken).ConfigureAwait(false);

            // 3. 等待 session.updated
            var sessionUpdated = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
            if (GetEventType(sessionUpdated) != "session.updated")
            {
                var evType = GetEventType(sessionUpdated);
                var errDetail = evType == "error" ? ExtractErrorDetail(sessionUpdated) : null;
                var suffix = errDetail != null ? $"，错误详情: {errDetail}" : "";
                throw new InvalidOperationException($"Qwen-TTS Realtime 期望 session.updated，实际收到 {evType ?? "(null/Close)"}{suffix}");
            }

            // 4. 分批发送文本到缓冲区（每片 ≤500 字符）
            var text = request.Input;
            var maxChunkLen = 500;
            var offset = 0;
            while (offset < text.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkLen = Math.Min(maxChunkLen, text.Length - offset);
                if (offset + chunkLen < text.Length && Char.IsHighSurrogate(text[offset + chunkLen - 1]))
                    chunkLen--;
                var chunk = text.Substring(offset, chunkLen);
                offset += chunkLen;

                await SendRealtimeEventAsync(ws, new
                {
                    event_id = Guid.NewGuid().ToString("N")[..20],
                    type = "input_text_buffer.append",
                    text = chunk,
                }, cancellationToken).ConfigureAwait(false);
            }

            // 5. 提交文本缓冲区触发合成（commit 模式必须显式提交）
            await SendRealtimeEventAsync(ws, new
            {
                event_id = Guid.NewGuid().ToString("N")[..20],
                type = "input_text_buffer.commit",
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            span?.SetError(ex, null);
            throw;
        }

        // 接收阶段：try-finally（无 catch），yield return 合法
        try
        {
            // 6. 接收音频事件流，直到 session.finished
            while (ws.State == WebSocketState.Open)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ev = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
                if (ev == null) break;

                var evType = GetEventType(ev);
                switch (evType)
                {
                    case "response.audio.delta":
                        // 音频数据在 JSON 文本帧内以 base64 编码，即时 yield 给调用方实现流式播放
                        if (ev.TryGetValue("delta", out var deltaVal) && deltaVal is String base64Audio && base64Audio.Length > 0)
                            yield return Convert.FromBase64String(base64Audio);
                        break;

                    case "response.done":
                        // 提取用量（usage.characters 或 usage.total_tokens）
                        if (ev.TryGetValue("response", out var respObj) && respObj is IDictionary<String, Object?> resp
                            && resp.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object?> usageDic)
                        {
                            var chars = 0;
                            if (chars == 0 && usageDic.TryGetValue("characters", out var uc)) chars = uc.ToInt();
                            if (chars == 0 && usageDic.TryGetValue("total_tokens", out var ut)) chars = ut.ToInt();
                            request.CharactersUsed = chars;
                        }
                        break;

                    case "session.finished":
                        yield break;

                    case "error":
                        var errMsg = "未知错误";
                        if (ev.TryGetValue("error", out var errObj) && errObj is IDictionary<String, Object?> errDic
                            && errDic.TryGetValue("message", out var em))
                            errMsg = em as String ?? errMsg;
                        throw new InvalidOperationException($"Qwen-TTS Realtime 错误: {errMsg}");

                    default:
                        // input_text_buffer.committed / response.created / response.output_item.added / response.content_part.*  等中间事件，忽略继续
                        break;
                }

                // response.done 收到后发 session.finish，然后等 session.finished
                if (evType == "response.done")
                {
                    await SendRealtimeEventAsync(ws, new
                    {
                        event_id = Guid.NewGuid().ToString("N")[..20],
                        type = "session.finish",
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // A-108：连接结束清理待消费事件队列，防止按 ws 隔离的字典条目泄漏
            _pendingEvents.TryRemove(ws, out _);
            if (ws.State == WebSocketState.Open)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
            }
        }
    }

    /// <summary>发送 Qwen-TTS Realtime JSON 事件帧</summary>
    private async Task SendRealtimeEventAsync(ClientWebSocket ws, Object eventObj, CancellationToken cancellationToken)
    {
        var json = JsonHost.Write(eventObj, JsonOptions) ?? "{}";
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<Byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>从事件字典中提取 type 字段</summary>
    private static String? GetEventType(IDictionary<String, Object?>? dic)
    {
        if (dic == null) return null;
        return dic.TryGetValue("type", out var t) ? t as String : null;
    }

    /// <summary>从 DashScope 错误事件中提取可读错误信息（message + code）</summary>
    /// <param name="ev">JSON 反序列化后的事件字典</param>
    /// <returns>格式化错误信息；提取失败返回 null</returns>
    private static String? ExtractErrorDetail(IDictionary<String, Object?>? ev)
    {
        if (ev == null) return null;
        if (!ev.TryGetValue("error", out var errObj) || errObj is not IDictionary<String, Object?> errDic)
            return null;

        var message = errDic.TryGetValue("message", out var msg) ? msg as String : null;
        var code = errDic.TryGetValue("code", out var c) ? c as String : null;

        if (message == null && code == null) return null;
        if (message != null && code != null) return $"[{code}] {message}";
        return message ?? code;
    }

    /// <summary>执行 CosyVoice WebSocket TTS 全流程，逐帧产出音频块</summary>
    private async IAsyncEnumerable<Byte[]> RunWebSocketTtsAsync(ClientWebSocket ws, String taskId, String modelCode, String voice, String format, Int32 sampleRate, Double rate, SpeechRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var wsUrl = BuildWebSocketUrl();
        using var span = Tracer?.NewSpan("ai:DashScopeTtsStream", new { model = modelCode, format, voice, textLength = request.Input.Length });

        // Setup 阶段：连接 + 握手 + 发送文本。此 try-catch 块内无 yield，符合 C# 规则
        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);

            // 发送 run-task
            await SendWebSocketJsonAsync(ws, new
            {
                header = new { task_id = taskId, action = "run-task", streaming = "duplex" },
                payload = new
                {
                    model = modelCode,
                    task_group = "audio",
                    task = "tts",
                    function = "SpeechSynthesizer",
                    parameters = new
                    {
                        text_type = "PlainText",
                        voice,
                        format,
                        sample_rate = sampleRate,
                        rate,
                        volume = request.Volume ?? 50,
                        pitch = request.Pitch ?? 1.0,
                        enable_ssml = false,
                    },
                    input = new Dictionary<String, Object>(),
                },
            }, cancellationToken).ConfigureAwait(false);

            // 等待 task-started
            var started = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
            if (started == null || GetHeaderAction(started) != "task-started")
            {
                var reason = _lastCloseReason.IsNullOrEmpty() ? "" : $"，连接关闭原因: {_lastCloseReason}";
                var extra = modelCode.StartsWith("cosyvoice-v3.5", StringComparison.OrdinalIgnoreCase)
                    ? "。v3.5 模型仅限北京地域 MaaS 业务空间（需设置 Organization），且仅支持声音复刻（自定义音色）"
                    : "";
                throw new InvalidOperationException($"CosyVoice WebSocket 期望 task-started，实际收到 {GetHeaderAction(started) ?? "(Close/非文本帧)"}{reason}{extra}");
            }

            // 文本分片发送（每片 ≤500 UTF-8 字节，保守取 ≤166 字符）
            var text = request.Input;
            var maxChunkSize = 500;
            var offset = 0;
            while (offset < text.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkLen = Math.Min(maxChunkSize / 3, text.Length - offset);
                if (offset + chunkLen < text.Length && Char.IsHighSurrogate(text[offset + chunkLen - 1]))
                    chunkLen--;
                var chunk = text.Substring(offset, chunkLen);
                offset += chunkLen;

                await SendWebSocketJsonAsync(ws, new
                {
                    header = new { task_id = taskId, action = "continue-task", streaming = "duplex" },
                    payload = new
                    {
                        input = new { text = chunk },
                    },
                }, cancellationToken).ConfigureAwait(false);
            }

            // 发送 finish-task
            await SendWebSocketJsonAsync(ws, new
            {
                header = new { task_id = taskId, action = "finish-task", streaming = "duplex" },
                payload = new { input = new Dictionary<String, Object>() },
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            span?.SetError(ex, null);
            throw;
        }

        // 接收阶段：try-finally（无 catch），yield return 合法
        try
        {
            // 循环接收 result-generated + binary / task-finished
            while (ws.State == WebSocketState.Open)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
                if (json == null) break;

                var action = GetHeaderAction(json);
                switch (action)
                {
                    case "result-generated":
                        var audioBytes = await ReceiveWebSocketBinaryAsync(ws, cancellationToken).ConfigureAwait(false);
                        if (audioBytes != null && audioBytes.Length > 0)
                            yield return audioBytes;
                        break;

                    case "task-finished":
                        if (json.TryGetValue("payload", out var fp) && fp is IDictionary<String, Object> fpDic
                            && fpDic.TryGetValue("usage", out var u) && u is IDictionary<String, Object> uDic
                            && uDic.TryGetValue("characters", out var chars))
                            request.CharactersUsed = chars.ToInt();
                        yield break;

                    case "task-failed":
                        var errMsg = "未知错误";
                        if (json.TryGetValue("payload", out var ep) && ep is IDictionary<String, Object> epDic
                            && epDic.TryGetValue("message", out var em))
                            errMsg = em as String ?? errMsg;
                        throw new InvalidOperationException($"CosyVoice WebSocket 任务失败: {errMsg}");

                    default:
                        break;
                }
            }
        }
        finally
        {
            // A-108：连接结束清理待消费事件队列，防止按 ws 隔离的字典条目泄漏
            _pendingEvents.TryRemove(ws, out _);
            if (ws.State == WebSocketState.Open)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "task-finished", CancellationToken.None).ConfigureAwait(false); } catch { }
            }
        }
    }

    /// <summary>发送 JSON 文本帧</summary>
    private async Task SendWebSocketJsonAsync(ClientWebSocket ws, Object body, CancellationToken cancellationToken)
    {
        var json = JsonHost.Write(body, JsonOptions) ?? "{}";
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<Byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>接收一条 JSON 文本帧并解析为字典。非文本帧返回 null（Close 帧通过 out 参数传出原因）</summary>
    /// <remarks>
    /// A-108：单条 WS 消息可能含多个 JSON 事件（服务端批量推送拼接，如 response.audio.delta + response.done）。
    /// JsonParser.Decode 对多顶层对象静默返回第一个，后续事件丢失——用 SplitJsonObjects 切分后全部缓存，
    /// 逐次消费，与 A-71 的 Omni 路径处理一致。
    /// </remarks>
    private async Task<IDictionary<String, Object?>?> ReceiveWebSocketJsonAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        // 优先消费已切分缓存的事件（单条消息含多事件时，后续事件在此排队）
        if (_pendingEvents.TryGetValue(ws, out var queue) && queue.Count > 0)
            return queue.Dequeue();

        var buffer = new ArraySegment<Byte>(new Byte[65536]);
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _lastCloseReason = result.CloseStatusDescription ?? result.CloseStatus?.ToString() ?? "未知";
                return null;
            }
            if (result.MessageType != WebSocketMessageType.Text)
                return null;
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());
        if (json.IsNullOrWhiteSpace()) return null;

        // A-108：切分多事件。仅一个时直接返回；多个时入队后返回首个，后续由下次调用消费
        var parts = ParseJsonEvents(json);
        if (parts.Count == 0) return null;
        if (parts.Count == 1) return parts[0];

        var q = new Queue<IDictionary<String, Object?>>();
        foreach (var part in parts)
            q.Enqueue(part);
        _pendingEvents[ws] = q;
        return q.Dequeue();
    }

    /// <summary>将一条可能含多个 JSON 事件的文本切分并逐个解析。空片段 / 解析失败片段跳过</summary>
    /// <param name="json">原始文本</param>
    /// <returns>解析后的事件字典列表</returns>
    internal static IList<IDictionary<String, Object?>> ParseJsonEvents(String json)
    {
        var list = new List<IDictionary<String, Object?>>();
        foreach (var part in DashScopeRealtimeClient.SplitJsonObjects(json))
        {
            if (part.IsNullOrWhiteSpace()) continue;
            var dic = JsonParser.Decode(part);
            if (dic != null) list.Add(dic);
        }
        return list;
    }

    /// <summary>最近一次 WebSocket Close 帧的原因描述。由 ReceiveWebSocketJsonAsync 在收到 Close 时设置</summary>
    private String _lastCloseReason = "";

    /// <summary>按连接隔离的待消费事件队列。单条 WS 消息可能含多个 JSON 事件（服务端批量推送），切分后入队逐次消费，避免静默丢弃（A-108）</summary>
    private readonly ConcurrentDictionary<ClientWebSocket, Queue<IDictionary<String, Object?>>> _pendingEvents = [];

    /// <summary>接收一条二进制帧。非二进制帧返回 null</summary>
    private async Task<Byte[]?> ReceiveWebSocketBinaryAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<Byte>(new Byte[65536]);
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType != WebSocketMessageType.Binary)
                return null;
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage);

        return ms.ToArray();
    }

    /// <summary>从 WebSocket JSON 事件字典中提取 header.action</summary>
    private String? GetHeaderAction(IDictionary<String, Object?>? dic)
    {
        if (dic == null) return null;
        if (dic.TryGetValue("header", out var headerObj) && headerObj is IDictionary<String, Object?> header
            && header.TryGetValue("action", out var action))
            return action as String;
        return null;
    }

    #endregion
}
