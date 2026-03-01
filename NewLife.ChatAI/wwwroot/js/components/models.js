/**
 * NewLife.ChatAI — 模型选择器组件
 * 下拉菜单展示可用模型，支持能力标识
 */

import { $, escapeHtml } from '../utils.js';
import { getState, setState } from '../store.js';
import { api } from '../api.js';
import { showToast } from './toast.js';

let modelSelectorBtn, modelDropdown, currentModelName;

/**
 * 初始化模型选择器
 */
export function initModelSelector() {
    modelSelectorBtn = $('modelSelectorBtn');
    modelDropdown = $('modelDropdown');
    currentModelName = $('currentModelName');

    modelSelectorBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        const selector = modelSelectorBtn.closest('.model-selector');
        modelDropdown?.classList.toggle('hidden');
        selector?.classList.toggle('open');
    });

    document.addEventListener('click', (e) => {
        if (!e.target.closest('.model-selector')) {
            modelDropdown?.classList.add('hidden');
            modelSelectorBtn?.closest('.model-selector')?.classList.remove('open');
        }
    });
}

/**
 * 从后端加载模型列表
 */
export async function loadModels() {
    try {
        const models = await api('/api/models');
        setState({ models });
        renderModelDropdown();
        // 如果还没选模型，默认选第一个
        if (models.length > 0 && !getState().currentModel) {
            selectModel(models[0]);
        }
    } catch (e) {
        showToast('加载模型列表失败: ' + e.message, 'error');
    }
}

/**
 * 渲染模型下拉列表
 */
export function renderModelDropdown() {
    if (!modelDropdown) return;
    modelDropdown.innerHTML = '';

    const { models, currentModel } = getState();

    for (const m of models) {
        const opt = document.createElement('div');
        opt.className = `model-option${currentModel?.code === m.code ? ' active' : ''}`;

        // 能力标识
        const badges = [];
        if (m.supportImageGeneration) badges.push('🖼️');
        if (m.supportVision) badges.push('👁️');
        if (m.supportThinking) badges.push('🧠');
        if (m.supportFunctionCalling) badges.push('🔧');

        opt.innerHTML = `
            <span class="model-option-name">${escapeHtml(m.name)}</span>
            <span class="model-option-badges">${badges.join(' ')}</span>`;

        opt.addEventListener('click', () => {
            selectModel(m);
            modelDropdown.classList.add('hidden');
            modelSelectorBtn?.closest('.model-selector')?.classList.remove('open');
        });

        modelDropdown.appendChild(opt);
    }
}

/**
 * 选择模型
 * @param {Object} model
 */
export function selectModel(model) {
    setState({ currentModel: model });
    if (currentModelName) currentModelName.textContent = model.name;

    // 更新下拉选中态
    modelDropdown?.querySelectorAll('.model-option').forEach(el => el.classList.remove('active'));
    modelDropdown?.querySelectorAll('.model-option').forEach(opt => {
        if (opt.querySelector('.model-option-name')?.textContent === model.name) {
            opt.classList.add('active');
        }
    });

    // 思考模式联动
    _updateThinkingModeAvailability(model);
}

/** 更新思考模式可用性 */
function _updateThinkingModeAvailability(model) {
    const thinkingModeDropdown = $('thinkingModeDropdown');
    const thinkOpt = thinkingModeDropdown?.querySelector('[data-mode="1"]');
    if (!thinkOpt) return;

    if (model?.supportThinking) {
        thinkOpt.classList.remove('disabled');
    } else {
        thinkOpt.classList.add('disabled');
        if (getState().thinkingMode === 1) {
            setThinkingMode(0);
        }
    }
}

/**
 * 设置思考模式
 * @param {number} mode - 0=自动, 1=思考, 2=快速
 */
export function setThinkingMode(mode) {
    setState({ thinkingMode: mode });
    const labels = { 0: '自动', 1: '思考', 2: '快速' };
    const label = $('thinkingModeLabel');
    if (label) label.textContent = labels[mode];

    const dropdown = $('thinkingModeDropdown');
    dropdown?.querySelectorAll('.dropdown-item').forEach(el => {
        el.classList.toggle('active', parseInt(el.dataset.mode) === mode);
    });
}

/**
 * 填充设置页的模型选择下拉
 */
export function populateSettingsModels() {
    const sel = $('settingDefaultModel');
    if (!sel) return;
    sel.innerHTML = '';
    for (const m of getState().models) {
        const opt = document.createElement('option');
        opt.value = m.code;
        opt.textContent = m.name;
        sel.appendChild(opt);
    }
}
