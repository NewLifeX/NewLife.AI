/**
 * NewLife.ChatAI — 反馈/点踩组件
 * 选择原因标签 + 文本描述
 */

import { $ } from '../utils.js';
import { getState } from '../store.js';
import { api } from '../api.js';
import { showToast } from './toast.js';

let feedbackModal, dislikeMsgId = null;

/**
 * 初始化反馈组件
 */
export function initFeedback() {
    feedbackModal = $('feedbackModal');
    if (!feedbackModal) return;

    $('feedbackClose')?.addEventListener('click', closeFeedback);
    feedbackModal.addEventListener('click', (e) => {
        if (e.target === feedbackModal) closeFeedback();
    });

    $('feedbackSubmitBtn')?.addEventListener('click', _submitFeedback);
}

/**
 * 打开反馈弹窗
 * @param {number} messageId
 */
export function openFeedback(messageId) {
    dislikeMsgId = messageId;
    if (!feedbackModal) return;

    // 重置表单
    const feedbackText = $('feedbackText');
    if (feedbackText) feedbackText.value = '';
    feedbackModal.querySelectorAll('.tag-checkbox input').forEach(c => c.checked = false);
    feedbackModal.classList.remove('hidden');
}

/**
 * 关闭反馈弹窗
 */
export function closeFeedback() {
    feedbackModal?.classList.add('hidden');
    dislikeMsgId = null;
}

// ════════════════════════════════════════

/** 提交反馈 */
async function _submitFeedback() {
    if (!dislikeMsgId) return;

    const tags = [...feedbackModal.querySelectorAll('.tag-checkbox input:checked')].map(c => c.value);
    const text = $('feedbackText')?.value?.trim() || '';
    const reason = [...tags, text].filter(Boolean).join('; ');

    try {
        await api(`/api/messages/${dislikeMsgId}/feedback`, {
            method: 'POST',
            body: {
                type: 2,
                reason: reason || null,
                allowTraining: getState().settings?.allowTraining,
            },
        });

        // 更新 UI 中的按钮状态
        const msgDiv = document.querySelector(`[data-message-id="${dislikeMsgId}"]`);
        if (msgDiv) {
            const dislikeBtn = msgDiv.querySelector('[data-action="dislike"]');
            dislikeBtn?.classList.add('active');
            const likeBtn = msgDiv.querySelector('[data-action="like"]');
            likeBtn?.classList.remove('active');
        }

        closeFeedback();
        showToast('感谢你的反馈', 'success');
    } catch (e) {
        showToast('提交失败: ' + e.message, 'error');
    }
}
