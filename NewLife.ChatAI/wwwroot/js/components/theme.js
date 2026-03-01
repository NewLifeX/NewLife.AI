/**
 * NewLife.ChatAI — 主题管理组件
 * 支持浅色、深色、跟随系统三种模式
 */

import { getState, setState, subscribe } from '../store.js';

let _mediaQuery = null;
let _mediaHandler = null;

/**
 * 初始化主题系统
 */
export function initTheme() {
    _mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    _mediaHandler = () => {
        if (getState().theme === 'system') {
            _applyEffectiveTheme('system');
        }
    };
    _mediaQuery.addEventListener('change', _mediaHandler);

    // 订阅主题变更
    subscribe('theme', (theme) => {
        _applyEffectiveTheme(theme);
    });
}

/**
 * 应用主题
 * @param {'light'|'dark'|'system'} theme
 */
export function applyTheme(theme) {
    setState({ theme });
    _applyEffectiveTheme(theme);
}

/**
 * 获取当前生效的主题
 * @returns {'light'|'dark'}
 */
export function getEffectiveTheme() {
    const theme = getState().theme;
    if (theme === 'system') {
        return _mediaQuery?.matches ? 'dark' : 'light';
    }
    return theme;
}

/**
 * 切换到下一个主题 (light → dark → system → light)
 */
export function toggleTheme() {
    const cycle = { light: 'dark', dark: 'system', system: 'light' };
    const next = cycle[getState().theme] || 'light';
    applyTheme(next);
}

/** 内部：应用生效主题到 DOM */
function _applyEffectiveTheme(theme) {
    let effective = theme;
    if (theme === 'system') {
        effective = _mediaQuery?.matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', effective);

    // 切换代码高亮主题
    const hlLight = document.getElementById('hlThemeLight');
    const hlDark = document.getElementById('hlThemeDark');
    if (hlLight && hlDark) {
        hlLight.disabled = effective === 'dark';
        hlDark.disabled = effective !== 'dark';
    }
}
