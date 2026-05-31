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
 * 将节点标签和边标签内的字面量 \n（反斜杠+n）转换为 <br/>。
 * Mermaid 在被引号包裹的标签（["..."]）及圆形节点（((...))）中，
 * \n 不会自动换行，统一替换为 <br/>，与默认的 htmlLabels 配合产生真正的换行。
 * 覆盖：双圆括号 ((  ))、圆括号 (  )、引号方括号 ["  "]、方括号 [  ]、边标签 |  |。
 */
function fixNewlinesInLabels(code: string): string {
  const br = '<br/>'
  return code
    .replace(/\(\(([^()]*)\)\)/g, (_, c) => `((${c.replace(/\\n/g, br)}))`)
    .replace(/\(([^()]*)\)/g, (_, c) => `(${c.replace(/\\n/g, br)})`)
    .replace(/\["([^"]*)"\]/g, (_, c) => `["${c.replace(/\\n/g, br)}"]`)
    .replace(/\[([^\[\]"]*)\]/g, (_, c) => `[${c.replace(/\\n/g, br)}]`)
    .replace(/\|([^|]*)\|/g, (_, c) => `|${c.replace(/\\n/g, br)}|`)
}

/**
 * 修复 LLM 将 <br/> 及追加文本写在节点标签括号之外的问题。
 * LLM 有时生成 `NodeId[label]<br/>extra_text:::class`，
 * `<br/>` 及其后的文字处于 `[...]` 括号外，被 Mermaid 解析器视为非法 token，
 * 导致整行 parse 失败。统一收纳回括号内：`NodeId["label<br/>extra_text"]:::class`。
 */
function fixBrAfterNodeLabel(code: string): string {
  return code.replace(
    /\[([^\[\]"]+)\](<br\/>)([^:\n\[\]{}|]*)(:::[\w]+)?/g,
    (_m, label, br, extra, cls) => `["${label}${br}${extra.trimEnd()}"]${cls ?? ''}`,
  )
}

/**
 * 修复 LLM 在方括号节点标签内嵌入双引号的问题。
 * LLM 有时生成 `NodeId[选择"忘记密码"]`，双引号未在最外层（即非 `["..."]` 格式），
 * Mermaid 解析器会把 `"` 解析为 STR token 开始，导致剩余内容报 parse 错误。
 * 将标签内的 `"` 替换为 `'`：`NodeId[选择'忘记密码']`。
 */
function fixQuotesInBracketLabels(code: string): string {
  // 匹配未以 " 开头的方括号标签（已用外层引号包裹的不处理）
  return code.replace(
    /\[([^"\[\]\n{}<>|][^\[\]\n{}<>]*)\]/g,
    (m, label) => (label.includes('"') ? `[${label.replace(/"/g, "'")}]` : m),
  )
}

/**
 * LLM 常把 Redis Key、占位符、模板变量写进 Mermaid 节点标签，例如：
 *   D[device:{id}:latest]
 * Mermaid 会把其中的 `{` 误判为菱形节点起始符，导致 parse 失败。
 *
 * 这里只在"已进入标签文本上下文"时转义花括号：
 * - 方括号节点标签 [...]
 * - 圆括号节点标签 (...)
 * - 边标签 |...|
 * - 引号标签 "..."
 *
 * 不处理顶层的 `{...}`，避免破坏合法的菱形节点语法。
 */
export function normalizeMermaidCode(code: string): string {
  // 先做正则预处理（顺序：dasharray → pipe in labels → newlines → br修复 → 引号修复 → 字符扫描）
  code = fixClassDefStrokeDasharray(code)
  code = fixPipesInNodeLabels(code)
  code = fixNewlinesInLabels(code)
  code = fixBrAfterNodeLabel(code)
  code = fixQuotesInBracketLabels(code)

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
