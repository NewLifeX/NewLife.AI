/* ========================================
   NewLife.ChatAI — 前端核心逻辑
   ======================================== */

// ── 状态管理 ──
const state = {
    currentConversationId: null,
    models: [],
    currentModel: null,
    thinkingMode: 0,          // 0=自动 1=思考 2=快速
    isGenerating: false,
    currentMessageId: null,    // 正在生成的消息ID
    abortController: null,
    conversations: [],
    conversationPage: 1,
    conversationHasMore: true,
    conversationLoading: false,
    pendingAttachments: [],
    settings: null,
    userAutoScroll: true,
    lightboxImages: [],
    lightboxIndex: 0,
    sendShortcut: 'Enter',
};

// ── DOM 引用 ──
const $ = id => document.getElementById(id);
const sidebar = $('sidebar');
const sidebarToggle = $('sidebarToggle');
const sidebarOpenBtn = $('sidebarOpenBtn');
const newConversationBtn = $('newConversation');
const conversationList = $('conversationList');
const profileBtn = $('profileBtn');
const profileMenu = $('profileMenu');
const menuSettings = $('menuSettings');
const modelSelectorBtn = $('modelSelectorBtn');
const modelDropdown = $('modelDropdown');
const currentModelName = $('currentModelName');
const welcomePage = $('welcomePage');
const messagesEl = $('messages');
const messagesInner = $('messagesInner');
const scrollBottomBtn = $('scrollBottomBtn');
const suggestedQuestions = $('suggestedQuestions');
const composer = $('composer');
const attachmentPreview = $('attachmentPreview');
const uploadBtn = $('uploadBtn');
const fileInput = $('fileInput');
const promptInput = $('promptInput');
const charCount = $('charCount');
const thinkingModeBtn = $('thinkingModeBtn');
const thinkingModeLabel = $('thinkingModeLabel');
const thinkingModeDropdown = $('thinkingModeDropdown');
const sendBtn = $('sendBtn');
const sendIcon = $('sendIcon');
const dropOverlay = $('dropOverlay');
const sidebarOverlay = $('sidebarOverlay');
const toastContainer = $('toastContainer');

// ── API 封装 ──
async function api(path, options = {}) {
    const { method = 'GET', body, raw = false } = options;
    const headers = {};
    if (body && !(body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
    }
    const resp = await fetch(path, {
        method,
        headers,
        body: body instanceof FormData ? body : (body ? JSON.stringify(body) : undefined),
    });
    if (!resp.ok) {
        let errMsg = `请求失败 (${resp.status})`;
        try {
            const err = await resp.json();
            errMsg = err.message || err.title || errMsg;
        } catch { /* 忽略 */ }
        throw new Error(errMsg);
    }
    if (raw) return resp;
    if (resp.status === 204) return null;
    const ct = resp.headers.get('content-type') || '';
    if (ct.includes('json')) return resp.json();
    return resp;
}

// ── Toast 通知 ──
function showToast(message, type = '') {
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.textContent = message;
    toastContainer.appendChild(el);
    setTimeout(() => {
        el.style.animation = 'toastOut .3s ease forwards';
        el.addEventListener('animationend', () => el.remove());
    }, 3000);
}

// ── 确认弹窗 ──
function showConfirm(title, message) {
    return new Promise(resolve => {
        $('confirmTitle').textContent = title;
        $('confirmMessage').textContent = message;
        $('confirmModal').classList.remove('hidden');
        const onOk = () => { cleanup(); resolve(true); };
        const onCancel = () => { cleanup(); resolve(false); };
        const cleanup = () => {
            $('confirmModal').classList.add('hidden');
            $('confirmOk').removeEventListener('click', onOk);
            $('confirmCancel').removeEventListener('click', onCancel);
        };
        $('confirmOk').addEventListener('click', onOk);
        $('confirmCancel').addEventListener('click', onCancel);
    });
}

// ── 相对时间 ──
function relativeTime(dateStr) {
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

function exactTime(dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

// ── 分组标签 ──
function getGroupLabel(dateStr) {
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

// ── Markdown 渲染 ──
function initMarkdown() {
    if (typeof marked === 'undefined') return;

    const renderer = new marked.Renderer();

    // 代码块：加一键复制按钮
    renderer.code = function ({ text, lang }) {
        const langClass = lang ? `language-${lang}` : '';
        let highlighted = text;
        if (typeof hljs !== 'undefined' && lang && hljs.getLanguage(lang)) {
            try { highlighted = hljs.highlight(text, { language: lang }).value; } catch { /* 忽略 */ }
        }
        return `<pre><code class="hljs ${langClass}">${highlighted}</code><button class="code-copy-btn" onclick="copyCode(this)">复制</button></pre>`;
    };

    // 图片：支持点击预览
    renderer.image = function ({ href, title, text }) {
        return `<img src="${href}" alt="${text || ''}" title="${title || ''}" onclick="openLightbox(this.src)" loading="lazy" />`;
    };

    marked.setOptions({
        renderer,
        breaks: true,
        gfm: true,
    });
}

function renderMarkdown(text) {
    if (!text) return '';
    if (typeof marked === 'undefined') return escapeHtml(text);

    // 保护 LaTeX 公式不被 marked 处理
    const blocks = [];
    let safe = text.replace(/\$\$([\s\S]*?)\$\$/g, (_, m) => { blocks.push(m); return `%%BLOCK_${blocks.length - 1}%%`; });
    safe = safe.replace(/\$([^\n$]+?)\$/g, (_, m) => { blocks.push(m); return `%%INLINE_${blocks.length - 1}%%`; });

    let html = marked.parse(safe);

    // 恢复 LaTeX
    html = html.replace(/%%BLOCK_(\d+)%%/g, (_, i) => {
        try { return katex.renderToString(blocks[i], { displayMode: true, throwOnError: false }); } catch { return `$$${blocks[i]}$$`; }
    });
    html = html.replace(/%%INLINE_(\d+)%%/g, (_, i) => {
        try { return katex.renderToString(blocks[i], { displayMode: false, throwOnError: false }); } catch { return `$${blocks[i]}$`; }
    });

    return html;
}

// 处理 Mermaid 图表
function processMermaid(container) {
    if (typeof mermaid === 'undefined') return;
    const codeBlocks = container.querySelectorAll('code.language-mermaid');
    codeBlocks.forEach((block, i) => {
        const pre = block.parentElement;
        const div = document.createElement('div');
        div.className = 'mermaid';
        div.textContent = block.textContent;
        pre.replaceWith(div);
    });
    try { mermaid.run({ querySelector: '.mermaid' }); } catch { /* 忽略 */ }
}

function copyCode(btn) {
    const code = btn.previousElementSibling;
    navigator.clipboard.writeText(code.textContent).then(() => {
        btn.textContent = '已复制';
        btn.classList.add('copied');
        setTimeout(() => { btn.textContent = '复制'; btn.classList.remove('copied'); }, 2000);
    });
}
// 暴露到全局
window.copyCode = copyCode;

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ── 模型管理 ──
async function loadModels() {
    try {
        state.models = await api('/api/models');
        renderModelDropdown();
        populateSettingsModels();
        // 默认选第一个
        if (state.models.length > 0 && !state.currentModel) {
            selectModel(state.models[0]);
        }
    } catch (e) {
        showToast('加载模型列表失败: ' + e.message, 'error');
    }
}

function renderModelDropdown() {
    modelDropdown.innerHTML = '';
    for (const m of state.models) {
        const opt = document.createElement('div');
        opt.className = `model-option${state.currentModel?.code === m.code ? ' active' : ''}`;
        const badges = [];
        if (m.supportImageGeneration) badges.push('🖼️');
        if (m.supportVision) badges.push('👁️');
        if (m.supportThinking) badges.push('🧠');
        if (m.supportFunctionCalling) badges.push('🔧');
        opt.innerHTML = `<span class="model-option-name">${escapeHtml(m.name)}</span>
            <span class="model-option-badges">${badges.join(' ')}</span>`;
        opt.addEventListener('click', () => {
            selectModel(m);
            modelDropdown.classList.add('hidden');
        });
        modelDropdown.appendChild(opt);
    }
}

function selectModel(model) {
    state.currentModel = model;
    currentModelName.textContent = model.name;
    // 更新下拉选中态
    modelDropdown.querySelectorAll('.model-option').forEach(el => el.classList.remove('active'));
    const options = modelDropdown.querySelectorAll('.model-option');
    for (const opt of options) {
        if (opt.querySelector('.model-option-name')?.textContent === model.name) {
            opt.classList.add('active');
        }
    }
    // 思考模式联动
    updateThinkingModeAvailability();
}

function updateThinkingModeAvailability() {
    const thinkOpt = thinkingModeDropdown.querySelector('[data-mode="1"]');
    if (state.currentModel?.supportThinking) {
        thinkOpt.classList.remove('disabled');
    } else {
        thinkOpt.classList.add('disabled');
        if (state.thinkingMode === 1) {
            setThinkingMode(0);
        }
    }
}

function setThinkingMode(mode) {
    state.thinkingMode = mode;
    const labels = { 0: '自动', 1: '思考', 2: '快速' };
    thinkingModeLabel.textContent = labels[mode];
    thinkingModeDropdown.querySelectorAll('.dropdown-item').forEach(el => {
        el.classList.toggle('active', parseInt(el.dataset.mode) === mode);
    });
}

// ── 侧边栏 ──
function toggleSidebar() {
    const isMobile = window.innerWidth <= 768;
    if (isMobile) {
        const isOpen = sidebar.classList.toggle('mobile-open');
        sidebarOverlay.classList.toggle('hidden', !isOpen);
    } else {
        sidebar.classList.toggle('collapsed');
        sidebarOpenBtn.classList.toggle('hidden', !sidebar.classList.contains('collapsed'));
    }
}

function closeMobileSidebar() {
    sidebar.classList.remove('mobile-open');
    sidebarOverlay.classList.add('hidden');
}

// ── 对话列表 ──
async function loadConversations(reset = true) {
    if (state.conversationLoading) return;
    if (reset) {
        state.conversationPage = 1;
        state.conversationHasMore = true;
        state.conversations = [];
    }
    if (!state.conversationHasMore) return;

    state.conversationLoading = true;
    try {
        const result = await api(`/api/conversations?page=${state.conversationPage}&pageSize=20`);
        state.conversations = reset ? result.items : [...state.conversations, ...result.items];
        state.conversationHasMore = state.conversations.length < result.total;
        state.conversationPage++;
        renderConversationList();
    } catch (e) {
        showToast('加载对话列表失败: ' + e.message, 'error');
    } finally {
        state.conversationLoading = false;
    }
}

function renderConversationList() {
    conversationList.innerHTML = '';
    let lastGroup = '';
    for (const conv of state.conversations) {
        const group = getGroupLabel(conv.lastMessageTime);
        if (group !== lastGroup) {
            lastGroup = group;
            const label = document.createElement('div');
            label.className = 'conv-group-label';
            label.textContent = group;
            conversationList.appendChild(label);
        }
        const item = document.createElement('div');
        item.className = `conv-item${conv.id === state.currentConversationId ? ' active' : ''}`;
        item.dataset.id = conv.id;
        item.innerHTML = `
            <span class="conv-item-title">${escapeHtml(conv.title)}</span>
            <div class="conv-item-actions">
                <button title="重命名" data-action="rename">✏</button>
                <button title="删除" data-action="delete">🗑</button>
            </div>`;
        item.addEventListener('click', e => {
            if (e.target.closest('[data-action]')) return;
            openConversation(conv.id);
            // 移动端自动关闭侧边栏
            if (window.innerWidth <= 768) closeMobileSidebar();
        });
        // 重命名
        item.querySelector('[data-action="rename"]')?.addEventListener('click', e => {
            e.stopPropagation();
            startRename(item, conv);
        });
        // 删除
        item.querySelector('[data-action="delete"]')?.addEventListener('click', async e => {
            e.stopPropagation();
            const ok = await showConfirm('删除对话', `确定删除"${conv.title}"？此操作不可撤销。`);
            if (!ok) return;
            try {
                await api(`/api/conversations/${conv.id}`, { method: 'DELETE' });
                if (state.currentConversationId === conv.id) {
                    state.currentConversationId = null;
                    showWelcomePage();
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

function startRename(item, conv) {
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
    input.addEventListener('keydown', e => {
        if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
        if (e.key === 'Escape') { input.value = oldTitle; input.blur(); }
    });
}

// 滚动加载更多
conversationList.addEventListener('scroll', () => {
    const { scrollTop, scrollHeight, clientHeight } = conversationList;
    if (scrollHeight - scrollTop - clientHeight < 50) {
        loadConversations(false);
    }
});

// ── 会话操作 ──
async function createConversation() {
    try {
        const result = await api('/api/conversations', {
            method: 'POST',
            body: { title: '新建对话', modelCode: state.currentModel?.code }
        });
        state.currentConversationId = result.id;
        showChatView();
        messagesInner.innerHTML = '';
        await loadConversations();
        promptInput.focus();
        updateUrl();
    } catch (e) {
        showToast('创建对话失败: ' + e.message, 'error');
    }
}

async function openConversation(id) {
    if (state.currentConversationId === id) return;
    state.currentConversationId = id;
    showChatView();
    messagesInner.innerHTML = '';
    updateUrl();

    // 高亮当前项
    conversationList.querySelectorAll('.conv-item').forEach(el => {
        el.classList.toggle('active', el.dataset.id == id);
    });

    try {
        const messages = await api(`/api/conversations/${id}/messages`);
        for (const msg of messages) {
            renderMessage(msg);
        }
        // 仅最后一条 AI 回复显示重新生成按钮
        addRegenerateToLastAi();
        scrollToBottom(true);
    } catch (e) {
        showToast('加载消息失败: ' + e.message, 'error');
    }
}

function showWelcomePage() {
    welcomePage.classList.remove('hidden');
    messagesEl.classList.add('hidden');
    scrollBottomBtn.classList.add('hidden');
    updateUrl();
}

function showChatView() {
    welcomePage.classList.add('hidden');
    messagesEl.classList.remove('hidden');
}

function updateUrl() {
    const path = state.currentConversationId ? `/chat/${state.currentConversationId}` : '/chat';
    if (window.location.pathname !== path) {
        history.pushState(null, '', path);
    }
}

// ── 消息渲染 ──
function renderMessage(msg) {
    const div = document.createElement('div');
    div.className = `msg ${msg.role}`;
    div.dataset.messageId = msg.id;

    const isUser = msg.role === 'user';
    const avatarContent = isUser ? 'U' : '🤖';

    let contentHtml = '';
    if (isUser) {
        contentHtml = `<div class="msg-content">${escapeHtml(msg.content)}</div>`;
    } else {
        contentHtml = `<div class="msg-content md-content">${renderMarkdown(msg.content)}</div>`;
    }

    // 附件展示
    let attachmentsHtml = '';
    if (msg.attachments) {
        try {
            const ids = JSON.parse(msg.attachments);
            if (Array.isArray(ids) && ids.length > 0) {
                attachmentsHtml = '<div class="msg-attachments">' + ids.map(id => {
                    return `<div class="msg-attach-item">
                        <a href="/api/attachments/${id}" target="_blank" class="msg-attach-link">📎 附件 #${id}</a>
                    </div>`;
                }).join('') + '</div>';
            }
        } catch { /* 忽略解析错误 */ }
    }

    // 历史思考过程展示
    let thinkingHtml = '';
    if (!isUser && msg.thinkingContent) {
        thinkingHtml = `<div class="thinking-block">
            <div class="thinking-header">
                <span class="thinking-toggle">▼</span>
                <span>思考过程</span>
            </div>
            <div class="thinking-body">${escapeHtml(msg.thinkingContent)}</div>
        </div>`;
    }

    div.innerHTML = `
        <div class="msg-avatar">${avatarContent}</div>
        <div class="msg-body">
            ${thinkingHtml}
            ${contentHtml}
            ${attachmentsHtml}
            <div class="msg-time" title="${exactTime(msg.createTime)}">${relativeTime(msg.createTime)}</div>
            ${isUser ? renderUserActions(msg) : renderAiActions(msg)}
        </div>`;

    messagesInner.appendChild(div);

    // 思考过程折叠/展开
    const thinkBlock = div.querySelector('.thinking-block');
    if (thinkBlock) {
        thinkBlock.querySelector('.thinking-header').addEventListener('click', () => {
            const toggle = thinkBlock.querySelector('.thinking-toggle');
            const body = thinkBlock.querySelector('.thinking-body');
            toggle.classList.toggle('collapsed');
            body.classList.toggle('collapsed');
        });
    }

    // 处理 Mermaid
    if (!isUser) {
        const content = div.querySelector('.md-content');
        if (content) processMermaid(content);
    }

    return div;
}

function renderUserActions(msg) {
    return `<div class="msg-actions">
        <button class="msg-action-btn" onclick="copyMessage(${msg.id})" title="复制">📋</button>
        <button class="msg-action-btn" onclick="editMessage(${msg.id}, true)" title="编辑">✏️</button>
    </div>`;
}

function renderAiActions(msg) {
    return `<div class="msg-actions">
        <button class="msg-action-btn" onclick="copyMessage(${msg.id})" title="复制">📋</button>
        <button class="msg-action-btn" onclick="editMessage(${msg.id}, false)" title="编辑">✏️</button>
        <button class="msg-action-btn" onclick="likeMessage(${msg.id}, this)" title="点赞">👍</button>
        <button class="msg-action-btn" onclick="dislikeMessage(${msg.id})" title="点踩">👎</button>
        <button class="msg-action-btn" onclick="shareConversation()" title="分享">🔗</button>
    </div>`;
}

// 仅为最后一条 AI 回复添加重新生成按钮
function addRegenerateToLastAi() {
    // 移除所有已有的重新生成按钮
    messagesInner.querySelectorAll('.msg-action-regen').forEach(b => b.remove());
    // 找到最后一条 AI 消息
    const aiMsgs = messagesInner.querySelectorAll('.msg.assistant');
    if (aiMsgs.length === 0) return;
    const lastAi = aiMsgs[aiMsgs.length - 1];
    const msgId = lastAi.dataset.messageId;
    if (!msgId || msgId === '0') return;
    const actions = lastAi.querySelector('.msg-actions');
    if (!actions) return;
    const btn = document.createElement('button');
    btn.className = 'msg-action-btn msg-action-regen';
    btn.title = '重新生成';
    btn.textContent = '🔄';
    btn.addEventListener('click', () => regenerateMessage(Number(msgId)));
    actions.appendChild(btn);
}

// ── SSE 流式消息处理 ──
async function sendMessage() {
    const content = promptInput.value.trim();
    if (!content || state.isGenerating) return;

    // 如果没有当前会话，先创建
    if (!state.currentConversationId) {
        await createConversation();
    }

    // 渲染用户消息
    const userMsg = {
        id: 0,
        role: 'user',
        content,
        createTime: new Date().toISOString()
    };
    renderMessage(userMsg);
    promptInput.value = '';
    autoResizeInput();
    updateSendButton();

    // 准备 AI 回复容器
    const aiDiv = document.createElement('div');
    aiDiv.className = 'msg assistant';
    aiDiv.innerHTML = `
        <div class="msg-avatar">🤖</div>
        <div class="msg-body">
            <div class="msg-content md-content"></div>
            <div class="typing-indicator"><span></span><span></span><span></span></div>
        </div>`;
    messagesInner.appendChild(aiDiv);
    scrollToBottom();

    // 开始流式请求
    state.isGenerating = true;
    state.abortController = new AbortController();
    setSendButtonStop(true);
    setInputDisabled(true);

    const attachmentIds = state.pendingAttachments.map(a => a.id);
    clearAttachments();

    let thinkingContent = '';
    let answerContent = '';
    let currentThinkingBlock = null;
    let thinkingStartTime = 0;
    let messageId = 0;
    let usage = null;
    let hasError = false;

    try {
        const resp = await fetch(`/api/conversations/${state.currentConversationId}/messages`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content, thinkingMode: state.thinkingMode, attachmentIds }),
            signal: state.abortController.signal,
        });

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
                try { data = JSON.parse(payload); } catch { continue; }

                switch (data.type) {
                    case 'message_start':
                        messageId = data.messageId;
                        state.currentMessageId = messageId;
                        aiDiv.dataset.messageId = messageId;
                        thinkingStartTime = Date.now();
                        break;

                    case 'thinking_delta':
                        if (!currentThinkingBlock) {
                            currentThinkingBlock = createThinkingBlock(aiDiv);
                        }
                        thinkingContent += data.content;
                        currentThinkingBlock.querySelector('.thinking-body').textContent = thinkingContent;
                        if (state.userAutoScroll) scrollToBottom();
                        break;

                    case 'thinking_done': {
                        if (currentThinkingBlock) {
                            const elapsed = data.thinkingTime || (Date.now() - thinkingStartTime);
                            const header = currentThinkingBlock.querySelector('.thinking-header');
                            const spinner = header.querySelector('.spinner');
                            if (spinner) spinner.remove();
                            header.querySelector('span:last-child').textContent = `思考过程（${(elapsed / 1000).toFixed(1)}s）`;
                        }
                        currentThinkingBlock = null;
                        thinkingContent = '';
                        break;
                    }

                    case 'tool_call_start':
                        createToolCallBlock(aiDiv, data);
                        if (state.userAutoScroll) scrollToBottom();
                        break;

                    case 'tool_call_done':
                        updateToolCallBlock(data.toolCallId, data.result, true);
                        break;

                    case 'tool_call_error':
                        updateToolCallBlock(data.toolCallId, data.error, false);
                        break;

                    case 'content_delta': {
                        answerContent += data.content;
                        const contentEl = aiDiv.querySelector('.msg-content');
                        contentEl.innerHTML = renderMarkdown(answerContent);
                        // 移除加载指示器
                        const typing = aiDiv.querySelector('.typing-indicator');
                        if (typing) typing.remove();
                        if (state.userAutoScroll) scrollToBottom();
                        break;
                    }

                    case 'message_done':
                        usage = data.usage;
                        // 标题更新
                        if (data.title) {
                            updateConversationTitle(data.title);
                        }
                        break;

                    case 'error':
                        hasError = true;
                        showErrorInMessage(aiDiv, data.code, data.message);
                        break;
                }
            }
        }
    } catch (e) {
        if (e.name !== 'AbortError') {
            // 网络异常自动重试：最多 3 次，间隔 1s/2s/4s
            let retried = false;
            if (!hasError && answerContent.length === 0) {
                for (let retry = 0; retry < 3; retry++) {
                    const delay = Math.pow(2, retry) * 1000;
                    showErrorInMessage(aiDiv, 'NETWORK_ERROR', `网络异常，${(delay / 1000)}s 后重试（${retry + 1}/3）...`);
                    await new Promise(r => setTimeout(r, delay));
                    // 移除错误提示
                    const errEl = aiDiv.querySelector('.msg-error');
                    if (errEl) errEl.remove();
                    try {
                        state.abortController = new AbortController();
                        const retryResp = await fetch(`/api/conversations/${state.currentConversationId}/messages`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ content, thinkingMode: state.thinkingMode, attachmentIds }),
                            signal: state.abortController.signal,
                        });
                        // 重试成功，但此处简化处理直接刷新
                        retried = true;
                        break;
                    } catch (retryErr) {
                        if (retryErr.name === 'AbortError') break;
                    }
                }
            }
            if (!retried) {
                showErrorInMessage(aiDiv, 'NETWORK_ERROR', '网络异常，请重试');
            }
            hasError = true;
        }
    } finally {
        state.isGenerating = false;
        state.currentMessageId = null;
        state.abortController = null;
        setSendButtonStop(false);
        setInputDisabled(false);

        // 移除加载指示器
        const typing = aiDiv.querySelector('.typing-indicator');
        if (typing) typing.remove();

        // 添加操作按钮和用量
        if (messageId && !hasError) {
            const body = aiDiv.querySelector('.msg-body');
            // 用量
            if (usage) {
                const usageEl = document.createElement('div');
                usageEl.className = 'msg-usage';
                usageEl.textContent = `${usage.promptTokens} + ${usage.completionTokens} = ${usage.totalTokens} tokens`;
                body.appendChild(usageEl);
            }
            // 操作按钮
            const actions = document.createElement('div');
            actions.className = 'msg-actions';
            actions.innerHTML = `
                <button class="msg-action-btn" onclick="copyMessage(${messageId})" title="复制">📋</button>
                <button class="msg-action-btn" onclick="editMessage(${messageId}, false)" title="编辑">✏️</button>
                <button class="msg-action-btn" onclick="likeMessage(${messageId}, this)" title="点赞">👍</button>
                <button class="msg-action-btn" onclick="dislikeMessage(${messageId})" title="点踩">👎</button>
                <button class="msg-action-btn" onclick="shareConversation()" title="分享">🔗</button>`;
            body.appendChild(actions);

            // 处理 Mermaid
            const contentEl = aiDiv.querySelector('.md-content');
            if (contentEl) processMermaid(contentEl);

            // 仅为最后一条 AI 回复添加重新生成按钮
            addRegenerateToLastAi();
        }

        // 时间
        const timeEl = document.createElement('div');
        timeEl.className = 'msg-time';
        timeEl.textContent = '刚刚';
        timeEl.title = exactTime(new Date().toISOString());
        aiDiv.querySelector('.msg-body').insertBefore(timeEl, aiDiv.querySelector('.msg-usage') || aiDiv.querySelector('.msg-actions'));

        scrollToBottom();
        promptInput.focus();
        await loadConversations();
    }
}

// ── 思考过程块 ──
function createThinkingBlock(msgDiv) {
    const body = msgDiv.querySelector('.msg-body');
    const typing = msgDiv.querySelector('.typing-indicator');

    const block = document.createElement('div');
    block.className = 'thinking-block';
    block.innerHTML = `
        <div class="thinking-header">
            <span class="thinking-toggle">▼</span>
            <div class="spinner"></div>
            <span>思考中...</span>
        </div>
        <div class="thinking-body"></div>`;

    // 插入到加载指示器前面
    if (typing) {
        body.insertBefore(block, typing);
    } else {
        const content = body.querySelector('.msg-content');
        body.insertBefore(block, content);
    }

    // 折叠/展开
    block.querySelector('.thinking-header').addEventListener('click', () => {
        const toggle = block.querySelector('.thinking-toggle');
        const thinkBody = block.querySelector('.thinking-body');
        toggle.classList.toggle('collapsed');
        thinkBody.classList.toggle('collapsed');
    });

    return block;
}

// ── 工具调用块 ──
function createToolCallBlock(msgDiv, data) {
    const body = msgDiv.querySelector('.msg-body');
    const typing = msgDiv.querySelector('.typing-indicator');

    const block = document.createElement('div');
    block.className = 'tool-call-block';
    block.dataset.toolCallId = data.toolCallId;

    let argsDisplay = '';
    try {
        argsDisplay = JSON.stringify(JSON.parse(data.arguments), null, 2);
    } catch {
        argsDisplay = data.arguments || '';
    }

    block.innerHTML = `
        <div class="tool-call-header">
            <span class="tool-call-status pending">⟳</span>
            <span class="tool-call-name">${escapeHtml(data.name)}</span>
            <span class="thinking-toggle">▼</span>
        </div>
        <div class="tool-call-body">
            <div class="tool-call-label">参数</div>
            <pre>${escapeHtml(argsDisplay)}</pre>
            <div class="tool-call-result"></div>
        </div>`;

    if (typing) {
        body.insertBefore(block, typing);
    } else {
        const content = body.querySelector('.msg-content');
        body.insertBefore(block, content);
    }

    // 折叠
    block.querySelector('.tool-call-header').addEventListener('click', () => {
        const toggle = block.querySelector('.thinking-toggle');
        const tcBody = block.querySelector('.tool-call-body');
        toggle.classList.toggle('collapsed');
        tcBody.classList.toggle('collapsed');
    });
}

function updateToolCallBlock(toolCallId, result, success) {
    const block = document.querySelector(`[data-tool-call-id="${toolCallId}"]`);
    if (!block) return;

    const statusEl = block.querySelector('.tool-call-status');
    statusEl.classList.remove('pending');
    statusEl.textContent = success ? '✓' : '✗';
    statusEl.style.color = success ? 'var(--success)' : 'var(--danger)';

    const resultDiv = block.querySelector('.tool-call-result');
    if (result) {
        let resultDisplay = result;
        try { resultDisplay = JSON.stringify(JSON.parse(result), null, 2); } catch { /* 忽略 */ }
        resultDiv.innerHTML = `<div class="tool-call-label">${success ? '结果' : '错误'}</div><pre>${escapeHtml(resultDisplay)}</pre>`;
    }
}

function showErrorInMessage(msgDiv, code, message) {
    const body = msgDiv.querySelector('.msg-body');
    const typing = msgDiv.querySelector('.typing-indicator');
    if (typing) typing.remove();

    const errEl = document.createElement('div');
    errEl.className = 'msg-error';
    errEl.textContent = `${message} (${code})`;
    body.appendChild(errEl);
}

// ── 消息操作 ──
window.copyMessage = async function (msgId) {
    const msgDiv = document.querySelector(`[data-message-id="${msgId}"]`);
    if (!msgDiv) return;
    const content = msgDiv.querySelector('.msg-content');
    const text = content?.textContent || '';
    try {
        await navigator.clipboard.writeText(text);
        const btn = msgDiv.querySelector('.msg-action-btn[title="复制"]');
        if (btn) {
            btn.textContent = '✓';
            btn.classList.add('copied');
            setTimeout(() => { btn.textContent = '📋'; btn.classList.remove('copied'); }, 2000);
        }
    } catch {
        showToast('复制失败', 'error');
    }
};

window.editMessage = function (msgId, isUser) {
    const msgDiv = document.querySelector(`[data-message-id="${msgId}"]`);
    if (!msgDiv) return;
    const contentDiv = msgDiv.querySelector('.msg-content');
    const originalText = contentDiv.textContent;

    const textarea = document.createElement('textarea');
    textarea.className = 'msg-edit-area';
    textarea.value = originalText;
    contentDiv.replaceWith(textarea);
    textarea.focus();

    const actions = document.createElement('div');
    actions.className = 'msg-edit-actions';
    actions.innerHTML = `<button class="btn-primary">保存</button><button class="btn-outline">取消</button>`;
    textarea.after(actions);

    const msgActions = msgDiv.querySelector('.msg-actions');
    if (msgActions) msgActions.classList.add('hidden');

    actions.querySelector('.btn-outline').addEventListener('click', () => {
        const newContent = document.createElement('div');
        newContent.className = 'msg-content' + (isUser ? '' : ' md-content');
        newContent.innerHTML = isUser ? escapeHtml(originalText) : renderMarkdown(originalText);
        textarea.replaceWith(newContent);
        actions.remove();
        if (msgActions) msgActions.classList.remove('hidden');
    });

    actions.querySelector('.btn-primary').addEventListener('click', async () => {
        const newText = textarea.value.trim();
        if (!newText) return;

        try {
            if (isUser) {
                // 用户消息编辑：删除之后所有消息，重新发送
                const allMsgs = [...messagesInner.querySelectorAll('.msg')];
                const idx = allMsgs.indexOf(msgDiv);
                // 移除该消息之后的所有消息
                for (let i = allMsgs.length - 1; i > idx; i--) {
                    allMsgs[i].remove();
                }
                // 更新当前消息
                const newContent = document.createElement('div');
                newContent.className = 'msg-content';
                newContent.textContent = newText;
                textarea.replaceWith(newContent);
                actions.remove();
                if (msgActions) msgActions.classList.remove('hidden');

                // 重新发送
                promptInput.value = newText;
                // 移除当前用户消息（sendMessage 会重新渲染）
                msgDiv.remove();
                await sendMessage();
            } else {
                // AI 消息编辑：仅修改显示
                await api(`/api/messages/${msgId}`, { method: 'PUT', body: { content: newText } });
                const newContent = document.createElement('div');
                newContent.className = 'msg-content md-content';
                newContent.innerHTML = renderMarkdown(newText);
                textarea.replaceWith(newContent);
                actions.remove();
                if (msgActions) msgActions.classList.remove('hidden');
                processMermaid(newContent);
            }
        } catch (e) {
            showToast('编辑失败: ' + e.message, 'error');
        }
    });
};

window.likeMessage = async function (msgId, btn) {
    try {
        if (btn.classList.contains('active')) {
            await api(`/api/messages/${msgId}/feedback`, { method: 'DELETE' });
            btn.classList.remove('active');
        } else {
            await api(`/api/messages/${msgId}/feedback`, {
                method: 'POST',
                body: { type: 1, reason: null, allowTraining: state.settings?.allowTraining }
            });
            btn.classList.add('active');
            // 取消同一消息的踩
            const dislikeBtn = btn.parentElement.querySelector('[title="点踩"]');
            if (dislikeBtn) dislikeBtn.classList.remove('active');
        }
    } catch (e) {
        showToast('操作失败: ' + e.message, 'error');
    }
};

window.dislikeMessage = function (msgId) {
    state._dislikeMsgId = msgId;
    $('feedbackModal').classList.remove('hidden');
};

window.regenerateMessage = async function (msgId) {
    if (state.isGenerating) return;
    try {
        // 移除最后一条 AI 消息
        const msgDiv = document.querySelector(`[data-message-id="${msgId}"]`);
        if (msgDiv) msgDiv.remove();

        // 创建新的 AI 容器并流式请求（复用 regenerate 接口）
        const aiDiv = document.createElement('div');
        aiDiv.className = 'msg assistant';
        aiDiv.innerHTML = `
            <div class="msg-avatar">🤖</div>
            <div class="msg-body">
                <div class="msg-content md-content"></div>
                <div class="typing-indicator"><span></span><span></span><span></span></div>
            </div>`;
        messagesInner.appendChild(aiDiv);
        scrollToBottom();

        state.isGenerating = true;
        setSendButtonStop(true);
        setInputDisabled(true);

        const result = await api(`/api/messages/${msgId}/regenerate`, { method: 'POST' });
        if (result) {
            const typing = aiDiv.querySelector('.typing-indicator');
            if (typing) typing.remove();
            const contentEl = aiDiv.querySelector('.msg-content');
            contentEl.innerHTML = renderMarkdown(result.content);
            aiDiv.dataset.messageId = result.id;

            const body = aiDiv.querySelector('.msg-body');
            const timeEl = document.createElement('div');
            timeEl.className = 'msg-time';
            timeEl.textContent = '刚刚';
            body.appendChild(timeEl);

            const actions = document.createElement('div');
            actions.className = 'msg-actions';
            actions.innerHTML = `
                <button class="msg-action-btn" onclick="copyMessage(${result.id})" title="复制">📋</button>
                <button class="msg-action-btn" onclick="likeMessage(${result.id}, this)" title="点赞">👍</button>
                <button class="msg-action-btn" onclick="dislikeMessage(${result.id})" title="点踩">👎</button>
                <button class="msg-action-btn" onclick="shareConversation()" title="分享">🔗</button>
                <button class="msg-action-btn" onclick="regenerateMessage(${result.id})" title="重新生成">🔄</button>`;
            body.appendChild(actions);
            processMermaid(contentEl);
        }
    } catch (e) {
        showToast('重新生成失败: ' + e.message, 'error');
    } finally {
        state.isGenerating = false;
        setSendButtonStop(false);
        setInputDisabled(false);
        scrollToBottom();
    }
};

// ── 停止生成 ──
function stopGeneration() {
    if (state.abortController) {
        state.abortController.abort();
    }
    if (state.currentMessageId) {
        api(`/api/messages/${state.currentMessageId}/stop`, { method: 'POST' }).catch(() => {});
    }
}

// ── 分享 ──
window.shareConversation = async function () {
    if (!state.currentConversationId) return;
    try {
        const result = await api(`/api/conversations/${state.currentConversationId}/share`, {
            method: 'POST',
            body: {}
        });
        $('shareLinkInput').value = result.url;
        $('shareModal').classList.remove('hidden');
    } catch (e) {
        showToast('创建分享链接失败: ' + e.message, 'error');
    }
};

// ── 附件 ──
function handleFileSelect(files) {
    const maxSize = 20 * 1024 * 1024;
    const maxCount = 5;
    const allowed = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.pdf', '.docx', '.txt', '.md', '.csv'];

    for (const file of files) {
        if (state.pendingAttachments.length >= maxCount) {
            showToast(`最多上传 ${maxCount} 个文件`, 'error');
            break;
        }
        const ext = '.' + file.name.split('.').pop().toLowerCase();
        if (!allowed.includes(ext)) {
            showToast(`不支持的文件类型: ${ext}`, 'error');
            continue;
        }
        if (file.size > maxSize) {
            showToast(`文件太大: ${file.name}（最大 20MB）`, 'error');
            continue;
        }
        uploadFile(file);
    }
}

async function uploadFile(file) {
    const formData = new FormData();
    formData.append('file', file);
    try {
        const result = await api('/api/attachments/upload', { method: 'POST', body: formData });
        state.pendingAttachments.push({
            id: result.attachmentId,
            name: result.fileName,
            url: result.url,
            size: result.size,
            isImage: /\.(jpg|jpeg|png|gif|webp)$/i.test(result.fileName)
        });
        renderAttachmentPreview();
    } catch (e) {
        showToast('上传失败: ' + e.message, 'error');
    }
}

function renderAttachmentPreview() {
    if (state.pendingAttachments.length === 0) {
        attachmentPreview.classList.add('hidden');
        attachmentPreview.innerHTML = '';
        return;
    }
    attachmentPreview.classList.remove('hidden');
    attachmentPreview.innerHTML = '';
    for (let i = 0; i < state.pendingAttachments.length; i++) {
        const a = state.pendingAttachments[i];
        const item = document.createElement('div');
        item.className = 'attach-preview-item';
        if (a.isImage && a.url) {
            item.innerHTML = `<img class="attach-preview-thumb" src="${a.url}" alt="" /><span>${escapeHtml(a.name)}</span>`;
        } else {
            item.innerHTML = `<span>📎</span><span>${escapeHtml(a.name)}</span>`;
        }
        const removeBtn = document.createElement('button');
        removeBtn.className = 'attach-preview-remove';
        removeBtn.textContent = '✕';
        removeBtn.addEventListener('click', () => {
            state.pendingAttachments.splice(i, 1);
            renderAttachmentPreview();
        });
        item.appendChild(removeBtn);
        attachmentPreview.appendChild(item);
    }
}

function clearAttachments() {
    state.pendingAttachments = [];
    renderAttachmentPreview();
}

// ── 输入区 ──
function autoResizeInput() {
    promptInput.style.height = 'auto';
    promptInput.style.height = Math.min(promptInput.scrollHeight, 200) + 'px';
}

function updateSendButton() {
    const hasContent = promptInput.value.trim().length > 0;
    sendBtn.disabled = !hasContent && !state.isGenerating;
}

function setSendButtonStop(isStop) {
    if (isStop) {
        sendBtn.disabled = false;
        sendBtn.classList.add('stop');
        sendIcon.innerHTML = '<rect x="6" y="6" width="12" height="12" rx="1" fill="currentColor"/>';
        sendBtn.title = '停止生成';
    } else {
        sendBtn.classList.remove('stop');
        sendIcon.innerHTML = '<path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" fill="currentColor"/>';
        sendBtn.title = '发送';
        updateSendButton();
    }
}

function setInputDisabled(disabled) {
    promptInput.disabled = disabled;
    uploadBtn.disabled = disabled;
}

function updateConversationTitle(title) {
    const item = conversationList.querySelector(`[data-id="${state.currentConversationId}"]`);
    if (item) {
        const titleEl = item.querySelector('.conv-item-title');
        if (titleEl) titleEl.textContent = title;
    }
}

// ── 滚动控制 ──
function scrollToBottom(instant = false) {
    if (!state.userAutoScroll && !instant) return;
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

messagesEl.addEventListener('scroll', () => {
    const { scrollTop, scrollHeight, clientHeight } = messagesEl;
    const isAtBottom = scrollHeight - scrollTop - clientHeight < 60;
    state.userAutoScroll = isAtBottom;
    scrollBottomBtn.classList.toggle('hidden', isAtBottom);
});

scrollBottomBtn.addEventListener('click', () => {
    state.userAutoScroll = true;
    scrollToBottom(true);
    scrollBottomBtn.classList.add('hidden');
});

// ── 全屏图片预览 ──
window.openLightbox = function (src) {
    // 收集页面中所有图片
    const imgs = messagesInner.querySelectorAll('.md-content img');
    state.lightboxImages = [...imgs].map(img => img.src);
    state.lightboxIndex = state.lightboxImages.indexOf(src);
    if (state.lightboxIndex < 0) {
        state.lightboxImages = [src];
        state.lightboxIndex = 0;
    }
    renderLightbox();
    $('lightbox').classList.remove('hidden');
};

function renderLightbox() {
    $('lightboxImg').src = state.lightboxImages[state.lightboxIndex];
    // 缩略图
    const thumbs = $('lightboxThumbnails');
    thumbs.innerHTML = '';
    if (state.lightboxImages.length > 1) {
        state.lightboxImages.forEach((src, i) => {
            const img = document.createElement('img');
            img.src = src;
            img.className = i === state.lightboxIndex ? 'active' : '';
            img.addEventListener('click', () => { state.lightboxIndex = i; renderLightbox(); });
            thumbs.appendChild(img);
        });
    }
    $('lightboxPrev').classList.toggle('hidden', state.lightboxImages.length <= 1);
    $('lightboxNext').classList.toggle('hidden', state.lightboxImages.length <= 1);
}

$('lightboxClose').addEventListener('click', () => $('lightbox').classList.add('hidden'));
$('lightbox').querySelector('.lightbox-backdrop').addEventListener('click', () => $('lightbox').classList.add('hidden'));
$('lightboxPrev').addEventListener('click', () => {
    state.lightboxIndex = (state.lightboxIndex - 1 + state.lightboxImages.length) % state.lightboxImages.length;
    renderLightbox();
});
$('lightboxNext').addEventListener('click', () => {
    state.lightboxIndex = (state.lightboxIndex + 1) % state.lightboxImages.length;
    renderLightbox();
});

// 键盘导航
document.addEventListener('keydown', e => {
    if ($('lightbox').classList.contains('hidden')) return;
    if (e.key === 'Escape') $('lightbox').classList.add('hidden');
    if (e.key === 'ArrowLeft') { state.lightboxIndex = (state.lightboxIndex - 1 + state.lightboxImages.length) % state.lightboxImages.length; renderLightbox(); }
    if (e.key === 'ArrowRight') { state.lightboxIndex = (state.lightboxIndex + 1) % state.lightboxImages.length; renderLightbox(); }
});

// ── 设置页 ──
function populateSettingsModels() {
    const sel = $('settingDefaultModel');
    sel.innerHTML = '';
    for (const m of state.models) {
        const opt = document.createElement('option');
        opt.value = m.code;
        opt.textContent = m.name;
        sel.appendChild(opt);
    }
}

async function loadSettings() {
    try {
        state.settings = await api('/api/user/settings');
        applySettings(state.settings);
    } catch { /* 使用默认 */ }
}

function applySettings(s) {
    if (!s) return;
    $('settingLanguage').value = s.language || 'zh-CN';
    $('settingTheme').value = s.theme || 'light';
    $('settingFontSize').value = s.fontSize || 16;
    $('fontSizeValue').textContent = (s.fontSize || 16) + 'px';
    $('settingSendKey').value = s.sendShortcut || 'Enter';
    $('settingDefaultModel').value = s.defaultModel || '';
    $('settingDefaultThinking').value = s.defaultThinkingMode ?? 0;
    $('settingContextRounds').value = s.contextRounds || 10;
    $('settingSystemPrompt').value = s.systemPrompt || '';
    $('settingAllowTraining').checked = s.allowTraining || false;

    state.sendShortcut = s.sendShortcut || 'Enter';
    applyTheme(s.theme || 'light');
    document.documentElement.style.setProperty('--font-size', (s.fontSize || 16) + 'px');

    // 默认思考模式
    setThinkingMode(s.defaultThinkingMode ?? 0);

    // 默认模型
    if (s.defaultModel && !state.currentModel) {
        const m = state.models.find(x => x.code === s.defaultModel);
        if (m) selectModel(m);
    }
}

function applyTheme(theme) {
    let effective = theme;
    if (theme === 'system') {
        effective = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', effective);
    // 切换代码高亮主题
    const hlLight = $('hlThemeLight');
    const hlDark = $('hlThemeDark');
    if (hlLight && hlDark) {
        hlLight.disabled = effective === 'dark';
        hlDark.disabled = effective !== 'dark';
    }
}

async function saveSettings() {
    const s = {
        language: $('settingLanguage').value,
        theme: $('settingTheme').value,
        fontSize: parseInt($('settingFontSize').value),
        sendShortcut: $('settingSendKey').value,
        defaultModel: $('settingDefaultModel').value,
        defaultThinkingMode: parseInt($('settingDefaultThinking').value),
        contextRounds: parseInt($('settingContextRounds').value),
        systemPrompt: $('settingSystemPrompt').value,
        allowTraining: $('settingAllowTraining').checked,
    };
    try {
        state.settings = await api('/api/user/settings', { method: 'PUT', body: s });
        applySettings(state.settings);
        showToast('设置已保存', 'success');
    } catch (e) {
        showToast('保存失败: ' + e.message, 'error');
    }
}

// ── 使用量统计 ──
async function loadUsageStats() {
    try {
        const data = await api('/api/usage/summary');
        $('usageConversations').textContent = data.conversations ?? 0;
        $('usageMessages').textContent = data.messages ?? 0;
        $('usageTotalTokens').textContent = formatTokenCount(data.totalTokens ?? 0);
        $('usageLastActive').textContent = data.lastActiveTime ? relativeTime(data.lastActiveTime) : '暂无';
    } catch { /* 忽略 */ }
}

function formatTokenCount(n) {
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
    return String(n);
}

// ── 推荐问题 ──
async function loadSuggestedQuestions() {
    // 从 ChatSetting 获取，或用默认值
    const questions = ['帮我写一封邮件', '解释量子计算', '用Python写一个排序算法', '帮我翻译一段英文'];
    suggestedQuestions.innerHTML = '';
    for (const q of questions) {
        const btn = document.createElement('button');
        btn.className = 'suggested-q';
        btn.textContent = q;
        btn.addEventListener('click', () => {
            promptInput.value = q;
            autoResizeInput();
            updateSendButton();
            sendMessage();
        });
        suggestedQuestions.appendChild(btn);
    }
}

// ── URL 路由 ──
function handleRoute() {
    const path = window.location.pathname;
    const chatMatch = path.match(/^\/chat\/(\d+)$/);
    const shareMatch = path.match(/^\/share\/(.+)$/);
    if (chatMatch) {
        openConversation(parseInt(chatMatch[1]));
    } else if (shareMatch) {
        loadSharePage(shareMatch[1]);
    }
}

// ── 共享对话只读页面 ──
async function loadSharePage(token) {
    // 隐藏侧边栏和输入区
    sidebar.classList.add('collapsed');
    sidebarOpenBtn.classList.add('hidden');
    composer.classList.add('hidden');

    showChatView();
    messagesInner.innerHTML = '';

    // 顶部共享标识
    const banner = document.createElement('div');
    banner.className = 'share-banner';
    banner.innerHTML = '🔗 共享对话 — 此对话由用户分享，仅供查看';
    messagesInner.appendChild(banner);

    try {
        const data = await api(`/api/share/${token}`);
        if (data.title) {
            document.title = `${data.title} — 共享对话`;
            currentModelName.textContent = data.modelCode || '共享对话';
        }
        const messages = data.messages || [];
        for (const msg of messages) {
            const div = document.createElement('div');
            div.className = `msg ${msg.role}`;
            const isUser = msg.role === 'user';
            const avatarContent = isUser ? 'U' : '🤖';
            let contentHtml = isUser
                ? `<div class="msg-content">${escapeHtml(msg.content)}</div>`
                : `<div class="msg-content md-content">${renderMarkdown(msg.content)}</div>`;

            // 思考过程
            let thinkingHtml = '';
            if (!isUser && msg.thinkingContent) {
                thinkingHtml = `<div class="thinking-block">
                    <div class="thinking-header">
                        <span class="thinking-toggle">▼</span>
                        <span>思考过程</span>
                    </div>
                    <div class="thinking-body">${escapeHtml(msg.thinkingContent)}</div>
                </div>`;
            }

            div.innerHTML = `
                <div class="msg-avatar">${avatarContent}</div>
                <div class="msg-body">
                    ${thinkingHtml}
                    ${contentHtml}
                    <div class="msg-time" title="${exactTime(msg.createTime)}">${relativeTime(msg.createTime)}</div>
                </div>`;
            messagesInner.appendChild(div);

            // 思考过程折叠
            const thinkBlock = div.querySelector('.thinking-block');
            if (thinkBlock) {
                thinkBlock.querySelector('.thinking-header').addEventListener('click', () => {
                    thinkBlock.querySelector('.thinking-toggle').classList.toggle('collapsed');
                    thinkBlock.querySelector('.thinking-body').classList.toggle('collapsed');
                });
            }

            if (!isUser) {
                const md = div.querySelector('.md-content');
                if (md) processMermaid(md);
            }
        }

        // 底部引导
        const footer = document.createElement('div');
        footer.className = 'share-footer';
        footer.innerHTML = '使用 <strong>NewLife.ChatAI</strong> 开始你的对话 → <a href="/chat">立即体验</a>';
        messagesInner.appendChild(footer);

        scrollToBottom(true);
    } catch (e) {
        messagesInner.innerHTML = `<div class="share-banner" style="color: var(--text-error);">🚫 共享链接无效或已过期</div>`;
    }
}

window.addEventListener('popstate', handleRoute);

// ── 事件绑定 ──

// 侧边栏
sidebarToggle.addEventListener('click', toggleSidebar);
sidebarOpenBtn.addEventListener('click', toggleSidebar);
sidebarOverlay.addEventListener('click', closeMobileSidebar);

// 窗口尺寸变化时自动处理侧边栏
window.addEventListener('resize', () => {
    if (window.innerWidth > 768) {
        sidebar.classList.remove('mobile-open');
        sidebarOverlay.classList.add('hidden');
    }
});

// 移动端长按消息弹出操作菜单
let longPressTimer = null;
messagesInner.addEventListener('touchstart', e => {
    const msgEl = e.target.closest('.msg');
    if (!msgEl) return;
    longPressTimer = setTimeout(() => {
        e.preventDefault();
        showMobileContextMenu(msgEl, e.touches[0]);
    }, 500);
}, { passive: false });
messagesInner.addEventListener('touchend', () => clearTimeout(longPressTimer));
messagesInner.addEventListener('touchmove', () => clearTimeout(longPressTimer));

function showMobileContextMenu(msgEl, touch) {
    // 移除已有菜单
    document.querySelectorAll('.msg-context-menu').forEach(m => m.remove());
    const msgId = msgEl.dataset.messageId;
    const isUser = msgEl.classList.contains('user');
    const menu = document.createElement('div');
    menu.className = 'msg-context-menu';
    let items = `<div class="menu-item" data-action="copy">📋 复制</div>`;
    items += `<div class="menu-item" data-action="edit">✏️ 编辑</div>`;
    if (!isUser) {
        items += `<div class="menu-item" data-action="like">👍 点赞</div>`;
        items += `<div class="menu-item" data-action="dislike">👎 点踩</div>`;
        items += `<div class="menu-item" data-action="share">🔗 分享</div>`;
    }
    menu.innerHTML = items;
    // 定位
    menu.style.left = Math.min(touch.clientX, window.innerWidth - 160) + 'px';
    menu.style.top = Math.min(touch.clientY, window.innerHeight - 200) + 'px';
    document.body.appendChild(menu);

    menu.addEventListener('click', ev => {
        const action = ev.target.closest('[data-action]')?.dataset.action;
        if (!action) return;
        menu.remove();
        switch (action) {
            case 'copy': copyMessage(Number(msgId)); break;
            case 'edit': editMessage(Number(msgId), isUser); break;
            case 'like': {
                const btn = msgEl.querySelector('[title="\u70b9\u8d5e"]');
                likeMessage(Number(msgId), btn); break;
            }
            case 'dislike': dislikeMessage(Number(msgId)); break;
            case 'share': shareConversation(); break;
        }
    });
    // 点击其他位置关闭
    setTimeout(() => {
        document.addEventListener('click', function handler() {
            menu.remove();
            document.removeEventListener('click', handler);
        });
    }, 10);
}

newConversationBtn.addEventListener('click', () => {
    state.currentConversationId = null;
    showWelcomePage();
    promptInput.focus();
    // 取消高亮
    conversationList.querySelectorAll('.conv-item').forEach(el => el.classList.remove('active'));
});

// 个人菜单
profileBtn.addEventListener('click', e => {
    e.stopPropagation();
    profileMenu.classList.toggle('hidden');
});
document.addEventListener('click', () => profileMenu.classList.add('hidden'));
menuSettings.addEventListener('click', () => {
    profileMenu.classList.add('hidden');
    $('settingsModal').classList.remove('hidden');
    loadUsageStats();
});

// 模型选择器
modelSelectorBtn.addEventListener('click', e => {
    e.stopPropagation();
    modelDropdown.classList.toggle('hidden');
});
document.addEventListener('click', e => {
    if (!e.target.closest('.model-selector')) modelDropdown.classList.add('hidden');
});

// 思考模式选择器
thinkingModeBtn.addEventListener('click', e => {
    e.stopPropagation();
    thinkingModeDropdown.classList.toggle('hidden');
});
document.addEventListener('click', e => {
    if (!e.target.closest('.thinking-mode-selector')) thinkingModeDropdown.classList.add('hidden');
});
thinkingModeDropdown.querySelectorAll('.dropdown-item').forEach(item => {
    item.addEventListener('click', () => {
        if (item.classList.contains('disabled')) return;
        setThinkingMode(parseInt(item.dataset.mode));
        thinkingModeDropdown.classList.add('hidden');
    });
});

// 发送
sendBtn.addEventListener('click', () => {
    if (state.isGenerating) {
        stopGeneration();
    } else {
        sendMessage();
    }
});

// 输入框
promptInput.addEventListener('input', () => {
    autoResizeInput();
    updateSendButton();
    // 字数提示
    const len = promptInput.value.length;
    if (len > 5000) {
        charCount.classList.remove('hidden');
        charCount.textContent = `${len}/6000`;
        charCount.classList.toggle('warn', len > 5800);
    } else {
        charCount.classList.add('hidden');
    }
});

promptInput.addEventListener('keydown', e => {
    if (state.sendShortcut === 'Enter') {
        if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            if (state.isGenerating) return;
            sendMessage();
        }
    } else {
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            if (state.isGenerating) return;
            sendMessage();
        }
    }
});

// 附件上传
uploadBtn.addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', () => {
    if (fileInput.files.length > 0) handleFileSelect(fileInput.files);
    fileInput.value = '';
});

// 拖拽上传
const mainContent = $('mainContent');
mainContent.addEventListener('dragover', e => {
    e.preventDefault();
    dropOverlay.classList.remove('hidden');
});
dropOverlay.addEventListener('dragleave', e => {
    e.preventDefault();
    dropOverlay.classList.add('hidden');
});
dropOverlay.addEventListener('drop', e => {
    e.preventDefault();
    dropOverlay.classList.add('hidden');
    if (e.dataTransfer.files.length > 0) handleFileSelect(e.dataTransfer.files);
});

// 粘贴图片
promptInput.addEventListener('paste', e => {
    const items = e.clipboardData?.items;
    if (!items) return;
    for (const item of items) {
        if (item.type.startsWith('image/')) {
            e.preventDefault();
            const file = item.getAsFile();
            if (file) handleFileSelect([file]);
        }
    }
});

// 设置弹窗
$('settingsBack').addEventListener('click', () => {
    saveSettings();
    $('settingsModal').classList.add('hidden');
});
$('settingTheme').addEventListener('change', () => applyTheme($('settingTheme').value));
$('settingFontSize').addEventListener('input', () => {
    $('fontSizeValue').textContent = $('settingFontSize').value + 'px';
    document.documentElement.style.setProperty('--font-size', $('settingFontSize').value + 'px');
});

$('settingExport').addEventListener('click', async () => {
    try {
        const resp = await api('/api/user/export', { method: 'POST', raw: true });
        const blob = await resp.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'chat-export.json';
        a.click();
        URL.revokeObjectURL(url);
        showToast('导出成功', 'success');
    } catch (e) {
        showToast('导出失败: ' + e.message, 'error');
    }
});

$('settingClear').addEventListener('click', async () => {
    const ok = await showConfirm('清除所有对话', '确定清除所有对话数据？此操作不可撤销。');
    if (!ok) return;
    try {
        await api('/api/user/conversations', { method: 'DELETE' });
        state.currentConversationId = null;
        showWelcomePage();
        await loadConversations();
        showToast('所有对话已清除', 'success');
    } catch (e) {
        showToast('清除失败: ' + e.message, 'error');
    }
});

// 分享弹窗
$('shareClose').addEventListener('click', () => $('shareModal').classList.add('hidden'));
$('shareCopyBtn').addEventListener('click', async () => {
    try {
        await navigator.clipboard.writeText($('shareLinkInput').value);
        $('shareCopyBtn').textContent = '已复制';
        setTimeout(() => { $('shareCopyBtn').textContent = '复制'; }, 2000);
    } catch {
        showToast('复制失败', 'error');
    }
});

// 反馈弹窗
$('feedbackClose').addEventListener('click', () => $('feedbackModal').classList.add('hidden'));
$('feedbackSubmitBtn').addEventListener('click', async () => {
    const tags = [...$('feedbackModal').querySelectorAll('.tag-checkbox input:checked')].map(c => c.value);
    const text = $('feedbackText').value.trim();
    const reason = [...tags, text].filter(Boolean).join('; ');
    try {
        await api(`/api/messages/${state._dislikeMsgId}/feedback`, {
            method: 'POST',
            body: { type: 2, reason: reason || null, allowTraining: state.settings?.allowTraining }
        });
        $('feedbackModal').classList.add('hidden');
        $('feedbackText').value = '';
        $('feedbackModal').querySelectorAll('.tag-checkbox input').forEach(c => c.checked = false);

        // 更新按钮状态
        const msgDiv = document.querySelector(`[data-message-id="${state._dislikeMsgId}"]`);
        if (msgDiv) {
            const dislikeBtn = msgDiv.querySelector('[title="点踩"]');
            if (dislikeBtn) dislikeBtn.classList.add('active');
            const likeBtn = msgDiv.querySelector('[title="点赞"]');
            if (likeBtn) likeBtn.classList.remove('active');
        }
        showToast('感谢你的反馈', 'success');
    } catch (e) {
        showToast('提交失败: ' + e.message, 'error');
    }
});

// 系统主题变化监听
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (state.settings?.theme === 'system' || $('settingTheme')?.value === 'system') {
        applyTheme('system');
    }
});

// ── 初始化 ──
async function init() {
    initMarkdown();
    if (typeof mermaid !== 'undefined') {
        mermaid.initialize({ startOnLoad: false, theme: 'default' });
    }
    await loadModels();
    await loadSettings();
    await loadConversations();
    loadSuggestedQuestions();
    handleRoute();
    promptInput.focus();
}

init();
