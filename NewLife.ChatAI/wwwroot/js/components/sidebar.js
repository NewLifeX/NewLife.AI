/**
 * NewLife.ChatAI — 侧边栏组件
 * 包含：新建对话、对话列表（分组 + 滚动加载）、会话操作、个人菜单
 */

import { $, escapeHtml, getGroupLabel } from '../utils.js';
import { getState, setState } from '../store.js';
import { api } from '../api.js';
import { showToast, showConfirm } from './toast.js';

// DOM 引用
let sidebar, sidebarToggle, sidebarOpenBtn, conversationList,
    newConversationBtn, profileBtn, profileMenu, sidebarOverlay,
    userAvatar, userName, menuAccount;

/**
 * 初始化侧边栏
 */
export function initSidebar() {
    sidebar = $('sidebar');
    sidebarToggle = $('sidebarToggle');
    sidebarOpenBtn = $('sidebarOpenBtn');
    conversationList = $('conversationList');
    newConversationBtn = $('newConversationBtn');
    profileBtn = $('profileBtn');
    profileMenu = $('profileMenu');
    sidebarOverlay = $('sidebarOverlay');
    userAvatar = $('userAvatar');
    userName = $('userName');
    menuAccount = $('menuAccount');

    // 侧边栏收起/展开
    sidebarToggle?.addEventListener('click', toggleSidebar);
    sidebarOpenBtn?.addEventListener('click', toggleSidebar);
    sidebarOverlay?.addEventListener('click', closeMobileSidebar);

    // 新建对话
    newConversationBtn?.addEventListener('click', () => {
        setState({ currentConversationId: null });
        _clearHighlight();
        // 由 main.js 监听来切换到欢迎页
        window.dispatchEvent(new CustomEvent('chatai:newConversation'));
    });

    // 个人菜单
    profileBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        profileMenu?.classList.toggle('hidden');
    });
    document.addEventListener('click', () => {
        profileMenu?.classList.add('hidden');
    });

    // 设置按钮
    $('menuSettings')?.addEventListener('click', () => {
        profileMenu?.classList.add('hidden');
        window.dispatchEvent(new CustomEvent('chatai:openSettings'));
    });

    // 退出登录
    $('menuLogout')?.addEventListener('click', () => {
        profileMenu?.classList.add('hidden');
        // 本期暂未启用登录鉴权，仅提示
        window.dispatchEvent(new CustomEvent('chatai:logout'));
    });

    // 对话列表滚动加载
    conversationList?.addEventListener('scroll', () => {
        const { scrollTop, scrollHeight, clientHeight } = conversationList;
        if (scrollHeight - scrollTop - clientHeight < 50) {
            loadConversations(false);
        }
    });

    // 响应窗口尺寸变化
    window.addEventListener('resize', _handleResize);

    // 初始化时处理小屏状态
    _handleResize();
}

/** 根据屏幕宽度更新侧边栏状态 */
function _handleResize() {
    const isMobile = window.innerWidth <= 768;
    if (isMobile) {
        // 小屏：关闭浮层，侧边栏收起但保留汉堡按钮可用
        sidebar?.classList.remove('mobile-open');
        sidebarOverlay?.classList.add('hidden');
    } else {
        // 大屏：移除移动端状态，恢复展开/折叠按钮
        sidebar?.classList.remove('mobile-open');
        sidebarOverlay?.classList.add('hidden');
        sidebarOpenBtn?.classList.toggle('hidden', !sidebar?.classList.contains('collapsed'));
    }
}

/**
 * 切换侧边栏显示/隐藏
 */
export function toggleSidebar() {
    const isMobile = window.innerWidth <= 768;
    if (isMobile) {
        const isOpen = sidebar.classList.toggle('mobile-open');
        sidebarOverlay?.classList.toggle('hidden', !isOpen);
    } else {
        const willCollapse = !sidebar.classList.contains('collapsed');
        sidebar.classList.toggle('collapsed');
        sidebarOpenBtn?.classList.toggle('hidden', !willCollapse);
        setState({ sidebarOpen: !willCollapse });
    }
}

/**
 * 关闭移动端侧边栏
 */
export function closeMobileSidebar() {
    sidebar?.classList.remove('mobile-open');
    sidebarOverlay?.classList.add('hidden');
}

/**
 * 加载用户资料（头像/昵称/帐号）
 */
export async function loadUserProfile() {
    try {
        const profile = await api('/api/user/profile');
        if (profile) {
            // 更新头像（取昵称首字）
            if (userAvatar) {
                const initial = (profile.nickname || profile.account || 'U').charAt(0).toUpperCase();
                userAvatar.textContent = initial;
            }
            // 更新昵称
            if (userName) userName.textContent = profile.nickname || '用户';
            // 更新菜单顶部帐号
            const accountTextEl = $('menuAccountText');
            if (accountTextEl) accountTextEl.textContent = profile.account || '';
        }
    } catch { /* 使用默认值 */ }
}

/**
 * 加载对话列表
 * @param {boolean} [reset=true] - 是否重置（从第一页加载）
 */
export async function loadConversations(reset = true) {
    const state = getState();
    if (state.conversationLoading) return;
    if (reset) {
        setState({ conversationPage: 1, conversationHasMore: true, conversations: [] });
    }
    if (!getState().conversationHasMore) return;

    setState({ conversationLoading: true });

    try {
        const page = getState().conversationPage;
        const result = await api(`/api/conversations?page=${page}&pageSize=20`);
        const conversations = reset ? result.items : [...getState().conversations, ...result.items];
        setState({
            conversations,
            conversationHasMore: conversations.length < result.total,
            conversationPage: page + 1,
        });
        renderConversationList();
    } catch (e) {
        showToast('加载对话列表失败: ' + e.message, 'error');
    } finally {
        setState({ conversationLoading: false });
    }
}

/**
 * 渲染对话列表
 */
export function renderConversationList() {
    if (!conversationList) return;
    conversationList.innerHTML = '';

    const { conversations, currentConversationId } = getState();
    let lastGroup = '';

    for (const conv of conversations) {
        // 分组标签
        const group = getGroupLabel(conv.lastMessageTime);
        if (group !== lastGroup) {
            lastGroup = group;
            const label = document.createElement('div');
            label.className = 'conv-group-label';
            label.textContent = group;
            conversationList.appendChild(label);
        }

        // 对话项
        const item = document.createElement('div');
        item.className = `conv-item${conv.id === currentConversationId ? ' active' : ''}`;
        item.dataset.id = conv.id;
        item.innerHTML = `
            <span class="conv-item-title">${escapeHtml(conv.title)}</span>
            <div class="conv-item-actions">
                <button title="重命名" data-action="rename">✏</button>
                <button title="删除" data-action="delete">🗑</button>
            </div>`;

        // 点击打开对话
        item.addEventListener('click', (e) => {
            if (e.target.closest('[data-action]')) return;
            openConversation(conv.id);
            if (window.innerWidth <= 768) closeMobileSidebar();
        });

        // 重命名
        item.querySelector('[data-action="rename"]')?.addEventListener('click', (e) => {
            e.stopPropagation();
            _startRename(item, conv);
        });

        // 删除
        item.querySelector('[data-action="delete"]')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const ok = await showConfirm('删除对话', `确定删除"${conv.title}"？此操作不可撤销。`);
            if (!ok) return;
            try {
                await api(`/api/conversations/${conv.id}`, { method: 'DELETE' });
                if (getState().currentConversationId === conv.id) {
                    setState({ currentConversationId: null });
                    window.dispatchEvent(new CustomEvent('chatai:newConversation'));
                }
                await loadConversations();
                showToast('对话已删除');
            } catch (err) {
                showToast('删除失败: ' + err.message, 'error');
            }
        });

        conversationList.appendChild(item);
    }
}

/**
 * 打开指定对话
 * @param {number} id
 */
export function openConversation(id) {
    if (getState().currentConversationId === id) return;
    setState({ currentConversationId: id });
    _highlightConversation(id);
    window.dispatchEvent(new CustomEvent('chatai:openConversation', { detail: { id } }));
}

/**
 * 更新对话标题（SSE 返回标题后调用）
 * @param {string} title
 */
export function updateConversationTitle(title) {
    const id = getState().currentConversationId;
    if (!id) return;
    const item = conversationList?.querySelector(`[data-id="${id}"]`);
    if (item) {
        const titleEl = item.querySelector('.conv-item-title');
        if (titleEl) titleEl.textContent = title;
    }
}

/** 高亮当前对话 */
function _highlightConversation(id) {
    conversationList?.querySelectorAll('.conv-item').forEach(el => {
        el.classList.toggle('active', el.dataset.id == id);
    });
}

/** 清除所有高亮 */
function _clearHighlight() {
    conversationList?.querySelectorAll('.conv-item').forEach(el => {
        el.classList.remove('active');
    });
}

/** 内联重命名 */
function _startRename(item, conv) {
    const titleEl = item.querySelector('.conv-item-title');
    const oldTitle = conv.title;
    const input = document.createElement('input');
    input.className = 'conv-item-edit';
    input.value = oldTitle;
    titleEl.replaceWith(input);
    input.focus();
    input.select();

    const finish = async () => {
        const newTitle = input.value.trim() || oldTitle;
        const span = document.createElement('span');
        span.className = 'conv-item-title';
        span.textContent = newTitle;
        input.replaceWith(span);
        if (newTitle !== oldTitle) {
            try {
                await api(`/api/conversations/${conv.id}`, { method: 'PUT', body: { title: newTitle } });
                conv.title = newTitle;
            } catch (err) {
                span.textContent = oldTitle;
                showToast('重命名失败: ' + err.message, 'error');
            }
        }
    };

    input.addEventListener('blur', finish);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
        if (e.key === 'Escape') { input.value = oldTitle; input.blur(); }
    });
}
