/**
 * 结果卡主题配色。文档/表格/幻灯片下载卡的头部样式按卡片风格 Key 解析。
 */

export interface OfficeThemeColors {
  bg: string
  accent: string
  text: string
}

/** 内置主题色表（与后端 ThemeColors 对应，供头部样式） */
const COLORS: Record<string, OfficeThemeColors> = {
  blue:      { bg: '#1E40AF', accent: '#2563EB', text: '#FFFFFF' },
  dark:      { bg: '#0F172A', accent: '#6366F1', text: '#F1F5F9' },
  corporate: { bg: '#374151', accent: '#1F2937', text: '#FFFFFF' },
  warm:      { bg: '#C2410C', accent: '#EA580C', text: '#FFFFFF' },
  green:     { bg: '#15803D', accent: '#16A34A', text: '#FFFFFF' },
  minimal:   { bg: '#18181B', accent: '#71717A', text: '#FAFAFA' },
  ocean:     { bg: '#0C4A6E', accent: '#0EA5E9', text: '#FFFFFF' },
  sunset:    { bg: '#1E1B4B', accent: '#F97316', text: '#FFFFFF' },
  forest:    { bg: '#064E3B', accent: '#059669', text: '#FFFFFF' },
  slate:     { bg: '#0F172A', accent: '#64748B', text: '#F8FAFC' },
  amber:     { bg: '#451A03', accent: '#F59E0B', text: '#FFFFFF' },
}

/**
 * 卡片风格 Key → 内置色组别名。
 * 卡片风格的 28 套风格（25 精选 + 3 幻灯片专用）按色系归组，避免为每套重复维护颜色。
 */
const ALIASES: Record<string, string> = {
  'geist-minimal': 'minimal',
  'linear-pro': 'dark',
  'finance-bloom': 'blue',
  'enterprise-blue': 'blue',
  'gov-authority': 'corporate',
  'logistics-pro': 'slate',
  'raycast-dark': 'dark',
  'deep-space': 'dark',
  'editorial-ink': 'dark',
  'medical-pure': 'minimal',
  'nordic-sage': 'green',
  'organic-eco': 'forest',
  'iot-green': 'green',
  'stripe-vibrant': 'warm',
  'sunset-blur': 'sunset',
  'internet-orange': 'warm',
  'food-warm': 'warm',
  'ai-neural': 'ocean',
  'industrial-raw': 'corporate',
  'autonomous-cyan': 'ocean',
  'vision-glass': 'minimal',
  'flat-minimal': 'minimal',
  'chinese-red': 'warm',
  'awwwards-luxury': 'amber',
  'retro-future': 'dark',
  'ppt-executive': 'blue',
  'ppt-product': 'dark',
  'ppt-data': 'dark',
}

/**
 * 解析结果卡主题配色：优先精确 Key，其次色组别名，未知时回退蓝色。
 * @param key 卡片风格 Key 或内置主题名
 */
export function getThemeColors(key?: string): OfficeThemeColors {
  if (!key) return COLORS.blue
  return COLORS[key] ?? COLORS[ALIASES[key]] ?? COLORS.blue
}
