/**
 * NewLife.ChatAI — Toast 通知组件
 */

import { $ } from '../utils.js';

/**
 * 显示 Toast 通知
 * @param {string} message - 通知内容
 * @param {'error'|'success'|'warning'} [type=''] - 类型
 * @param {number} [duration=3000] - 显示时长(ms)
 */
export function showToast(message, type = '', duration = 3000) {
    const container = $('toastContainer');
    if (!container) return;

    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.textContent = message;
    container.appendChild(el);

    setTimeout(() => {
        el.style.animation = 'toastOut .3s ease forwards';
        el.addEventListener('animationend', () => el.remove());
    }, duration);
}

/**
 * 显示确认弹窗
 * @param {string} title
 * @param {string} message
 * @returns {Promise<boolean>}
 */
export function showConfirm(title, message) {
    return new Promise(resolve => {
        const modal = $('confirmModal');
        const titleEl = $('confirmTitle');
        const msgEl = $('confirmMessage');
        const okBtn = $('confirmOk');
        const cancelBtn = $('confirmCancel');

        if (!modal || !okBtn || !cancelBtn) {
            resolve(false);
            return;
        }

        titleEl.textContent = title;
        msgEl.textContent = message;
        modal.classList.remove('hidden');

        const cleanup = () => {
            modal.classList.add('hidden');
            okBtn.removeEventListener('click', onOk);
            cancelBtn.removeEventListener('click', onCancel);
        };

        const onOk = () => { cleanup(); resolve(true); };
        const onCancel = () => { cleanup(); resolve(false); };

        okBtn.addEventListener('click', onOk);
        cancelBtn.addEventListener('click', onCancel);
    });
}
