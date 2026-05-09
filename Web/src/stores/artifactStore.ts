import { create } from 'zustand'
import type { Artifact } from '@/types'

interface ArtifactState {
  /** 当前预览的 Artifact，null 表示面板关闭 */
  current: Artifact | null
  /** 是否正在流式接收 */
  isStreaming: boolean
  /** Artifact 来源：手动打开 / 显式流式事件 / 正文自动提取 */
  source: 'manual' | 'stream' | 'auto' | null
  /** 打开 Artifact 预览面板 */
  open: (artifact: Artifact) => void
  /** 自动打开 Artifact 预览（仅用于正文代码块自动提取） */
  openAuto: (artifact: Artifact) => void
  /** 关闭自动提取的 Artifact，手动/流式来源不受影响 */
  clearAuto: () => void
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
  source: null,
  open: (artifact) => set({ current: artifact, isStreaming: false, source: 'manual' }),
  openAuto: (artifact) => set((s) =>
    s.source === 'manual' || s.source === 'stream'
      ? s
      : { current: artifact, isStreaming: false, source: 'auto' },
  ),
  clearAuto: () => set((s) => (s.source === 'auto' ? { current: null, isStreaming: false, source: null } : s)),
  close: () => set({ current: null, isStreaming: false, source: null }),
  startStreaming: (language, title) => set({ current: { language, code: '', title }, isStreaming: true, source: 'stream' }),
  appendCode: (content) => {
    const cur = get().current
    if (cur) set({ current: { ...cur, code: cur.code + content } })
  },
  endStreaming: () => set({ isStreaming: false }),
}))
