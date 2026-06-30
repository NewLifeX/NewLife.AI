/**
 * 系统设置懒加载缓存
 *
 * 按需从服务端拉取系统设置（如 allowedExtensions），
 * 避免在页面初始化时加载不必要的配置。
 */
import { fetchSystemSettings } from '@/lib/api'

/** 缓存的文件扩展名字符串（逗号分隔，如 ".jpg,.png"），null 表示尚未加载 */
let _allowedExtensions: string | null = null

/** 加载中的 Promise，避免并发重复请求 */
let _loadingPromise: Promise<string> | null = null

/**
 * 确保 allowedExtensions 已加载，返回逗号分隔的扩展名字符串
 *
 * 首次调用会发 GET /api/system/settings 请求，后续直接返回缓存
 */
export async function ensureAllowedExtensions(): Promise<string> {
  if (_allowedExtensions !== null) return _allowedExtensions

  // 已有加载中的请求则复用
  if (_loadingPromise) return _loadingPromise

  _loadingPromise = (async () => {
    try {
      const settings = await fetchSystemSettings()
      _allowedExtensions = settings.allowedExtensions ?? ''
    } catch {
      // 加载失败时返回空字符串（等同于"全部允许"）
      _allowedExtensions = ''
    } finally {
      _loadingPromise = null
    }
    return _allowedExtensions!
  })()

  return _loadingPromise
}

/**
 * 清空缓存（系统设置修改后可调用）
 */
export function clearSystemSettingsCache(): void {
  _allowedExtensions = null
  _loadingPromise = null
}

/**
 * 判断文件扩展名是否在允许列表中
 *
 * @param fileName 文件名（或完整路径）
 * @param fileType 文件 MIME type（可选，用于无扩展名时的回退判断）
 * @returns true 表示允许上传
 *
 * 行为：
 * - 允许列表为空字符串时返回 true（全部允许），与后端一致
 * - 对无扩展名的文件（如剪贴板截图 name='image'），检查 MIME type
 *   是否属于 image/*，再判断常见图片扩展名是否在列表中
 */
export async function isExtensionAllowed(fileName: string, fileType?: string): Promise<boolean> {
  const allowedStr = await ensureAllowedExtensions()

  // 允许列表为空则全部允许
  if (!allowedStr) return true

  const allowed = allowedStr.split(',').map(s => s.trim().toLowerCase()).filter(Boolean)
  if (allowed.length === 0) return true

  // 提取文件扩展名
  const dotIndex = fileName.lastIndexOf('.')
  let ext = dotIndex >= 0 ? fileName.slice(dotIndex).toLowerCase() : ''

  // 有扩展名则直接匹配
  if (ext) return allowed.includes(ext)

  // 没有扩展名（如剪贴板截图），回退检查 MIME type
  if (fileType) {
    if (fileType.startsWith('image/')) {
      // 检查常见图片扩展名是否在列表中
      const imgExts = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp', '.tiff', '.tif']
      return imgExts.some(e => allowed.includes(e))
    }
    if (fileType.startsWith('audio/')) {
      const audioExts = ['.mp3', '.wav', '.ogg', '.aac', '.webm', '.flac']
      return audioExts.some(e => allowed.includes(e))
    }
    if (fileType.startsWith('video/')) {
      const videoExts = ['.mp4', '.webm', '.avi', '.mov', '.mkv']
      return videoExts.some(e => allowed.includes(e))
    }
    // 根据 MIME 转扩展名
    const mimeToExt: Record<string, string> = {
      'application/pdf': '.pdf',
      'application/msword': '.doc',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document': '.docx',
      'application/vnd.ms-excel': '.xls',
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': '.xlsx',
      'application/vnd.ms-powerpoint': '.ppt',
      'application/vnd.openxmlformats-officedocument.presentationml.presentation': '.pptx',
      'text/plain': '.txt',
      'text/markdown': '.md',
      'text/csv': '.csv',
    }
    const mappedExt = mimeToExt[fileType]
    if (mappedExt) return allowed.includes(mappedExt)
  }

  // 无法判断时拒绝（避免漏网）
  return false
}

/**
 * 获取允许的扩展名数组（带点，小写）
 *
 * 用于设置 `<input accept>` 属性
 */
export async function getAllowedExtensionsArray(): Promise<string[]> {
  const allowedStr = await ensureAllowedExtensions()
  if (!allowedStr) return []

  return [...new Set(
    allowedStr.split(',').map(s => s.trim().toLowerCase()).filter(Boolean)
  )]
}
