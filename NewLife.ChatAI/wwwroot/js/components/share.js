/**
 * NewLife.ChatAI — 分享组件
 * 生成对话共享链接
 */

import { $ } from '../utils.js';
import { getState } from '../store.js';
import { api } from '../api.js';
import { showToast } from './toast.js';

let shareModal;

/**
 * 初始化分享组件
 */
export function initShare() {
    shareModal = $('shareModal');
    if (!shareModal) return;

    $('shareClose')?.addEventListener('click', closeShare);
    shareModal.addEventListener('click', (e) => {
        if (e.target === shareModal) closeShare();
    });

    $('shareCopyBtn')?.addEventListener('click', _copyLink);

    $('shareCreateBtn')?.addEventListener('click', _createShareLink);
}

/**
 * 打开分享弹窗
 */
export function openShare() {
    const { currentConversationId } = getState();
    if (!currentConversationId) {
        showToast('请先打开一个对话', 'warning');
        return;
    }
    if (!shareModal) return;

    // 重置状态
    const linkInput = $('shareLinkInput');
    if (linkInput) linkInput.value = '';
    $('shareStatus')?.classList.add('hidden');
    $('shareCopyBtn')?.classList.add('hidden');

    shareModal.classList.remove('hidden');
    _createShareLink();
}

/**
 * 关闭分享弹窗
 */
export function closeShare() {
    shareModal?.classList.add('hidden');
}

// ════════════════════════════════════════

/** 创建共享链接 */
async function _createShareLink() {
    const { currentConversationId } = getState();
    if (!currentConversationId) return;
    const statusEl = $('shareStatus');

    try {
        if (statusEl) {
            statusEl.textContent = '正在生成链接...';
            statusEl.classList.remove('hidden');
        }

        const result = await api(`/api/conversations/${currentConversationId}/share`, { method: 'POST' });
        const url = `${window.location.origin}/share/${result.token}`;
        const linkInput = $('shareLinkInput');
        if (linkInput) linkInput.value = url;
        $('shareCopyBtn')?.classList.remove('hidden');

        if (statusEl) {
            statusEl.textContent = '链接已生成，有效期 7 天';
            const expiry = result.expireTime ? new Date(result.expireTime).toLocaleDateString() : '';
            if (expiry) statusEl.textContent = `链接已生成，有效期至 ${expiry}`;
        }
    } catch (e) {
        showToast('生成分享链接失败: ' + e.message, 'error');
        if (statusEl) {
            statusEl.textContent = '生成失败，请重试';
        }
    }
}

/** 复制链接 */
async function _copyLink() {
    const url = $('shareLinkInput')?.value;
    if (!url) return;
    try {
        await navigator.clipboard.writeText(url);
        const btn = $('shareCopyBtn');
        if (btn) {
            btn.textContent = '已复制';
            setTimeout(() => { btn.textContent = '复制'; }, 2000);
        }
    } catch {
        showToast('复制失败', 'error');
    }
}
