/**
 * 页面标题与 favicon 工具函数
 *
 * 仅保留 document.title 更新和 favicon 同步，已移除全部 OG/Twitter meta 标签操作。
 */

/** 更新 document.title，可选追加站点名后缀 */
export function setPageTitle(title: string, siteTitle?: string): void {
  document.title = siteTitle ? `${title} - ${siteTitle}` : title
}

/** 更新 favicon（浏览器标签页图标），仅当 imageUrl 是 svg 或 ico 时 */
export function setPageImage(imageUrl: string): void {
  if (!imageUrl) return

  const faviconLink = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (faviconLink && (imageUrl.endsWith('.svg') || imageUrl.endsWith('.ico'))) {
    faviconLink.href = imageUrl
  }
}
