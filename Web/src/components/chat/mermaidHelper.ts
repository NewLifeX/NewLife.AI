import mermaid from 'mermaid'

/**
 * 修复 classDef 中 stroke-dasharray 值含空格的问题。
 * LLM 常生成 `stroke-dasharray: 5 5`，Mermaid 解析器以空格分割属性 token，
 * 导致 `5 5` 被拆为两个无效 token，整个 classDef 块失效。
 * 保留第一个数值，后续空格分隔的数值一并移除。
 */
function fixClassDefStrokeDasharray(code: string): string {
  return code.replace(
    /(stroke-dasharray)\s*:\s*(\d+(?:\.\d+)?)(?:\s+[\d.]+)+/g,
    '$1:$2',
  )
}

/**
 * 修复节点标签 [...] 中含有 `|` 的情况。
 * `|` 是 Mermaid 边标签的保留分隔符，在未加引号的方括号标签内会触发 Parse error：
 *   got 'PIPE'
 * 自动将含 `|` 的 [...] 标签包裹为 ["..."]。
 * 已有引号的标签（["..."]）不受影响。
 */
function fixPipesInNodeLabels(code: string): string {
  return code.replace(/\[([^\[\]"]*\|[^\[\]"]*)\]/g, (_, content) => `["${content}"]`)
}

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
  // 先做正则预处理（顺序：dasharray → pipe in labels → 字符扫描）
  code = fixClassDefStrokeDasharray(code)
  code = fixPipesInNodeLabels(code)

  // 字符级扫描：转义标签上下文内的 {}
  // 引号上下文（inQuoted）内，| [ ( ] ) 均为字面量，不做上下文切换
  const stack: string[] = []
  let result = ''

  for (let i = 0; i < code.length; i++) {
    const ch = code[i]
    const top = stack[stack.length - 1]
    const inQuoted = top === '"'

    // 引号：开/关引号上下文
    if (ch === '"') {
      if (top === '"') stack.pop()
      else stack.push('"')
      result += ch
      continue
    }

    // 引号内：所有字符均为字面量，仅转义 {}
    if (inQuoted) {
      result += ch === '{' ? '&#123;' : ch === '}' ? '&#125;' : ch
      continue
    }

    // 非引号上下文：正常处理边标签 | 和括号上下文
    if (ch === '|') {
      if (top === '|') stack.pop()
      else stack.push('|')
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
