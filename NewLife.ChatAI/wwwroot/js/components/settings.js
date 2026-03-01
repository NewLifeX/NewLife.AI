/**
 * NewLife.ChatAI — 设置组件
 * 常规设置、对话设置、隐私与数据、使用量统计、AppKey 管理
 */

import { $, relativeTime, formatTokenCount } from '../utils.js';
import { getState, setState } from '../store.js';
import { api } from '../api.js';
import { showToast, showConfirm } from './toast.js';
import { applyTheme } from './theme.js';
import { selectModel } from './models.js';
import { loadConversations } from './sidebar.js';
import { showWelcomePage } from './welcome.js';

let settingsModal;

/** Tab 名称映射 */
const _tabLabels = {
    general: '通用设置',
    chat: '对话设置',
    privacy: '数据与隐私',
    usage: '使用统计',
    appkey: 'API 密钥',
    about: '关于',
};

/**
 * 初始化设置组件
 */
export function initSettings() {
    settingsModal = $('settingsModal');
    if (!settingsModal) return;

    // 关闭 / 返回
    $('settingsBack')?.addEventListener('click', async () => {
        await saveSettings();
        closeSettings();
    });

    // ESC 关闭
    settingsModal.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeSettings();
    });

    // 点击遮罩关闭
    settingsModal.addEventListener('click', (e) => {
        if (e.target === settingsModal) closeSettings();
    });

    // Tab 切换
    settingsModal.querySelectorAll('.settings-tab').forEach(tab => {
        tab.addEventListener('click', () => _switchTab(tab.dataset.tab));
    });

    // 主题实时预览
    $('settingTheme')?.addEventListener('change', () => {
        applyTheme($('settingTheme').value);
    });

    // 字号滑块
    $('settingFontSize')?.addEventListener('input', () => {
        const v = $('settingFontSize').value;
        $('fontSizeValue').textContent = v + 'px';
        document.documentElement.style.setProperty('--font-size', v + 'px');
    });

    // 导出数据
    $('settingExport')?.addEventListener('click', _exportData);

    // 清除全部对话
    $('settingClear')?.addEventListener('click', _clearAllConversations);

    // AppKey 管理
    $('appKeyAddBtn')?.addEventListener('click', _addAppKey);
}

/**
 * 打开设置
 */
export function openSettings() {
    if (!settingsModal) return;
    settingsModal.classList.remove('hidden');
    _populateModels();
    applySettingsUI();
    loadUsageStats();
    _loadAppKeys();

    // 默认选中第一个 Tab
    _switchTab('general');
}

/**
 * 关闭设置
 */
export function closeSettings() {
    settingsModal?.classList.add('hidden');
}

/**
 * 加载设置
 */
export async function loadSettings() {
    try {
        const s = await api('/api/user/settings');
        setState({ settings: s });
        _applyConfigValues(s);
    } catch { /* 使用默认值 */ }
}

/**
 * 将设置的值写入表单
 */
export function applySettingsUI() {
    const s = getState().settings;
    _applyConfigValues(s);
}

// ════════════════════════════════════════

/** 将服务端设置映射到表单控件 */
function _applyConfigValues(s) {
    if (!s) return;
    _setVal('settingLanguage', s.language || 'zh-CN');
    _setVal('settingTheme', s.theme || 'light');
    _setVal('settingFontSize', s.fontSize || 16);
    if ($('fontSizeValue')) $('fontSizeValue').textContent = (s.fontSize || 16) + 'px';
    _setVal('settingSendKey', s.sendShortcut || 'enter');
    _setVal('settingDefaultModel', s.defaultModel || '');
    _setVal('settingDefaultThinking', String(s.defaultThinkingMode ?? 0));
    _setVal('settingContextRounds', s.contextRounds || 10);
    _setVal('settingSystemPrompt', s.systemPrompt || '');
    _setChecked('settingAllowTraining', s.allowTraining || false);

    // 应用到运行时
    setState({ sendShortcut: (s.sendShortcut || 'enter').toLowerCase() });
    applyTheme(s.theme || 'light');
    document.documentElement.style.setProperty('--font-size', (s.fontSize || 16) + 'px');

    // 默认模型
    if (s.defaultModel && !getState().currentModel) {
        const m = getState().models.find(x => x.code === s.defaultModel);
        if (m) selectModel(m);
    }
}

/** 保存设置到服务端 */
async function saveSettings() {
    const s = {
        language: _getVal('settingLanguage'),
        theme: _getVal('settingTheme'),
        fontSize: parseInt(_getVal('settingFontSize')),
        sendShortcut: _getVal('settingSendKey'),
        defaultModel: _getVal('settingDefaultModel'),
        defaultThinkingMode: parseInt(_getVal('settingDefaultThinking')),
        contextRounds: parseInt(_getVal('settingContextRounds')),
        systemPrompt: _getVal('settingSystemPrompt'),
        allowTraining: _getChecked('settingAllowTraining'),
    };
    try {
        const result = await api('/api/user/settings', { method: 'PUT', body: s });
        setState({ settings: result });
        _applyConfigValues(result);
        showToast('设置已保存', 'success');
    } catch (e) {
        showToast('保存失败: ' + e.message, 'error');
    }
}

/** 加载使用量统计 */
async function loadUsageStats() {
    try {
        const data = await api('/api/usage/summary');
        _setText('usageConversations', data.conversations ?? 0);
        _setText('usageMessages', data.messages ?? 0);
        _setText('usageTotalTokens', formatTokenCount(data.totalTokens ?? 0));
        _setText('usageLastActive', data.lastActiveTime ? relativeTime(data.lastActiveTime) : '暂无');
    } catch { /* 忽略 */ }
}

/** 填充模型下拉 */
function _populateModels() {
    const sel = $('settingDefaultModel');
    if (!sel) return;
    sel.innerHTML = '<option value="">跟随选择</option>';
    for (const m of getState().models || []) {
        const opt = document.createElement('option');
        opt.value = m.code;
        opt.textContent = m.name;
        sel.appendChild(opt);
    }
}

/** 导出数据 */
async function _exportData() {
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
}

/** 清除所有对话 */
async function _clearAllConversations() {
    const ok = await showConfirm('清除所有对话', '确定清除所有对话数据？此操作不可撤销。');
    if (!ok) return;
    try {
        await api('/api/user/conversations', { method: 'DELETE' });
        setState({ currentConversationId: null });
        showWelcomePage();
        await loadConversations();
        showToast('所有对话已清除', 'success');
    } catch (e) {
        showToast('清除失败: ' + e.message, 'error');
    }
}

// ── AppKey 管理 ──

async function _loadAppKeys() {
    const container = $('appKeyList');
    if (!container) return;
    try {
        const keys = await api('/api/user/appkeys');
        container.innerHTML = '';
        if (!keys || keys.length === 0) {
            container.innerHTML = '<div class="empty-hint">暂无 AppKey</div>';
            return;
        }
        for (const key of keys) {
            const el = document.createElement('div');
            el.className = 'appkey-item';
            el.innerHTML = `
                <div class="appkey-item-info">
                    <span class="appkey-item-name">${key.name || 'AppKey'}</span>
                    <code class="appkey-item-key">${key.maskedKey || '****'}</code>
                </div>
                <div class="appkey-item-actions">
                    <button class="btn-outline btn-danger" data-key-id="${key.id}">删除</button>
                </div>`;
            el.querySelector('.btn-danger').addEventListener('click', () => _deleteAppKey(key.id));
            container.appendChild(el);
        }
    } catch { /* 忽略 */ }
}

async function _addAppKey() {
    const nameInput = $('appKeyName');
    const name = nameInput?.value?.trim() || 'AppKey';
    try {
        const result = await api('/api/user/appkeys', { method: 'POST', body: { name } });
        if (nameInput) nameInput.value = '';
        if (result.key) {
            await navigator.clipboard.writeText(result.key).catch(() => { });
            showToast(`AppKey 已创建并复制: ${result.key}`, 'success', 8000);
        }
        _loadAppKeys();
    } catch (e) {
        showToast('创建失败: ' + e.message, 'error');
    }
}

async function _deleteAppKey(id) {
    const ok = await showConfirm('删除 AppKey', '确定要删除此 AppKey？关联的调用将失效。');
    if (!ok) return;
    try {
        await api(`/api/user/appkeys/${id}`, { method: 'DELETE' });
        _loadAppKeys();
        showToast('已删除', 'success');
    } catch (e) {
        showToast('删除失败: ' + e.message, 'error');
    }
}

// ── 辅助 ──

/** 切换 Tab 面板 */
function _switchTab(tabName) {
    if (!settingsModal) return;
    // 更新 tab 高亮
    settingsModal.querySelectorAll('.settings-tab').forEach(t => {
        t.classList.toggle('active', t.dataset.tab === tabName);
    });
    // 切换面板
    settingsModal.querySelectorAll('.settings-panel').forEach(p => {
        p.classList.toggle('active', p.dataset.panel === tabName);
    });
    // 更新右侧标题
    const titleEl = $('settingsPanelTitle');
    if (titleEl) titleEl.textContent = _tabLabels[tabName] || tabName;
}

function _setVal(id, val) { const el = $(id); if (el) el.value = val; }
function _getVal(id) { return $(id)?.value || ''; }
function _setChecked(id, v) { const el = $(id); if (el) el.checked = v; }
function _getChecked(id) { return $(id)?.checked || false; }
function _setText(id, txt) { const el = $(id); if (el) el.textContent = txt; }
