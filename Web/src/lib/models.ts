import type { ModelInfo } from '@/types'

/**
 * 判断模型是否为对话模型（用于默认模型候选过滤，与后端 IsChatModel 语义一致）。
 *
 * 主模型列表已由服务端按 IsMainModel 过滤（排除语音识别/语音合成/嵌入/排序，纳入文生图/文生视频），
 * 此处按能力位在客户端计算：含语音特征位即非对话模型；具备思考/工具/视觉任一能力，
 * 或不带图像/视频生成标记的模型视为对话模型（文生图/文生视频类不算对话模型）。
 */
export function isChatModel(m: ModelInfo): boolean {
  if (m.supportAudio || m.supportSpeech) return false
  return !!(m.supportThinking || m.supportFunction || m.supportVision || (!m.supportImage && !m.supportVideo))
}
