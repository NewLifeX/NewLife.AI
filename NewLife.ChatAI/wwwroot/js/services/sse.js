/**
 * NewLife.ChatAI — SSE 流式消息服务
 * 处理 Server-Sent Events 流式响应
 */

/**
 * SSE 事件回调类型
 * @typedef {Object} SSECallbacks
 * @property {(data: Object) => void} onMessageStart
 * @property {(data: Object) => void} onThinkingDelta
 * @property {(data: Object) => void} onThinkingDone
 * @property {(data: Object) => void} onContentDelta
 * @property {(data: Object) => void} onMessageDone
 * @property {(data: Object) => void} onToolCallStart
 * @property {(data: Object) => void} onToolCallDone
 * @property {(data: Object) => void} onToolCallError
 * @property {(data: Object) => void} onError
 */

/**
 * 发送消息并处理 SSE 流式响应
 * @param {string} url - 请求路径
 * @param {Object} body - 请求体
 * @param {SSECallbacks} callbacks - 事件回调
 * @param {AbortSignal} [signal] - 取消信号
 * @returns {Promise<void>}
 */
export async function streamChat(url, body, callbacks, signal) {
    const resp = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        signal,
    });

    if (!resp.ok) {
        let errMsg = `请求失败 (${resp.status})`;
        try {
            const err = await resp.json();
            errMsg = err.message || err.title || errMsg;
        } catch { /* 忽略 */ }
        throw new Error(errMsg);
    }

    const reader = resp.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
            if (!line.startsWith('data:')) continue;
            const payload = line.substring(5).trim();
            if (!payload) continue;

            let data;
            try {
                data = JSON.parse(payload);
            } catch {
                continue;
            }

            const handler = _eventMap[data.type];
            if (handler && callbacks[handler]) {
                callbacks[handler](data);
            }
        }
    }
}

/** 事件类型到回调方法名的映射 */
const _eventMap = {
    'message_start': 'onMessageStart',
    'thinking_delta': 'onThinkingDelta',
    'thinking_done': 'onThinkingDone',
    'content_delta': 'onContentDelta',
    'message_done': 'onMessageDone',
    'tool_call_start': 'onToolCallStart',
    'tool_call_done': 'onToolCallDone',
    'tool_call_error': 'onToolCallError',
    'error': 'onError',
};
