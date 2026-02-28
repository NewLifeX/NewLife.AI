/**
 * NewLife.ChatAI — 输入组件
 * 多行文本输入、附件上传、思考模式、发送/停止
 */

import { $, escapeHtml } from '../utils.js';
import { getState, setState } from '../store.js';
import { api } from '../api.js';
import { showToast } from './toast.js';
import { setThinkingMode } from './models.js';

let textarea, composerEl, charCountEl, attachPreview;

/**
 * 初始化输入组件
 */
export function initComposer() {
    composerEl = $('composer');
    textarea = $('promptInput');
    charCountEl = $('charCount');
    attachPreview = $('attachmentPreview');

    if (!textarea) return;

    // 自适应高度
    textarea.addEventListener('input', () => {
        _autoResize();
        _updateCharCount();
    });

    // 快捷键发送
    textarea.addEventListener('keydown', (e) => {
        const sendKey = getState().sendShortcut || 'enter';

        if (sendKey === 'enter' && e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            _handleSend();
        } else if (sendKey === 'ctrl+enter' && e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            _handleSend();
        }
    });

    // 发送按钮
    $('sendBtn')?.addEventListener('click', _handleSend);
    $('stopBtn')?.addEventListener('click', () => {
        window.dispatchEvent(new CustomEvent('chatai:stopGeneration'));
    });

    // 附件上传按钮
    $('attachBtn')?.addEventListener('click', () => {
        $('fileInput')?.click();
    });

    $('fileInput')?.addEventListener('change', (e) => {
        _handleFiles(e.target.files);
        e.target.value = '';
    });

    // 拖拽上传
    composerEl?.addEventListener('dragover', (e) => {
        e.preventDefault();
        composerEl.classList.add('drag-over');
    });
    composerEl?.addEventListener('dragleave', () => {
        composerEl.classList.remove('drag-over');
    });
    composerEl?.addEventListener('drop', (e) => {
        e.preventDefault();
        composerEl.classList.remove('drag-over');
        _handleFiles(e.dataTransfer.files);
    });

    // 粘贴上传
    textarea?.addEventListener('paste', (e) => {
        const items = e.clipboardData.items;
        const files = [];
        for (const item of items) {
            if (item.kind === 'file') {
                files.push(item.getAsFile());
            }
        }
        if (files.length > 0) {
            e.preventDefault();
            _handleFiles(files);
        }
    });

    // 思考模式按钮：展开/收起下拉菜单
    const thinkingModeBtn = $('thinkingModeBtn');
    const thinkingModeDropdown = $('thinkingModeDropdown');
    thinkingModeBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        thinkingModeDropdown?.classList.toggle('hidden');
    });
    // 思考模式下拉项点击
    thinkingModeDropdown?.querySelectorAll('.dropdown-item').forEach(item => {
        item.addEventListener('click', () => {
            if (item.classList.contains('disabled')) return;
            setThinkingMode(parseInt(item.dataset.mode));
            thinkingModeDropdown.classList.add('hidden');
        });
    });
    // 点击外部关闭思考模式下拉
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.thinking-mode-selector')) {
            thinkingModeDropdown?.classList.add('hidden');
        }
    });

    // 监听生成状态变化
    window.addEventListener('chatai:generatingChanged', (e) => {
        _updateGeneratingUI(e.detail.isGenerating);
    });

    // 监听附件变化
    window.addEventListener('chatai:attachmentsChanged', _renderAttachments);
}

/**
 * 聚焦输入框
 */
export function focusComposer() {
    textarea?.focus();
}

/**
 * 重置输入
 */
export function resetComposer() {
    if (textarea) {
        textarea.value = '';
        _autoResize();
        _updateCharCount();
    }
}

/**
 * 设置输入值
 * @param {string} text
 */
export function setComposerValue(text) {
    if (textarea) {
        textarea.value = text;
        _autoResize();
        _updateCharCount();
    }
}

// ════════════════════════════════════════

/** 发送 */
function _handleSend() {
    const text = textarea?.value?.trim();
    if (!text || getState().isGenerating) return;
    textarea.value = '';
    _autoResize();
    _updateCharCount();
    window.dispatchEvent(new CustomEvent('chatai:sendMessage', { detail: { content: text } }));
}

/** 自动调整高度 */
function _autoResize() {
    if (!textarea) return;
    textarea.style.height = 'auto';
    const maxHeight = window.innerWidth <= 768 ? 120 : 200;
    textarea.style.height = Math.min(textarea.scrollHeight, maxHeight) + 'px';
}

/** 更新字符数 */
function _updateCharCount() {
    if (!charCountEl || !textarea) return;
    const len = textarea.value.length;
    charCountEl.textContent = len > 0 ? `${len}` : '';
}

/** 处理文件上传 */
async function _handleFiles(files) {
    if (!files || files.length === 0) return;
    const maxSize = 5 * 1024 * 1024; // 5MB

    for (const file of files) {
        if (file.size > maxSize) {
            showToast(`文件 ${file.name} 超过 5MB 限制`, 'error');
            continue;
        }

        const formData = new FormData();
        formData.append('file', file);

        try {
            const result = await api('/api/attachments/upload', {
                method: 'POST',
                body: formData,
            });
            const attachments = [...getState().pendingAttachments, {
                id: result.id,
                name: file.name,
                size: file.size,
                type: file.type,
                previewUrl: file.type.startsWith('image/') ? URL.createObjectURL(file) : null,
            }];
            setState({ pendingAttachments: attachments });
            _renderAttachments();
        } catch (e) {
            showToast('上传失败: ' + e.message, 'error');
        }
    }
}

/** 渲染附件预览 */
function _renderAttachments() {
    if (!attachPreview) return;
    const { pendingAttachments } = getState();
    if (!pendingAttachments || pendingAttachments.length === 0) {
        attachPreview.innerHTML = '';
        attachPreview.classList.add('hidden');
        return;
    }

    attachPreview.classList.remove('hidden');
    attachPreview.innerHTML = pendingAttachments.map((a, i) => {
        const preview = a.previewUrl
            ? `<img src="${a.previewUrl}" class="attach-preview-thumb" alt="${escapeHtml(a.name)}">`
            : `<span class="attach-icon">📎</span>`;
        const size = (a.size / 1024).toFixed(1) + 'KB';
        return `<div class="attach-preview-item">
            ${preview}
            <span title="${escapeHtml(a.name)}">${escapeHtml(a.name)}</span>
            <span>${size}</span>
            <button class="attach-preview-remove" data-index="${i}" title="移除">✕</button>
        </div>`;
    }).join('');

    attachPreview.querySelectorAll('.attach-preview-remove').forEach(btn => {
        btn.addEventListener('click', () => {
            const idx = Number(btn.dataset.index);
            const arr = [...getState().pendingAttachments];
            if (arr[idx]?.previewUrl) URL.revokeObjectURL(arr[idx].previewUrl);
            arr.splice(idx, 1);
            setState({ pendingAttachments: arr });
            _renderAttachments();
        });
    });
}



/** 更新生成中 UI */
function _updateGeneratingUI(isGenerating) {
    const sendBtn = $('sendBtn');
    const stopBtn = $('stopBtn');

    if (isGenerating) {
        sendBtn?.classList.add('hidden');
        stopBtn?.classList.remove('hidden');
        textarea?.setAttribute('disabled', '');
    } else {
        sendBtn?.classList.remove('hidden');
        stopBtn?.classList.add('hidden');
        textarea?.removeAttribute('disabled');
    }
}
