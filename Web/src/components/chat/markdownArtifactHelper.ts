import type { Artifact } from '@/types'

const PREVIEWABLE = new Set(['html', 'svg', 'mermaid'])

/** 从 Markdown 中提取第一个可预览代码块，作为自动 Artifact 预览候选。 */
export function extractAutoArtifact(content: string): Artifact | null {
  const regex = /```([a-zA-Z0-9_-]+)[^\n]*\n([\s\S]*?)```/g
  let match: RegExpExecArray | null

  while ((match = regex.exec(content)) !== null) {
    const language = match[1].toLowerCase()
    if (!PREVIEWABLE.has(language)) continue

    const code = match[2].replace(/\n$/, '')
    const prefix = content.slice(0, match.index)
    const lines = prefix.split(/\r?\n/).map(line => line.trim()).filter(Boolean)
    const title = lines.at(-1)?.replace(/^#+\s*/, '') || language.toUpperCase()

    return { language, code, title }
  }

  return null
}
