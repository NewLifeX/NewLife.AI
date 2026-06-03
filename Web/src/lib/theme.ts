/**
 * 品牌色动态注入：将 SystemConfig.themeColor / brandGradient 写入 CSS 变量。
 * 仅作用于 :root，会覆盖 styles/index.css 中的默认 --color-brand-* / --gradient-brand。
 */

/** 解析 #rrggbb / #rgb，返回 [r, g, b]。失败返回 null */
function parseHex(hex: string): [number, number, number] | null {
  const m = hex.trim().match(/^#?([0-9a-f]{3}|[0-9a-f]{6})$/i)
  if (!m) return null
  let h = m[1]
  if (h.length === 3) h = h.split('').map((c) => c + c).join('')
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]
}

function toHex(rgb: [number, number, number]): string {
  return '#' + rgb.map((v) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('')
}

/** 与白色按比例混合（0=原色，1=白） */
function mixWhite(rgb: [number, number, number], t: number): [number, number, number] {
  return [rgb[0] + (255 - rgb[0]) * t, rgb[1] + (255 - rgb[1]) * t, rgb[2] + (255 - rgb[2]) * t]
}

/** 与黑色按比例混合 */
function mixBlack(rgb: [number, number, number], t: number): [number, number, number] {
  return [rgb[0] * (1 - t), rgb[1] * (1 - t), rgb[2] * (1 - t)]
}

/**
 * 应用品牌色到 CSS 变量。
 * @param themeColor 主色 HEX，如 #5B5BFF
 * @param brandGradient 渐变两端 HEX，逗号分隔，如 "#5B5BFF,#8B5CF6"
 */
export function applyBrandTheme(themeColor?: string | null, brandGradient?: string | null): void {
  const root = document.documentElement
  const base = themeColor ? parseHex(themeColor) : null
  if (base) {
    root.style.setProperty('--color-brand-50', toHex(mixWhite(base, 0.92)))
    root.style.setProperty('--color-brand-100', toHex(mixWhite(base, 0.84)))
    root.style.setProperty('--color-brand-200', toHex(mixWhite(base, 0.68)))
    root.style.setProperty('--color-brand-300', toHex(mixWhite(base, 0.5)))
    root.style.setProperty('--color-brand-400', toHex(mixWhite(base, 0.25)))
    root.style.setProperty('--color-brand-500', toHex(base))
    root.style.setProperty('--color-brand-600', toHex(mixBlack(base, 0.12)))
    root.style.setProperty('--color-brand-700', toHex(mixBlack(base, 0.28)))
    root.style.setProperty('--color-brand-800', toHex(mixBlack(base, 0.42)))
    root.style.setProperty('--color-brand-900', toHex(mixBlack(base, 0.55)))
    // 焦点环 / glow 透明度
    const [r, g, b] = base
    root.style.setProperty('--shadow-brand-glow', `0 8px 24px -8px rgba(${r}, ${g}, ${b}, 0.45), 0 2px 6px rgba(${r}, ${g}, ${b}, 0.18)`)
    root.style.setProperty('--shadow-glow', `0 0 0 4px rgba(${r}, ${g}, ${b}, 0.18)`)
  }

  if (brandGradient) {
    const parts = brandGradient.split(',').map((s) => s.trim()).filter(Boolean)
    if (parts.length >= 2 && parseHex(parts[0]) && parseHex(parts[1])) {
      root.style.setProperty('--gradient-brand', `linear-gradient(135deg, ${parts[0]} 0%, ${parts[1]} 100%)`)
    }
  }
}
