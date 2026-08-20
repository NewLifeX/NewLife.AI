/**
 * HTML Widget 卡片截图导出工具（外部截图架构）。
 *
 * 架构：卡片 HTML 渲染在隐藏的同源 iframe 中，由父页面直接读取 iframe DOM 截图。
 * 截图 iframe 必须同时具备 allow-scripts + allow-same-origin，这必然触发 Chrome 的
 * "sandbox escape" 控制台告警——属于本架构的固有代价：
 *   - 风险有界：仅截图那 1~3 秒内卡片脚本持有同源权限；后端 WidgetToolService 已对
 *     卡片做过体积与脚本来源白名单校验；展示用 iframe 仍是更安全的 allow-scripts 隔离。
 *   - 降噪：复用同一个常驻 iframe，并改用 document.write 写入卡片 HTML（而非 src 导航）。
 *     导航会让 Chrome 对 allow-scripts+allow-same-origin 组合每次重新打印告警；
 *     document.write 不触发导航，告警只在 iframe 首次创建时出现一次。
 *     请勿改为"iframe 内自截 + postMessage"——那正是历史上因协议脆弱、易受扩展干扰而放弃的方案。
 *
 * - 主引擎 html-to-image：SVG foreignObject 交给浏览器原生布局引擎绘制，文字位置
 *   与屏幕所见一致；html2canvas 是"重实现 CSS 布局"，对 line-height/flex 居中文本
 *   有逐字偏移问题，仅作失败兜底。
 * - 字体：等待 document.fonts.ready 后 getFontEmbedCSS 内嵌 webfont（foreignObject
 *   以 <img> 渲染是全新上下文，必须内嵌字体，否则静默回退导致偏移——历史踩坑点）。
 * - 空白裁剪：全屏等宽视口下卡片可能不延伸占满。先按 DOM 内容包围盒裁剪（保留卡片
 *   容器与自身留白）；若根容器为 width:100% 导致 DOM 占满（内容实际未延伸），再用
 *   像素扫描兜底裁掉右侧空白（见 computeCropBox）。
 */

import { getFontEmbedCSS, toPng } from 'html-to-image'

export interface HtmlCaptureOptions {
  /** 目标宽度（CSS 像素）。通常为展示 iframe 当前宽度；超出内容宽度时会被裁剪 */
  width: number
  /** 透明背景。true 时输出带 Alpha 通道的 PNG */
  transparent?: boolean
  /** 像素倍率，默认 2（高清） */
  scale?: number
}

type H2CFunc = (el: HTMLElement, opts?: object) => Promise<HTMLCanvasElement>
let _h2cLoader: Promise<H2CFunc> | null = null

/** 懒加载 html2canvas 模块（降级兜底用），首次需要时才拉取代码分片。 */
function lazyHtml2Canvas(): Promise<H2CFunc> {
  _h2cLoader ??= import('html2canvas').then(m => m.default as H2CFunc)
  return _h2cLoader
}

// ── 常驻截图 iframe（懒创建、复用；document.write 写入，避免重复 sandbox 告警）──
// 见文件头注释：allow-scripts + allow-same-origin 必然触发 Chrome sandbox 逃逸告警，
// 复用 iframe + document.write 让告警只在首次创建时出现一次，而非每次复制都打印。
let _captureIframe: HTMLIFrameElement | null = null

/** 创建并返回常驻隐藏截图 iframe（懒创建、复用）。 */
function getCaptureIframe(): HTMLIFrameElement {
  if (!_captureIframe) {
    const iframe = document.createElement('iframe')
    // blob/about:blank 与父页面同源，allow-same-origin 让父页面直接访问 iframe DOM
    iframe.setAttribute('sandbox', 'allow-scripts allow-same-origin')
    iframe.style.cssText = 'position:fixed;left:-9999px;top:-9999px;width:900px;height:600px;visibility:hidden;pointer-events:none;border:none;'
    document.body.appendChild(iframe)
    _captureIframe = iframe
  }
  return _captureIframe
}

/** 将卡片 HTML 写入常驻 iframe 并返回其文档。
 * 用 document.write 而非 iframe.src 导航：导航会让 Chrome 每次重新打印 sandbox 告警，
 * document.write 在同一文档内加载内容，不触发导航，告警只出现一次。 */
function writeCardIntoIframe(iframe: HTMLIFrameElement, htmlSrc: string, width: number): Document {
  iframe.style.width = `${width}px`
  iframe.style.height = '600px'
  const doc = iframe.contentDocument
  if (!doc) throw new Error('no contentDocument')
  doc.open()
  doc.write(htmlSrc)
  doc.close()
  return doc
}

/** 等待卡片文档资源加载完成（readyState=complete）与字体就绪，避免截到未渲染内容。 */
async function waitForDocReady(doc: Document): Promise<void> {
  const deadline = Date.now() + 8000
  while (doc.readyState !== 'complete' && Date.now() < deadline) {
    await new Promise(r => setTimeout(r, 100))
  }
  if (doc.fonts?.ready) {
    try {
      await Promise.race([doc.fonts.ready, new Promise(r => setTimeout(r, 3000))])
    } catch { /* 字体加载失败不阻塞截图 */ }
  }
}

/** 计算卡片内容的 DOM 包围盒（body 内可见元素的包围盒，CSS 像素）。
 * 全屏等宽视口下卡片不延伸占满时，用于裁剪掉入图的空白；同时还原模板 body 的内边距，
 * 避免误裁掉卡片自身留白（只裁"卡片未延伸"造成的大块空白）。 */
function measureContentBox(doc: Document): { left: number; top: number; right: number; bottom: number } | null {
  const body = doc.body
  if (!body) return null
  const bodyRect = body.getBoundingClientRect()
  const bw = bodyRect.width
  const bh = bodyRect.height
  if (bw <= 0 || bh <= 0) return null
  let minLeft = bw, minTop = bh, maxRight = 0, maxBottom = 0
  let found = false
  for (const el of Array.from(body.querySelectorAll('*'))) {
    const r = el.getBoundingClientRect()
    if (r.width <= 0 || r.height <= 0) continue
    // 跳过完全在视口外的元素（如隐藏的装饰元素）
    if (r.right <= 0 || r.bottom <= 0 || r.left >= bw || r.top >= bh) continue
    minLeft = Math.min(minLeft, Math.max(r.left, 0))
    minTop = Math.min(minTop, Math.max(r.top, 0))
    maxRight = Math.max(maxRight, Math.min(r.right, bw))
    maxBottom = Math.max(maxBottom, Math.min(r.bottom, bh))
    found = true
  }
  if (!found) return null
  const cs = getComputedStyle(body)
  const padL = parseFloat(cs.paddingLeft) || 0
  const padT = parseFloat(cs.paddingTop) || 0
  const padR = parseFloat(cs.paddingRight) || 0
  const padB = parseFloat(cs.paddingBottom) || 0
  return {
    left: Math.max(0, minLeft - padL),
    top: Math.max(0, minTop - padT),
    right: Math.min(bw, maxRight + padR),
    bottom: Math.min(bh, maxBottom + padB),
  }
}

/** 像素级内容包围盒：扫描与背景色一致的像素，取非背景内容的最小包围盒（成品图像素坐标）。
 * 背景：solid=白色，transparent=全透明。用于 DOM 检测不到的场景（如 width:100% 根容器
 * 占满视口但内容未延伸）兜底裁剪；容差取 6，接近白色的浅色卡片背景不会被误判为空白。 */
function measurePixelContentBox(canvas: HTMLCanvasElement, transparent: boolean): { left: number; top: number; right: number; bottom: number } | null {
  const ctx = canvas.getContext('2d')
  if (!ctx) return null
  const { width: w, height: h } = canvas
  const data = ctx.getImageData(0, 0, w, h).data
  let minX = w, minY = h, maxX = -1, maxY = -1
  const tol = 6
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4
      const blank = transparent
        ? data[i + 3] <= tol
        : data[i] >= 255 - tol && data[i + 1] >= 255 - tol && data[i + 2] >= 255 - tol && data[i + 3] >= 255 - tol
      if (!blank) {
        if (x < minX) minX = x
        if (x > maxX) maxX = x
        if (y < minY) minY = y
        if (y > maxY) maxY = y
      }
    }
  }
  if (maxX < 0) return null
  return { left: minX, top: minY, right: maxX + 1, bottom: maxY + 1 }
}

/** 计算成品图的裁剪框（CSS 像素坐标）：
 * 1) 优先 DOM 内容包围盒——保留卡片容器与自身留白（适用于 max-width/固定宽度卡片）；
 * 2) DOM 检测为占满（如 width:100% 根容器导致内容未延伸）时，用像素扫描兜底裁掉右侧空白。 */
function computeCropBox(doc: Document, canvas: HTMLCanvasElement, scale: number, transparent: boolean): { left: number; top: number; right: number; bottom: number } | null {
  const cw = canvas.width / scale
  const ch = canvas.height / scale
  const dom = measureContentBox(doc)
  if (dom && (cw - (dom.right - dom.left) > 16 || ch - (dom.bottom - dom.top) > 16)) return dom
  const px = measurePixelContentBox(canvas, transparent)
  if (px) {
    const pw = (px.right - px.left) / scale
    const ph = (px.bottom - px.top) / scale
    // 仅当存在大块空白（>96 CSS px）才用像素裁剪：避免把满宽白底卡片的内边距误当空白裁掉
    if (cw - pw > 96 || ch - ph > 96) {
      const m = 8 // 回补少量边距，避免内容贴边
      return {
        left: Math.max(0, px.left / scale - m),
        top: Math.max(0, px.top / scale - m),
        right: Math.min(cw, px.right / scale + m),
        bottom: Math.min(ch, px.bottom / scale + m),
      }
    }
  }
  return null
}

/** 按内容包围盒裁剪 canvas（坐标乘像素倍率）；无可裁空白（包围盒覆盖整图）时原样返回。 */
function cropCanvasByBox(canvas: HTMLCanvasElement, box: { left: number; top: number; right: number; bottom: number } | null, scale: number): HTMLCanvasElement {
  if (!box) return canvas
  const cw = Math.round((box.right - box.left) * scale)
  const ch = Math.round((box.bottom - box.top) * scale)
  // 仅当包围盒在横竖两个方向都覆盖整图时才算"无可裁空白"；任一方向有空白都要裁剪。
  // 注意不能用 `cw >= canvas.width || ch >= canvas.height`——卡片纵向铺满但横向未延伸时，
  // ch 会恰好等于 canvas.height，用 || 会误判为无需裁剪而保留右侧空白（历史踩坑点）。
  if (cw <= 0 || ch <= 0 || (cw >= canvas.width && ch >= canvas.height)) return canvas
  const out = document.createElement('canvas')
  out.width = cw
  out.height = ch
  const ctx = out.getContext('2d')
  if (!ctx) return canvas
  ctx.drawImage(canvas, Math.round(box.left * scale), Math.round(box.top * scale), cw, ch, 0, 0, cw, ch)
  return out
}

/** 加载图片（data URL）。 */
function loadImage(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve(img)
    img.onerror = () => reject(new Error('image load failed'))
    img.src = src
  })
}

/**
 * 主引擎：html-to-image（SVG foreignObject 原生布局 + 字体内嵌）。
 */
async function captureWithHtmlToImage(doc: Document, iframe: HTMLIFrameElement, opts: { width: number; transparent: boolean; scale: number }): Promise<Blob> {
  const body = doc.body
  if (!body) throw new Error('no body')
  // 等待 webfont 加载完成，避免回退字体导致文字偏移
  if (doc.fonts?.ready) await doc.fonts.ready
  // 等待 ECharts 等异步渲染脚本稳定（通常 < 500ms，预留少量余量）
  await new Promise(r => setTimeout(r, 300))

  const { width, transparent, scale } = opts
  // 读取实际渲染高度，避免固定 iframe 高度引起底部大量空白
  const contentH = Math.ceil(body.getBoundingClientRect().height) || 600
  iframe.style.height = `${contentH}px`

  // 把 iframe 文档里的 webfont 内嵌为 data URL，杜绝 foreignObject 渲染时回退字体
  let fontEmbedCSS = ''
  try {
    fontEmbedCSS = await getFontEmbedCSS(body, { fetchRequestInit: { cache: 'force-cache' } })
  } catch { /* ignore：字体嵌入失败时继续，布局仍原生 */ }

  const dataUrl = await toPng(body, {
    width,
    height: contentH,
    pixelRatio: scale,
    // 透明背景时不传 backgroundColor（缺省即透明）；否则白底
    ...(transparent ? {} : { backgroundColor: '#ffffff' }),
    fontEmbedCSS,
    cacheBust: false,
    fetchRequestInit: { cache: 'force-cache' },
    // 排除带 data-no-capture 属性的元素
    filter: (node: Node) => !(node instanceof HTMLElement && 'noCapture' in node.dataset),
  })

  // 空白裁剪：全屏等宽视口下卡片未延伸占满时，按内容包围盒裁掉右侧/四周空白
  const img = await loadImage(dataUrl)
  const full = document.createElement('canvas')
  full.width = Math.round(width * scale)
  full.height = Math.round(contentH * scale)
  const fctx = full.getContext('2d')
  if (!fctx) return (await fetch(dataUrl)).blob()
  fctx.drawImage(img, 0, 0, full.width, full.height)
  const cropBox = computeCropBox(doc, full, scale, transparent)
  const cropped = cropCanvasByBox(full, cropBox, scale)
  return new Promise<Blob>((res, rej) => cropped.toBlob(b => (b ? res(b) : rej(new Error('toBlob failed'))), 'image/png'))
}

/**
 * 兜底引擎：html2canvas（重实现布局，用于 html-to-image 无法处理的边界）。
 */
async function captureWithHtml2Canvas(doc: Document, iframe: HTMLIFrameElement, opts: { width: number; transparent: boolean; scale: number }): Promise<Blob> {
  const body = doc.body
  if (!body) throw new Error('no body')
  const { width, transparent, scale } = opts
  // 等待 ECharts 等异步渲染脚本完成（html2canvas 无字体等待，预留 1.5s）
  await new Promise(r => setTimeout(r, 1500))
  const contentH = Math.ceil(body.getBoundingClientRect().height) || 600
  iframe.style.height = `${contentH}px`
  const h2c = await lazyHtml2Canvas()
  const canvas = await h2c(body, {
    scale: scale, useCORS: true, logging: false,
    backgroundColor: transparent ? null : '#ffffff',
    width, height: contentH,
    windowWidth: width, windowHeight: contentH,
  })
  const cropBox = computeCropBox(doc, canvas, scale, transparent)
  const cropped = cropCanvasByBox(canvas, cropBox, scale)
  return new Promise<Blob>((res, rej) => cropped.toBlob(b => (b ? res(b) : rej(new Error('toBlob failed'))), 'image/png'))
}

/** 截图主流程：写入常驻 iframe → 等待就绪 → 主引擎截图（失败按需降级 html2canvas）。 */
async function captureCard(
  htmlSrc: string,
  width: number,
  transparent: boolean,
  scale: number,
  engine: 'htmlToImage' | 'html2Canvas',
  allowFallback: boolean,
): Promise<Blob> {
  const iframe = getCaptureIframe()
  const doc = writeCardIntoIframe(iframe, htmlSrc, width)
  await waitForDocReady(doc)

  const engines: Array<'htmlToImage' | 'html2Canvas'> =
    engine === 'html2Canvas' ? ['html2Canvas'] : allowFallback ? ['htmlToImage', 'html2Canvas'] : ['htmlToImage']
  const opts = { width, transparent, scale }
  let lastErr: unknown
  for (const eng of engines) {
    try {
      return eng === 'htmlToImage'
        ? await captureWithHtmlToImage(doc, iframe, opts)
        : await captureWithHtml2Canvas(doc, iframe, opts)
    } catch (err) {
      lastErr = err
    }
  }
  throw lastErr
}

/**
 * 把 HTML Widget 卡片截成 PNG Blob。
 *
 * 主路径 html-to-image（原生布局 + 字体内嵌）保证与屏幕所见一致；
 * 失败时自动降级 html2canvas，保证极端边界（跨域图片/污染 canvas）仍能导出。
 * 结果按内容包围盒裁剪，卡片未延伸占满视口时不会把空白截入成品图。
 *
 * @param htmlSrc    卡片完整 HTML 文档（含 <!doctype>）
 * @param options    截图参数，width 通常为展示 iframe 当前宽度
 */
export async function captureWidgetFromHtml(htmlSrc: string, { width, transparent = false, scale = 2 }: HtmlCaptureOptions): Promise<Blob> {
  // 主路径 html-to-image；失败降级 html2canvas（如卡片含被 CORS 污染的 canvas 时 toPng 抛错）
  return captureCard(htmlSrc, width, transparent, scale, 'htmlToImage', true)
}

// ── 测试钩子：暴露两个引擎供 E2E A/B 像素对比（生产无调用，仅测试脚本使用）──
declare global {
  interface Window {
    __htmlWidgetCapture?: {
      /** html-to-image 主引擎（原生布局） */
      htmlToImage: (htmlSrc: string, opts: HtmlCaptureOptions) => Promise<Blob>
      /** html2canvas 兜底引擎（重实现布局） */
      html2Canvas: (htmlSrc: string, opts: HtmlCaptureOptions) => Promise<Blob>
    }
  }
}
if (typeof window !== 'undefined') {
  window.__htmlWidgetCapture = {
    htmlToImage: (htmlSrc, opts) => captureCard(htmlSrc, opts.width, opts.transparent ?? false, opts.scale ?? 2, 'htmlToImage', false),
    html2Canvas: (htmlSrc, opts) => captureCard(htmlSrc, opts.width, opts.transparent ?? false, opts.scale ?? 2, 'html2Canvas', false),
  }
}
