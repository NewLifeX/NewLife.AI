import { create } from 'zustand'
import type { Artifact } from '@/types'

interface ArtifactState {
  /** 当前预览的 Artifact，null 表示面板关闭 */
  current: Artifact | null
  /** 是否正在流式接收 */
  isStreaming: boolean
  /** 打开 Artifact 预览面板 */
  open: (artifact: Artifact) => void
  /** 关闭面板 */
  close: () => void
  /** 开始流式 Artifact */
  startStreaming: (language: string, title?: string) => void
  /** 追加代码内容 */
  appendCode: (content: string) => void
  /** 结束流式 Artifact */
  endStreaming: () => void
}

export const useArtifactStore = create<ArtifactState>((set, get) => ({
  current: null,
  isStreaming: false,
  open: (artifact) => set({ current: artifact }),
  close: () => set({ current: null, isStreaming: false }),
  startStreaming: (language, title) => set({ current: { language, code: '', title }, isStreaming: true }),
  appendCode: (content) => {
    const cur = get().current
    if (cur) set({ current: { ...cur, code: cur.code + content } })
  },
  endStreaming: () => set({ isStreaming: false }),
}))
