import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import i18n from '@/i18n'
import type { UserSettings } from '@/types'
import { fetchUserSettings, saveUserSettings } from '@/lib/api'

interface SettingsState extends UserSettings {
  _loaded: boolean
  loadFromServer: () => Promise<void>
  update: (partial: Partial<UserSettings>) => void
  reset: () => void
}

const defaults: UserSettings = {
  theme: 'system',
  language: 'zh',
  fontSize: 16,
  sendShortcut: 'Enter',
  defaultModel: 'qwen-max',
  defaultThinkingMode: 0,
  contextRounds: 10,
  systemPrompt: '',
  mcpEnabled: true,
  defaultSkill: 'general',
  streamingSpeed: 3,
}

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set, get) => ({
      ...defaults,
      _loaded: false,

      loadFromServer: async () => {
        if (get()._loaded) return
        try {
          const remote = await fetchUserSettings()
          if (remote.language) i18n.changeLanguage(remote.language)
          set({ ...remote, _loaded: true })
        } catch {
          set({ _loaded: true })
        }
      },

      update: (partial) => {
        if (partial.language) {
          i18n.changeLanguage(partial.language)
        }
        set((s) => ({ ...s, ...partial }))
        // 异步保存到后端
        const merged = { ...get(), ...partial }
        saveUserSettings(merged).catch(() => {})
      },

      reset: () => {
        i18n.changeLanguage(defaults.language)
        set(defaults)
        saveUserSettings(defaults).catch(() => {})
      },
    }),
    {
      name: 'newlife-settings',
      partialize: (state) => {
        const { _loaded, loadFromServer, update, reset, ...rest } = state
        return rest
      },
      onRehydrateStorage: () => (state) => {
        if (state?.language) {
          i18n.changeLanguage(state.language)
        }
      },
    },
  ),
)
