/**
 * 图片导出工具。
 *
 * - savePngBlob        将 Blob 触发浏览器下载（另存为图片）
 * - captureDomAsPng    把任意 DOM 元素截成 PNG Blob（使用 html-to-image）
 * - copyImageOrFallback 尝试写剪贴板，不支持时返回 false 让调用方展示移动端 fallback 对话框
 */

import { toPng } from 'html-to-image'

/**
 * 触发 PNG 文件下载（另存为图片）。
 * @param blob     PNG Blob
 * @param filename 建议文件名，默认 image.png
 */
export function savePngBlob(blob: Blob, filename = 'image.png'): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 0)
}

/**
 * 将 DOM 元素截成 PNG Blob（使用 html-to-image）。
 * @param el    要截图的元素
 * @param scale 像素倍率，默认 2（高清）
 */
export async function captureDomAsPng(el: HTMLElement, scale = 2): Promise<Blob> {
  const dataUrl = await toPng(el, {
    pixelRatio: scale,
    cacheBust: false,
    filter: (node: Node) => !(node instanceof HTMLElement && 'noCapture' in node.dataset),
  })
  const res = await fetch(dataUrl)
  return res.blob()
}

/**
 * 尝试将 PNG Blob 写入系统剪贴板。
 * - 桌面 Chrome/Edge/Firefox（HTTPS）：通常成功，返回 true
 * - 微信/企业微信内置浏览器等：不支持 ClipboardItem，返回 false
 *
 * 调用方在返回 false 时应展示移动端降级对话框（长按图片保存）。
 */
export async function copyImageOrFallback(blob: Blob): Promise<Boolean> {
  try {
    if (
      typeof ClipboardItem === 'undefined' ||
      typeof navigator.clipboard?.write !== 'function'
    ) {
      return false
    }
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })])
    return true
  } catch {
    return false
  }
}
