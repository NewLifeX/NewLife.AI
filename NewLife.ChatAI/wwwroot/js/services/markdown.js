/**
 * NewLife.ChatAI — Markdown 渲染服务
 * 集成 marked + highlight.js + KaTeX + Mermaid
 */

import { escapeHtml } from '../utils.js';

let _initialized = false;

/**
 * 初始化 Markdown 渲染器
 */
export function initMarkdown() {
    if (_initialized) return;
    if (typeof marked === 'undefined') return;

    const renderer = new marked.Renderer();

    // 代码块：添加复制按钮
    renderer.code = function ({ text, lang }) {
        const langClass = lang ? `language-${lang}` : '';
        let highlighted = text;
        if (typeof hljs !== 'undefined' && lang && hljs.getLanguage(lang)) {
            try {
                highlighted = hljs.highlight(text, { language: lang }).value;
            } catch { /* 降级为纯文本 */ }
        }
        const langLabel = lang ? `<span class="code-lang">${escapeHtml(lang)}</span>` : '';
        return `<pre>${langLabel}<code class="hljs ${langClass}">${highlighted}</code><button class="code-copy-btn" onclick="window.__copyCode(this)">复制</button></pre>`;
    };

    // 图片：支持点击预览
    renderer.image = function ({ href, title, text }) {
        return `<img src="${href}" alt="${escapeHtml(text || '')}" title="${escapeHtml(title || '')}" onclick="window.__openLightbox(this.src)" loading="lazy" />`;
    };

    marked.setOptions({
        renderer,
        breaks: true,
        gfm: true,
    });

    // Mermaid 初始化
    if (typeof mermaid !== 'undefined') {
        mermaid.initialize({ startOnLoad: false, theme: 'default' });
    }

    _initialized = true;
}

/**
 * 渲染 Markdown 文本为 HTML
 * @param {string} text
 * @returns {string}
 */
export function renderMarkdown(text) {
    if (!text) return '';
    if (typeof marked === 'undefined') return escapeHtml(text);

    // 保护 LaTeX 公式，避免被 marked 处理
    const blocks = [];
    let safe = text.replace(/\$\$([\s\S]*?)\$\$/g, (_, m) => {
        blocks.push(m);
        return `%%BLOCK_${blocks.length - 1}%%`;
    });
    safe = safe.replace(/\$([^\n$]+?)\$/g, (_, m) => {
        blocks.push(m);
        return `%%INLINE_${blocks.length - 1}%%`;
    });

    let html = marked.parse(safe);

    // 恢复 LaTeX 公式
    html = html.replace(/%%BLOCK_(\d+)%%/g, (_, i) => {
        try {
            return katex.renderToString(blocks[i], { displayMode: true, throwOnError: false });
        } catch {
            return `$$${blocks[i]}$$`;
        }
    });
    html = html.replace(/%%INLINE_(\d+)%%/g, (_, i) => {
        try {
            return katex.renderToString(blocks[i], { displayMode: false, throwOnError: false });
        } catch {
            return `$${blocks[i]}$`;
        }
    });

    return html;
}

/**
 * 处理容器中的 Mermaid 代码块
 * @param {HTMLElement} container
 */
export function processMermaid(container) {
    if (typeof mermaid === 'undefined') return;
    const codeBlocks = container.querySelectorAll('code.language-mermaid');
    codeBlocks.forEach((block) => {
        const pre = block.parentElement;
        const div = document.createElement('div');
        div.className = 'mermaid';
        div.textContent = block.textContent;
        pre.replaceWith(div);
    });
    try {
        mermaid.run({ querySelector: '.mermaid' });
    } catch { /* Mermaid 渲染失败静默处理 */ }
}

// 暴露全局复制函数，供内联 onclick 调用
window.__copyCode = function (btn) {
    const code = btn.previousElementSibling;
    navigator.clipboard.writeText(code.textContent).then(() => {
        btn.textContent = '已复制';
        btn.classList.add('copied');
        setTimeout(() => {
            btn.textContent = '复制';
            btn.classList.remove('copied');
        }, 2000);
    });
};
