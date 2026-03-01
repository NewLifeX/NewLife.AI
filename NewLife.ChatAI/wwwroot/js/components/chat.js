/**
 * NewLife.ChatAI — 对话消息组件
 * 消息渲染、SSE 流式接收、思考过程、工具调用、消息操作
 */

import { $, escapeHtml, relativeTime, exactTime } from '../utils.js';
import { getState, setState, setStateSilent } from '../store.js';
import { api } from '../api.js';
import { streamChat } from '../services/sse.js';
import { renderMarkdown, processMermaid } from '../services/markdown.js';
import { showToast } from './toast.js';
import { updateConversationTitle, loadConversations } from './sidebar.js';
import { showChatView } from './welcome.js';

let messagesEl, messagesInner, scrollBottomBtn;

/**
 * 初始化对话组件
 */
export function initChat() {
    messagesEl = $('messages');
    messagesInner = $('messagesInner');
    scrollBottomBtn = $('scrollBottomBtn');

    // 滚动检测
    messagesEl?.addEventListener('scroll', () => {
        const { scrollTop, scrollHeight, clientHeight } = messagesEl;
        const isAtBottom = scrollHeight - scrollTop - clientHeight < 60;
        setStateSilent('userAutoScroll', isAtBottom);
        scrollBottomBtn?.classList.toggle('hidden', isAtBottom);
    });

    scrollBottomBtn?.addEventListener('click', () => {
        setStateSilent('userAutoScroll', true);
        scrollToBottom(true);
        scrollBottomBtn?.classList.add('hidden');
    });

    // 移动端长按菜单
    _initMobileLongPress();
}

/**
 * 打开会话并加载消息
 * @param {number} id
 */
export async function loadMessages(id) {
    showChatView();
    if (!messagesInner) return;
    messagesInner.innerHTML = '';
    _updateUrl(id);

    try {
        const messages = await api(`/api/conversations/${id}/messages`);
        for (const msg of messages) {
            renderMessage(msg);
        }
        _addRegenerateToLastAi();
        scrollToBottom(true);
    } catch (e) {
        showToast('加载消息失败: ' + e.message, 'error');
    }
}

/**
 * 发送消息（SSE 流式）
 * @param {string} content - 消息内容
 */
export async function sendMessage(content) {
    if (!content || getState().isGenerating) return;

    // 没有当前会话时先创建
    if (!getState().currentConversationId) {
        await _createConversation();
    }

    const convId = getState().currentConversationId;
    if (!convId) return;

    // 渲染用户消息
    renderMessage({
        id: 0,
        role: 'user',
        content,
        createTime: new Date().toISOString(),
    });

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
    setState({ isGenerating: true, abortController: new AbortController() });
    window.dispatchEvent(new CustomEvent('chatai:generatingChanged', { detail: { isGenerating: true } }));

    const { thinkingMode, pendingAttachments, abortController } = getState();
    const attachmentIds = pendingAttachments.map(a => a.id);
    setState({ pendingAttachments: [] });
    window.dispatchEvent(new CustomEvent('chatai:attachmentsChanged'));

    let thinkingContent = '';
    let answerContent = '';
    let currentThinkingBlock = null;
    let thinkingStartTime = 0;
    let messageId = 0;
    let usage = null;
    let hasError = false;

    try {
        await streamChat(
            `/api/conversations/${convId}/messages`,
            { content, thinkingMode, attachmentIds },
            {
                onMessageStart(data) {
                    messageId = data.messageId;
                    setState({ currentMessageId: messageId });
                    aiDiv.dataset.messageId = messageId;
                    thinkingStartTime = Date.now();
                },
                onThinkingDelta(data) {
                    if (!currentThinkingBlock) {
                        currentThinkingBlock = _createThinkingBlock(aiDiv);
                    }
                    thinkingContent += data.content;
                    currentThinkingBlock.querySelector('.thinking-body').textContent = thinkingContent;
                    if (getState().userAutoScroll) scrollToBottom();
                },
                onThinkingDone(data) {
                    if (currentThinkingBlock) {
                        const elapsed = data.thinkingTime || (Date.now() - thinkingStartTime);
                        const header = currentThinkingBlock.querySelector('.thinking-header');
                        header.querySelector('.spinner')?.remove();
                        header.querySelector('span:last-child').textContent = `思考过程（${(elapsed / 1000).toFixed(1)}s）`;
                    }
                    currentThinkingBlock = null;
                    thinkingContent = '';
                },
                onToolCallStart(data) {
                    _createToolCallBlock(aiDiv, data);
                    if (getState().userAutoScroll) scrollToBottom();
                },
                onToolCallDone(data) {
                    _updateToolCallBlock(data.toolCallId, data.result, true);
                },
                onToolCallError(data) {
                    _updateToolCallBlock(data.toolCallId, data.error, false);
                },
                onContentDelta(data) {
                    answerContent += data.content;
                    const contentEl = aiDiv.querySelector('.msg-content');
                    contentEl.innerHTML = renderMarkdown(answerContent);
                    aiDiv.querySelector('.typing-indicator')?.remove();
                    if (getState().userAutoScroll) scrollToBottom();
                },
                onMessageDone(data) {
                    usage = data.usage;
                    if (data.title) updateConversationTitle(data.title);
                },
                onError(data) {
                    hasError = true;
                    _showErrorInMessage(aiDiv, data.code, data.message);
                },
            },
            abortController.signal
        );
    } catch (e) {
        if (e.name !== 'AbortError') {
            hasError = true;
            _showErrorInMessage(aiDiv, 'NETWORK_ERROR', '网络异常，请重试');
        }
    } finally {
        _finishGeneration(aiDiv, messageId, usage, hasError);
    }
}

/**
 * 渲染单条消息
 * @param {Object} msg
 * @returns {HTMLElement}
 */
export function renderMessage(msg) {
    const div = document.createElement('div');
    div.className = `msg ${msg.role}`;
    div.dataset.messageId = msg.id;

    const isUser = msg.role === 'user';
    const avatarContent = isUser ? 'U' : '🤖';

    let contentHtml;
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
                attachmentsHtml = '<div class="msg-attachments">' + ids.map(id =>
                    `<div class="msg-attach-item"><a href="/api/attachments/${id}" target="_blank" class="msg-attach-link">📎 附件 #${id}</a></div>`
                ).join('') + '</div>';
            }
        } catch { /* 忽略解析错误 */ }
    }

    // 历史思考过程
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
            ${isUser ? _renderUserActions(msg) : _renderAiActions(msg)}
        </div>`;

    messagesInner?.appendChild(div);

    // 折叠/展开思考过程
    const thinkBlock = div.querySelector('.thinking-block');
    if (thinkBlock) {
        thinkBlock.querySelector('.thinking-header').addEventListener('click', () => {
            thinkBlock.querySelector('.thinking-toggle').classList.toggle('collapsed');
            thinkBlock.querySelector('.thinking-body').classList.toggle('collapsed');
        });
    }

    // 处理 Mermaid
    if (!isUser) {
        const md = div.querySelector('.md-content');
        if (md) processMermaid(md);
    }

    return div;
}

/**
 * 停止生成
 */
export function stopGeneration() {
    const { abortController, currentMessageId } = getState();
    abortController?.abort();
    if (currentMessageId) {
        api(`/api/messages/${currentMessageId}/stop`, { method: 'POST' }).catch(() => { });
    }
}

/**
 * 滚动到底部
 * @param {boolean} [instant=false]
 */
export function scrollToBottom(instant = false) {
    if (!getState().userAutoScroll && !instant) return;
    if (messagesEl) messagesEl.scrollTop = messagesEl.scrollHeight;
}

// ════════════════════════════════════════
// 内部函数
// ════════════════════════════════════════

/** 创建会话 */
async function _createConversation() {
    try {
        const result = await api('/api/conversations', {
            method: 'POST',
            body: { title: '新建对话', modelCode: getState().currentModel?.code },
        });
        setState({ currentConversationId: result.id });
        showChatView();
        _updateUrl(result.id);
        await loadConversations();
    } catch (e) {
        showToast('创建对话失败: ' + e.message, 'error');
    }
}

/** 完成生成后的收尾工作 */
function _finishGeneration(aiDiv, messageId, usage, hasError) {
    setState({ isGenerating: false, currentMessageId: null, abortController: null });
    window.dispatchEvent(new CustomEvent('chatai:generatingChanged', { detail: { isGenerating: false } }));

    aiDiv.querySelector('.typing-indicator')?.remove();

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
            <button class="msg-action-btn" data-action="copy" data-id="${messageId}" title="复制">📋</button>
            <button class="msg-action-btn" data-action="edit" data-id="${messageId}" data-user="false" title="编辑">✏️</button>
            <button class="msg-action-btn" data-action="like" data-id="${messageId}" title="点赞">👍</button>
            <button class="msg-action-btn" data-action="dislike" data-id="${messageId}" title="点踩">👎</button>
            <button class="msg-action-btn" data-action="share" title="分享">🔗</button>`;
        body.appendChild(actions);
        _bindActionEvents(actions);

        // Mermaid
        const md = aiDiv.querySelector('.md-content');
        if (md) processMermaid(md);

        _addRegenerateToLastAi();
    }

    // 时间
    const timeEl = document.createElement('div');
    timeEl.className = 'msg-time';
    timeEl.textContent = '刚刚';
    timeEl.title = exactTime(new Date().toISOString());
    const body = aiDiv.querySelector('.msg-body');
    const insertBefore = body.querySelector('.msg-usage') || body.querySelector('.msg-actions');
    body.insertBefore(timeEl, insertBefore);

    scrollToBottom();
    $('promptInput')?.focus();
    loadConversations();
}

/** 渲染用户消息操作按钮 */
function _renderUserActions(msg) {
    if (!msg.id) return '';
    return `<div class="msg-actions">
        <button class="msg-action-btn" data-action="copy" data-id="${msg.id}" title="复制">📋</button>
        <button class="msg-action-btn" data-action="edit" data-id="${msg.id}" data-user="true" title="编辑">✏️</button>
    </div>`;
}

/** 渲染 AI 消息操作按钮 */
function _renderAiActions(msg) {
    if (!msg.id) return '';
    return `<div class="msg-actions">
        <button class="msg-action-btn" data-action="copy" data-id="${msg.id}" title="复制">📋</button>
        <button class="msg-action-btn" data-action="edit" data-id="${msg.id}" data-user="false" title="编辑">✏️</button>
        <button class="msg-action-btn" data-action="like" data-id="${msg.id}" title="点赞">👍</button>
        <button class="msg-action-btn" data-action="dislike" data-id="${msg.id}" title="点踩">👎</button>
        <button class="msg-action-btn" data-action="share" title="分享">🔗</button>
    </div>`;
}

/** 绑定操作按钮事件（使用事件委托） */
function _bindActionEvents(container) {
    container.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        const action = btn.dataset.action;
        const msgId = Number(btn.dataset.id);

        switch (action) {
            case 'copy': _copyMessage(msgId); break;
            case 'edit': _editMessage(msgId, btn.dataset.user === 'true'); break;
            case 'like': _likeMessage(msgId, btn); break;
            case 'dislike': _dislikeMessage(msgId); break;
            case 'share': window.dispatchEvent(new CustomEvent('chatai:shareConversation')); break;
            case 'regenerate': _regenerateMessage(msgId); break;
        }
    });
}

/** 初始化已渲染消息的事件委托 */
export function initMessageActions() {
    messagesInner?.addEventListener('click', (e) => {
        const btn = e.target.closest('.msg-actions [data-action]');
        if (!btn) return;
        const action = btn.dataset.action;
        const msgId = Number(btn.dataset.id);

        switch (action) {
            case 'copy': _copyMessage(msgId); break;
            case 'edit': _editMessage(msgId, btn.dataset.user === 'true'); break;
            case 'like': _likeMessage(msgId, btn); break;
            case 'dislike': _dislikeMessage(msgId); break;
            case 'share': window.dispatchEvent(new CustomEvent('chatai:shareConversation')); break;
            case 'regenerate': _regenerateMessage(msgId); break;
        }
    });
}

/** 复制消息 */
async function _copyMessage(msgId) {
    const msgDiv = document.querySelector(`[data-message-id="${msgId}"]`);
    if (!msgDiv) return;
    const text = msgDiv.querySelector('.msg-content')?.textContent || '';
    try {
        await navigator.clipboard.writeText(text);
        const btn = msgDiv.querySelector('[data-action="copy"]');
        if (btn) {
            btn.textContent = '✓';
            btn.classList.add('copied');
            setTimeout(() => { btn.textContent = '📋'; btn.classList.remove('copied'); }, 2000);
        }
    } catch {
        showToast('复制失败', 'error');
    }
}

/** 编辑消息 */
function _editMessage(msgId, isUser) {
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
    actions.innerHTML = '<button class="btn-primary">保存</button><button class="btn-outline">取消</button>';
    textarea.after(actions);

    const msgActions = msgDiv.querySelector('.msg-actions');
    msgActions?.classList.add('hidden');

    // 取消
    actions.querySelector('.btn-outline').addEventListener('click', () => {
        const newContent = document.createElement('div');
        newContent.className = 'msg-content' + (isUser ? '' : ' md-content');
        newContent.innerHTML = isUser ? escapeHtml(originalText) : renderMarkdown(originalText);
        textarea.replaceWith(newContent);
        actions.remove();
        msgActions?.classList.remove('hidden');
    });

    // 保存
    actions.querySelector('.btn-primary').addEventListener('click', async () => {
        const newText = textarea.value.trim();
        if (!newText) return;
        try {
            if (isUser) {
                // 用户消息编辑：移除后续消息并重新发送
                const allMsgs = [...messagesInner.querySelectorAll('.msg')];
                const idx = allMsgs.indexOf(msgDiv);
                for (let i = allMsgs.length - 1; i > idx; i--) allMsgs[i].remove();
                msgDiv.remove();
                await sendMessage(newText);
            } else {
                await api(`/api/messages/${msgId}`, { method: 'PUT', body: { content: newText } });
                const newContent = document.createElement('div');
                newContent.className = 'msg-content md-content';
                newContent.innerHTML = renderMarkdown(newText);
                textarea.replaceWith(newContent);
                actions.remove();
                msgActions?.classList.remove('hidden');
                processMermaid(newContent);
            }
        } catch (e) {
            showToast('编辑失败: ' + e.message, 'error');
        }
    });
}

/** 点赞 */
async function _likeMessage(msgId, btn) {
    try {
        if (btn.classList.contains('active')) {
            await api(`/api/messages/${msgId}/feedback`, { method: 'DELETE' });
            btn.classList.remove('active');
        } else {
            await api(`/api/messages/${msgId}/feedback`, {
                method: 'POST',
                body: { type: 1, reason: null, allowTraining: getState().settings?.allowTraining },
            });
            btn.classList.add('active');
            const dislikeBtn = btn.parentElement.querySelector('[data-action="dislike"]');
            dislikeBtn?.classList.remove('active');
        }
    } catch (e) {
        showToast('操作失败: ' + e.message, 'error');
    }
}

/** 点踩 */
function _dislikeMessage(msgId) {
    window.dispatchEvent(new CustomEvent('chatai:openFeedback', { detail: { messageId: msgId } }));
}

/** 重新生成 */
async function _regenerateMessage(msgId) {
    if (getState().isGenerating) return;
    const msgDiv = document.querySelector(`[data-message-id="${msgId}"]`);
    if (msgDiv) msgDiv.remove();

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

    setState({ isGenerating: true });
    window.dispatchEvent(new CustomEvent('chatai:generatingChanged', { detail: { isGenerating: true } }));

    try {
        const result = await api(`/api/messages/${msgId}/regenerate`, { method: 'POST' });
        if (result) {
            aiDiv.querySelector('.typing-indicator')?.remove();
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
                <button class="msg-action-btn" data-action="copy" data-id="${result.id}" title="复制">📋</button>
                <button class="msg-action-btn" data-action="like" data-id="${result.id}" title="点赞">👍</button>
                <button class="msg-action-btn" data-action="dislike" data-id="${result.id}" title="点踩">👎</button>
                <button class="msg-action-btn" data-action="share" title="分享">🔗</button>
                <button class="msg-action-btn msg-action-regen" data-action="regenerate" data-id="${result.id}" title="重新生成">🔄</button>`;
            body.appendChild(actions);
            _bindActionEvents(actions);
            processMermaid(contentEl);
        }
    } catch (e) {
        showToast('重新生成失败: ' + e.message, 'error');
    } finally {
        setState({ isGenerating: false });
        window.dispatchEvent(new CustomEvent('chatai:generatingChanged', { detail: { isGenerating: false } }));
        scrollToBottom();
    }
}

/** 为最后一条 AI 消息添加重新生成按钮 */
function _addRegenerateToLastAi() {
    if (!messagesInner) return;
    messagesInner.querySelectorAll('.msg-action-regen').forEach(b => b.remove());
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
    btn.dataset.action = 'regenerate';
    btn.dataset.id = msgId;
    actions.appendChild(btn);
}

/** 创建思考过程块 */
function _createThinkingBlock(msgDiv) {
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

    if (typing) body.insertBefore(block, typing);
    else body.insertBefore(block, body.querySelector('.msg-content'));

    block.querySelector('.thinking-header').addEventListener('click', () => {
        block.querySelector('.thinking-toggle').classList.toggle('collapsed');
        block.querySelector('.thinking-body').classList.toggle('collapsed');
    });

    return block;
}

/** 创建工具调用块 */
function _createToolCallBlock(msgDiv, data) {
    const body = msgDiv.querySelector('.msg-body');
    const typing = msgDiv.querySelector('.typing-indicator');

    const block = document.createElement('div');
    block.className = 'tool-call-block';
    block.dataset.toolCallId = data.toolCallId;

    let argsDisplay = '';
    try { argsDisplay = JSON.stringify(JSON.parse(data.arguments), null, 2); }
    catch { argsDisplay = data.arguments || ''; }

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

    if (typing) body.insertBefore(block, typing);
    else body.insertBefore(block, body.querySelector('.msg-content'));

    block.querySelector('.tool-call-header').addEventListener('click', () => {
        block.querySelector('.thinking-toggle').classList.toggle('collapsed');
        block.querySelector('.tool-call-body').classList.toggle('collapsed');
    });
}

/** 更新工具调用块结果 */
function _updateToolCallBlock(toolCallId, result, success) {
    const block = document.querySelector(`[data-tool-call-id="${toolCallId}"]`);
    if (!block) return;

    const statusEl = block.querySelector('.tool-call-status');
    statusEl.classList.remove('pending');
    statusEl.textContent = success ? '✓' : '✗';
    statusEl.style.color = success ? 'var(--success)' : 'var(--danger)';

    const resultDiv = block.querySelector('.tool-call-result');
    if (result) {
        let display = result;
        try { display = JSON.stringify(JSON.parse(result), null, 2); } catch { /* 保持原文 */ }
        resultDiv.innerHTML = `<div class="tool-call-label">${success ? '结果' : '错误'}</div><pre>${escapeHtml(display)}</pre>`;
    }
}

/** 在消息中显示错误 */
function _showErrorInMessage(msgDiv, code, message) {
    msgDiv.querySelector('.typing-indicator')?.remove();
    const body = msgDiv.querySelector('.msg-body');
    const errEl = document.createElement('div');
    errEl.className = 'msg-error';
    errEl.textContent = `${message} (${code})`;
    body.appendChild(errEl);
}

/** 更新 URL */
function _updateUrl(convId) {
    const path = convId ? `/chat/${convId}` : '/chat';
    if (window.location.pathname !== path) {
        history.pushState(null, '', path);
    }
}

/** 移动端长按消息菜单 */
function _initMobileLongPress() {
    let timer = null;
    messagesInner?.addEventListener('touchstart', (e) => {
        const msgEl = e.target.closest('.msg');
        if (!msgEl) return;
        timer = setTimeout(() => {
            e.preventDefault();
            _showMobileContextMenu(msgEl, e.touches[0]);
        }, 500);
    }, { passive: false });
    messagesInner?.addEventListener('touchend', () => clearTimeout(timer));
    messagesInner?.addEventListener('touchmove', () => clearTimeout(timer));
}

/** 移动端上下文菜单 */
function _showMobileContextMenu(msgEl, touch) {
    document.querySelectorAll('.msg-context-menu').forEach(m => m.remove());
    const msgId = msgEl.dataset.messageId;
    const isUser = msgEl.classList.contains('user');

    const menu = document.createElement('div');
    menu.className = 'msg-context-menu';
    let items = '<div class="menu-item" data-action="copy">📋 复制</div>';
    items += '<div class="menu-item" data-action="edit">✏️ 编辑</div>';
    if (!isUser) {
        items += '<div class="menu-item" data-action="like">👍 点赞</div>';
        items += '<div class="menu-item" data-action="dislike">👎 点踩</div>';
        items += '<div class="menu-item" data-action="share">🔗 分享</div>';
    }
    menu.innerHTML = items;
    menu.style.left = Math.min(touch.clientX, window.innerWidth - 160) + 'px';
    menu.style.top = Math.min(touch.clientY, window.innerHeight - 200) + 'px';
    document.body.appendChild(menu);

    menu.addEventListener('click', (ev) => {
        const action = ev.target.closest('[data-action]')?.dataset.action;
        if (!action) return;
        menu.remove();
        switch (action) {
            case 'copy': _copyMessage(Number(msgId)); break;
            case 'edit': _editMessage(Number(msgId), isUser); break;
            case 'like': {
                const btn = msgEl.querySelector('[data-action="like"]');
                if (btn) _likeMessage(Number(msgId), btn); break;
            }
            case 'dislike': _dislikeMessage(Number(msgId)); break;
            case 'share': window.dispatchEvent(new CustomEvent('chatai:shareConversation')); break;
        }
    });

    setTimeout(() => {
        document.addEventListener('click', function handler() {
            menu.remove();
            document.removeEventListener('click', handler);
        });
    }, 10);
}

/**
 * 加载共享对话只读页面
 * @param {string} token
 */
export async function loadSharePage(token) {
    const sidebar = $('sidebar');
    sidebar?.classList.add('collapsed');
    $('sidebarOpenBtn')?.classList.add('hidden');
    $('composer')?.classList.add('hidden');

    showChatView();
    if (!messagesInner) return;
    messagesInner.innerHTML = '';

    const banner = document.createElement('div');
    banner.className = 'share-banner';
    banner.innerHTML = '🔗 共享对话 — 此对话由用户分享，仅供查看';
    messagesInner.appendChild(banner);

    try {
        const data = await api(`/api/share/${token}`);
        if (data.title) {
            document.title = `${data.title} — 共享对话`;
        }
        for (const msg of (data.messages || [])) {
            const div = renderMessage(msg);
        }

        const footer = document.createElement('div');
        footer.className = 'share-footer';
        footer.innerHTML = '使用 <strong>NewLife.ChatAI</strong> 开始你的对话 → <a href="/chat">立即体验</a>';
        messagesInner.appendChild(footer);

        scrollToBottom(true);
    } catch {
        messagesInner.innerHTML = '<div class="share-banner" style="color: var(--text-error);">🚫 共享链接无效或已过期</div>';
    }
}
