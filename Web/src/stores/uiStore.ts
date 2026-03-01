import { create } from 'zustand'

interface UIState {
  sidebarCollapsed: boolean
  settingsOpen: boolean
  skillPopoverOpen: boolean

  toggleSidebar: () => void
  setSidebarCollapsed: (v: boolean) => void
  openSettings: () => void
  closeSettings: () => void
  setSkillPopoverOpen: (v: boolean) => void
}

export const useUIStore = create<UIState>((set) => ({
  sidebarCollapsed: false,
  settingsOpen: false,
  skillPopoverOpen: false,

  toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
  setSidebarCollapsed: (v) => set({ sidebarCollapsed: v }),
  openSettings: () => set({ settingsOpen: true }),
  closeSettings: () => set({ settingsOpen: false }),
  setSkillPopoverOpen: (v) => set({ skillPopoverOpen: v }),
}))
