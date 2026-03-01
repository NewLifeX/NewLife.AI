/**
 * NewLife.ChatAI — 通用工具函数
 */

/**
 * 便捷 getElementById
 * @param {string} id
 * @returns {HTMLElement|null}
 */
export const $ = (id) => document.getElementById(id);

/**
 * HTML 转义，防止 XSS
 * @param {string} text
 * @returns {string}
 */
export function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * 相对时间格式化
 * @param {string} dateStr
 * @returns {string}
 */
export function relativeTime(dateStr) {
    const d = new Date(dateStr);
    const now = new Date();
    const diff = Math.floor((now - d) / 1000);
    if (diff < 60) return '刚刚';
    if (diff < 3600) return `${Math.floor(diff / 60)} 分钟前`;
    if (diff < 86400) return `${Math.floor(diff / 3600)} 小时前`;
    if (diff < 172800) return '昨天';
    if (diff < 604800) return `${Math.floor(diff / 86400)} 天前`;
    return d.toLocaleDateString('zh-CN');
}

/**
 * 精确时间格式化
 * @param {string} dateStr
 * @returns {string}
 */
export function exactTime(dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleString('zh-CN', {
        year: 'numeric', month: '2-digit', day: '2-digit',
        hour: '2-digit', minute: '2-digit', second: '2-digit',
    });
}

/**
 * 获取日期分组标签
 * @param {string} dateStr
 * @returns {string}
 */
export function getGroupLabel(dateStr) {
    const d = new Date(dateStr);
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const target = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const diffDays = Math.floor((today - target) / 86400000);
    if (diffDays === 0) return '今天';
    if (diffDays === 1) return '昨天';
    if (diffDays <= 7) return '过去 7 天';
    if (diffDays <= 30) return '过去 30 天';
    return '更早';
}

/**
 * Token 数量格式化
 * @param {number} n
 * @returns {string}
 */
export function formatTokenCount(n) {
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
    return String(n);
}

/**
 * 防抖
 * @param {Function} fn
 * @param {number} delay
 * @returns {Function}
 */
export function debounce(fn, delay = 300) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), delay);
    };
}

/**
 * 点击外部区域关闭
 * @param {HTMLElement} el - 需要保持的元素
 * @param {Function} onClose - 关闭回调
 * @returns {() => void} 移除监听函数
 */
export function onClickOutside(el, onClose) {
    const handler = (e) => {
        if (!el.contains(e.target)) {
            onClose();
        }
    };
    // 延迟一帧，避免触发事件被立即捕获
    requestAnimationFrame(() => {
        document.addEventListener('click', handler, true);
    });
    return () => document.removeEventListener('click', handler, true);
}
