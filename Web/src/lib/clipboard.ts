/**
 * 从剪贴板数据中提取图片文件。
 * 供 ChatInput 粘贴处理使用，也方便独立测试。
 */
export function extractImagesFromClipboard(clipboardData: DataTransfer | null): File[] {
  if (!clipboardData) return []

  const images: File[] = []

  // 优先读 items（含 MIME type）
  const items = clipboardData.items
  if (items) {
    for (let i = 0; i < items.length; i++) {
      const item = items[i]
      if (item.type.startsWith('image/')) {
        const file = item.getAsFile()
        if (file) images.push(file)
      }
    }
  }

  // 兜底读 files（某些浏览器/截图工具走此路径）
  if (images.length === 0 && clipboardData.files) {
    for (let i = 0; i < clipboardData.files.length; i++) {
      const file = clipboardData.files[i]
      if (file.type.startsWith('image/')) {
        images.push(file)
      }
    }
  }

  return images
}

/**
 * 从拖拽数据中提取文件列表。
 */
export function extractFilesFromDrop(dataTransfer: DataTransfer | null): File[] {
  if (!dataTransfer?.files) return []
  return Array.from(dataTransfer.files)
}
