/**
 * NewLife.ChatAI — API 客户端
 * 统一封装所有后端 HTTP 请求
 */

/**
 * 通用 API 请求
 * @param {string} path - 请求路径
 * @param {Object} [options]
 * @param {string} [options.method='GET']
 * @param {Object|FormData} [options.body]
 * @param {boolean} [options.raw=false] - 返回原始 Response
 * @param {AbortSignal} [options.signal]
 * @returns {Promise<any>}
 */
export async function api(path, options = {}) {
    const { method = 'GET', body, raw = false, signal } = options;
    const headers = {};

    if (body && !(body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
    }

    const resp = await fetch(path, {
        method,
        headers,
        body: body instanceof FormData ? body : (body ? JSON.stringify(body) : undefined),
        signal,
    });

    if (!resp.ok) {
        let errMsg = `请求失败 (${resp.status})`;
        try {
            const err = await resp.json();
            errMsg = err.message || err.title || errMsg;
        } catch { /* 解析失败时使用默认消息 */ }
        throw new Error(errMsg);
    }

    if (raw) return resp;
    if (resp.status === 204) return null;

    const ct = resp.headers.get('content-type') || '';
    if (ct.includes('json')) return resp.json();
    return resp;
}
