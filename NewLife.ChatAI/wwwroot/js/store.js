/**
 * NewLife.ChatAI — 响应式状态管理
 * 轻量级发布/订阅模式的状态存储
 */

const _state = {
    // UI 状态
    sidebarOpen: true,
    theme: 'system',          // 'light' | 'dark' | 'system'

    // 会话
    currentConversationId: null,
    conversations: [],
    conversationPage: 1,
    conversationHasMore: true,
    conversationLoading: false,

    // 模型
    models: [],
    currentModel: null,
    thinkingMode: 0,           // 0=自动 1=思考 2=快速

    // 生成中
    isGenerating: false,
    currentMessageId: null,
    abortController: null,

    // 附件
    pendingAttachments: [],

    // 设置
    settings: null,
    sendShortcut: 'Enter',

    // 图片预览
    lightboxImages: [],
    lightboxIndex: 0,

    // 滚动
    userAutoScroll: true,
};

/** @type {Object<string, Function[]>} */
const _listeners = {};

/**
 * 获取当前状态的只读快照
 * @returns {typeof _state}
 */
export function getState() {
    return _state;
}

/**
 * 批量更新状态并通知相关监听器
 * @param {Partial<typeof _state>} updates
 */
export function setState(updates) {
    const changedKeys = [];
    for (const key of Object.keys(updates)) {
        if (_state[key] !== updates[key]) {
            _state[key] = updates[key];
            changedKeys.push(key);
        }
    }
    // 按 key 通知
    for (const key of changedKeys) {
        if (_listeners[key]) {
            for (const fn of _listeners[key]) {
                try { fn(_state[key], _state); } catch (e) { console.error(`[Store] listener error (${key}):`, e); }
            }
        }
    }
    // 全局通知
    if (changedKeys.length > 0 && _listeners['*']) {
        for (const fn of _listeners['*']) {
            try { fn(_state); } catch (e) { console.error('[Store] global listener error:', e); }
        }
    }
}

/**
 * 订阅指定 key 的变化，返回取消订阅函数
 * @param {string} key - 状态字段名，'*' 表示监听所有变化
 * @param {Function} fn - 回调 (newValue, fullState) => void
 * @returns {() => void} 取消订阅函数
 */
export function subscribe(key, fn) {
    if (!_listeners[key]) _listeners[key] = [];
    _listeners[key].push(fn);
    return () => {
        _listeners[key] = _listeners[key].filter(f => f !== fn);
    };
}

/**
 * 直接修改单个状态字段（不触发通知，用于内部高频更新）
 * @param {string} key
 * @param {*} value
 */
export function setStateSilent(key, value) {
    _state[key] = value;
}
