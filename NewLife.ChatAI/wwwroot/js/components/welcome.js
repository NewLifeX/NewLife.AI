/**
 * NewLife.ChatAI — 欢迎页组件
 * 新会话欢迎页（空状态），含推荐问题
 */

import { $ } from '../utils.js';

/**
 * 初始化欢迎页
 */
export function initWelcome() {
    _loadSuggestedQuestions();
}

/**
 * 显示欢迎页，隐藏消息区
 */
export function showWelcomePage() {
    $('welcomePage')?.classList.remove('hidden');
    $('messages')?.classList.add('hidden');
    $('scrollBottomBtn')?.classList.add('hidden');
}

/**
 * 显示对话视图，隐藏欢迎页
 */
export function showChatView() {
    $('welcomePage')?.classList.add('hidden');
    $('messages')?.classList.remove('hidden');
}

/** 加载推荐问题 */
function _loadSuggestedQuestions() {
    const container = $('suggestedQuestions');
    if (!container) return;

    const questions = [
        '帮我写一封工作邮件',
        '解释一下量子计算的基本原理',
        '用 C# 写一个快速排序算法',
        '帮我翻译一段英文文档',
    ];

    container.innerHTML = '';
    for (const q of questions) {
        const btn = document.createElement('button');
        btn.className = 'suggested-q';
        btn.textContent = q;
        btn.addEventListener('click', () => {
            // 触发发送消息事件
            window.dispatchEvent(new CustomEvent('chatai:sendMessage', { detail: { content: q } }));
        });
        container.appendChild(btn);
    }
}
