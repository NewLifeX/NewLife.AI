import mermaid from 'mermaid'

/**
 * LLM 常把 Redis Key、占位符、模板变量写进 Mermaid 节点标签，例如：
 *   D[device:{id}:latest]
 * Mermaid 会把其中的 `{` 误判为菱形节点起始符，导致 parse 失败。
 *
 * 这里只在“已进入标签文本上下文”时转义花括号：
 * - 方括号节点标签 [...]
 * - 圆括号节点标签 (...)
 * - 边标签 |...|
 * - 引号标签 "..."
 *
 * 不处理顶层的 `{...}`，避免破坏合法的菱形节点语法。
 */
export function normalizeMermaidCode(code: string): string {
  const stack: string[] = []
  let result = ''

  for (let i = 0; i < code.length; i++) {
    const ch = code[i]
    const top = stack[stack.length - 1]

    if (ch === '|' || ch === '"') {
      if (top === ch) stack.pop()
      else stack.push(ch)
      result += ch
      continue
    }

    if (ch === '[' || ch === '(') {
      stack.push(ch)
      result += ch
      continue
    }

    if (ch === ']' && top === '[') {
      stack.pop()
      result += ch
      continue
    }

    if (ch === ')' && top === '(') {
      stack.pop()
      result += ch
      continue
    }

    if ((ch === '{' || ch === '}') && stack.length > 0) {
      result += ch === '{' ? '&#123;' : '&#125;'
      continue
    }

    result += ch
  }

  return result
}

/** 先尝试原始代码，再尝试规范化后的代码，返回可安全渲染的版本。 */
export async function resolveRenderableMermaidCode(code: string): Promise<string | null> {
  const candidates = [code]
  const normalized = normalizeMermaidCode(code)
  if (normalized !== code) candidates.push(normalized)

  for (const candidate of candidates) {
    const parsed = await mermaid.parse(candidate, { suppressErrors: true })
    if (parsed) return candidate
  }

  return null
}
