/**
 * 动态更新 OG meta 标签和 favicon 的工具函数
 *
 * SPA 模式下 IM 爬虫看不到 JS 动态更新的内容（靠后端中间件注入），
 * 但 JS 友好的客户端（如 iOS/Android 应用内浏览器）能读到实时值，
 * 同时保证前端在标题切换时浏览器标签页和预览卡片立即更新。
 */

/** 更新 og:title、twitter:title 及 document.title */
export function setPageTitle(title: string, siteTitle?: string): void {
  const fullTitle = siteTitle ? `${title} - ${siteTitle}` : title
  document.title = fullTitle

  setMeta('og:title', fullTitle)
  setMeta('twitter:title', fullTitle)
  setMeta('og:description', `${fullTitle} - 智能AI对话助手`)
  setMeta('twitter:description', `${fullTitle} - 智能AI对话助手`)
}

/** 更新 og:image、twitter:image 及 favicon */
export function setPageImage(imageUrl: string): void {
  if (!imageUrl) return

  setMeta('og:image', imageUrl)
  setMeta('twitter:image', imageUrl)

  // 同步更新 favicon（浏览器标签页图标），仅当 imageUrl 是 svg 或 ico 时
  const faviconLink = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (faviconLink && (imageUrl.endsWith('.svg') || imageUrl.endsWith('.ico'))) {
    faviconLink.href = imageUrl
  }
}

/** 更新 og:url */
export function setPageUrl(url: string): void {
  setMeta('og:url', url)
}

/** 设置单个 meta 标签的值（存在则更新 content，不存在则创建） */
function setMeta(property: string, content: string): void {
  // 尝试按 property 属性查找
  let el = document.querySelector(`meta[property="${property}"]`)
  if (!el) {
    // 尝试按 name 属性查找（如 twitter:card）
    el = document.querySelector(`meta[name="${property}"]`)
  }
  if (el) {
    el.setAttribute('content', content)
  } else {
    // 不存在时创建
    const meta = document.createElement('meta')
    // og: 开头的用 property，twitter: 开头的用 name
    if (property.startsWith('og:')) {
      meta.setAttribute('property', property)
    } else {
      meta.setAttribute('name', property)
    }
    meta.setAttribute('content', content)
    document.head.appendChild(meta)
  }
}
