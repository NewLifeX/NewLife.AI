import { create } from 'zustand'
import type { Artifact } from '@/types'

interface ArtifactState {
  /** 当前预览的 Artifact，null 表示面板关闭 */
  current: Artifact | null
  /** 打开 Artifact 预览面板 */
  open: (artifact: Artifact) => void
  /** 关闭面板 */
  close: () => void
}

export const useArtifactStore = create<ArtifactState>((set) => ({
  current: null,
  open: (artifact) => set({ current: artifact }),
  close: () => set({ current: null }),
}))
