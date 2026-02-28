/**
 * NewLife.ChatAI — 全屏图片预览组件
 * 支持缩放、键盘导航、缩略图
 */

import { $ } from '../utils.js';
import { getState, setState } from '../store.js';

/**
 * 初始化图片预览组件
 */
export function initLightbox() {
    const lightbox = $('lightbox');
    const closeBtn = $('lightboxClose');
    const prevBtn = $('lightboxPrev');
    const nextBtn = $('lightboxNext');
    const backdrop = lightbox?.querySelector('.lightbox-backdrop');

    if (!lightbox) return;

    closeBtn?.addEventListener('click', closeLightbox);
    backdrop?.addEventListener('click', closeLightbox);

    prevBtn?.addEventListener('click', () => navigateLightbox(-1));
    nextBtn?.addEventListener('click', () => navigateLightbox(1));

    // 键盘导航
    document.addEventListener('keydown', (e) => {
        if (lightbox.classList.contains('hidden')) return;
        if (e.key === 'Escape') closeLightbox();
        if (e.key === 'ArrowLeft') navigateLightbox(-1);
        if (e.key === 'ArrowRight') navigateLightbox(1);
    });

    // 暴露全局函数供内联调用
    window.__openLightbox = openLightbox;
}

/**
 * 打开图片预览
 * @param {string} src - 图片 URL
 */
export function openLightbox(src) {
    const messagesInner = $('messagesInner');
    const imgs = messagesInner?.querySelectorAll('.md-content img') || [];
    const images = [...imgs].map(img => img.src);

    let index = images.indexOf(src);
    if (index < 0) {
        images.push(src);
        index = images.length - 1;
    }

    setState({ lightboxImages: images, lightboxIndex: index });
    _renderLightbox();
    $('lightbox')?.classList.remove('hidden');
}

/**
 * 关闭图片预览
 */
export function closeLightbox() {
    $('lightbox')?.classList.add('hidden');
}

/**
 * 导航到上/下一张
 * @param {number} dir - -1 上一张, 1 下一张
 */
function navigateLightbox(dir) {
    const { lightboxImages, lightboxIndex } = getState();
    if (lightboxImages.length <= 1) return;
    const newIndex = (lightboxIndex + dir + lightboxImages.length) % lightboxImages.length;
    setState({ lightboxIndex: newIndex });
    _renderLightbox();
}

/** 渲染预览内容 */
function _renderLightbox() {
    const { lightboxImages, lightboxIndex } = getState();
    const img = $('lightboxImg');
    const thumbsEl = $('lightboxThumbs');

    if (img) img.src = lightboxImages[lightboxIndex];

    // 缩略图
    if (thumbsEl) {
        thumbsEl.innerHTML = '';
        if (lightboxImages.length > 1) {
            lightboxImages.forEach((src, i) => {
                const thumb = document.createElement('img');
                thumb.src = src;
                thumb.className = i === lightboxIndex ? 'active' : '';
                thumb.addEventListener('click', () => {
                    setState({ lightboxIndex: i });
                    _renderLightbox();
                });
                thumbsEl.appendChild(thumb);
            });
        }
    }

    // 导航按钮可见性
    const hasMultiple = lightboxImages.length > 1;
    $('lightboxPrev')?.classList.toggle('hidden', !hasMultiple);
    $('lightboxNext')?.classList.toggle('hidden', !hasMultiple);
}
