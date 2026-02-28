import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import i18n from '@/i18n'
import type { UserSettings } from '@/types'

interface SettingsState extends UserSettings {
  update: (partial: Partial<UserSettings>) => void
  reset: () => void
}

const defaults: UserSettings = {
  theme: 'system',
  language: 'zh',
  fontSize: 16,
  mcpEnabled: true,
  defaultSkill: 'general',
  streamingSpeed: 3,
}

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      ...defaults,

      update: (partial) => {
        if (partial.language) {
          i18n.changeLanguage(partial.language)
        }
        set((s) => ({ ...s, ...partial }))
      },

      reset: () => {
        i18n.changeLanguage(defaults.language)
        set(defaults)
      },
    }),
    {
      name: 'newlife-settings',
      onRehydrateStorage: () => (state) => {
        if (state?.language) {
          i18n.changeLanguage(state.language)
        }
      },
    },
  ),
)
