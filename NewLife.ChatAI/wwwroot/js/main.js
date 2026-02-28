/**
 * NewLife.ChatAI — 应用入口
 * 初始化所有组件、全局事件路由、URL 路由
 */

import { setState } from './store.js';
import { initMarkdown } from './services/markdown.js';
import { initTheme } from './components/theme.js';
import { initSidebar, loadConversations, loadUserProfile } from './components/sidebar.js';
import { initModelSelector, loadModels } from './components/models.js';
import { initWelcome, showWelcomePage } from './components/welcome.js';
import { initChat, loadMessages, sendMessage, stopGeneration, initMessageActions, loadSharePage } from './components/chat.js';
import { initComposer, focusComposer } from './components/composer.js';
import { initSettings, openSettings, loadSettings } from './components/settings.js';
import { initShare, openShare } from './components/share.js';
import { initFeedback, openFeedback } from './components/feedback.js';
import { initLightbox } from './components/lightbox.js';
import { showToast } from './components/toast.js';

/**
 * 检测登录状态，未登录时跳转登录页
 * @returns {boolean} 是否已登录（或为分享页无需登录）
 */
async function checkLogin() {
    // 分享页不需要登录
    if (window.location.pathname.match(/^\/share\//)) return true;

    try {
        const resp = await fetch('/api/user/profile');
        if (resp.status === 401) {
            const returnUrl = encodeURIComponent(window.location.href);
            window.location.href = '/admin/user/login?r=' + returnUrl;
            return false;
        }
    } catch { /* 网络异常时不阻断，后续流程会再处理 */ }
    return true;
}

/**
 * 应用初始化
 */
async function init() {
    // 0. 登录状态检测（分享页除外）
    if (!await checkLogin()) return;

    // 1. 初始化渲染相关服务（mermaid 在 initMarkdown 中初始化）
    initMarkdown();

    // 2. 初始化所有组件
    initTheme();
    initSidebar();
    initModelSelector();
    initWelcome();
    initChat();
    initMessageActions();
    initComposer();
    initSettings();
    initShare();
    initFeedback();
    initLightbox();

    // 3. 绑定全局事件路由
    _bindGlobalEvents();

    // 4. 加载基础数据（并行）
    await Promise.all([
        loadModels(),
        loadSettings(),
        loadConversations(),
        loadUserProfile(),
    ]);

    // 5. URL 路由
    _handleRoute();

    // 6. 聚焦输入
    focusComposer();
}

/**
 * 绑定全局自定义事件路由
 * 各组件通过 CustomEvent 通信，在此处统一分发
 */
function _bindGlobalEvents() {
    // 新建对话
    window.addEventListener('chatai:newConversation', () => {
        setState({ currentConversationId: null });
        showWelcomePage();
        focusComposer();
    });

    // 打开对话
    window.addEventListener('chatai:openConversation', (e) => {
        const id = e.detail?.id;
        if (id) {
            setState({ currentConversationId: id });
            loadMessages(id);
        }
    });

    // 发送消息
    window.addEventListener('chatai:sendMessage', (e) => {
        const content = e.detail?.content;
        if (content) sendMessage(content);
    });

    // 停止生成
    window.addEventListener('chatai:stopGeneration', () => {
        stopGeneration();
    });

    // 打开设置
    window.addEventListener('chatai:openSettings', () => {
        openSettings();
    });

    // 分享
    window.addEventListener('chatai:shareConversation', () => {
        openShare();
    });

    // 反馈
    window.addEventListener('chatai:openFeedback', (e) => {
        const messageId = e.detail?.messageId;
        if (messageId) openFeedback(messageId);
    });

    // 浏览器前进/后退
    window.addEventListener('popstate', _handleRoute);
}

/**
 * URL 路由
 */
function _handleRoute() {
    const path = window.location.pathname;

    const chatMatch = path.match(/^\/chat\/(\d+)$/);
    const shareMatch = path.match(/^\/share\/(.+)$/);

    if (chatMatch) {
        const id = parseInt(chatMatch[1]);
        setState({ currentConversationId: id });
        loadMessages(id);
    } else if (shareMatch) {
        loadSharePage(shareMatch[1]);
    } else {
        // 默认展示欢迎页
        showWelcomePage();
    }
}

// ── 启动 ──
init().catch(e => {
    console.error('初始化失败:', e);
    showToast('应用加载失败，请刷新重试', 'error');
});
