using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>对话选项。面向用户的简洁参数集合，可在每次调用时覆盖客户端默认值</summary>
/// <remarks>
/// 与 <see cref="ChatCompletionRequest"/> 的关系：ChatOptions 是面向 SDK 用户的简洁 API，
/// ChatCompletionRequest 是面向协议层的完整 DTO。客户端内部将两者合并后发送给服务商。
/// 所有属性均为 nullable，null 表示沿用客户端默认值。
/// </remarks>
public class ChatOptions : IExtend
{
    /// <summary>模型编码。覆盖客户端默认模型</summary>
    /// <remarks>
    /// 不同模型在能力、速度、价格上差异显著。常见选择：
    /// <list type="bullet">
    ///   <item><description>qwen-turbo：响应最快，适合简单问答和高并发场景</description></item>
    ///   <item><description>qwen-plus：综合性价比最优，适合多数业务场景</description></item>
    ///   <item><description>qwen-max：最强推理能力，适合复杂分析和代码生成</description></item>
    /// </list>
    /// null 时沿用 AiClientOptions.Model 配置的默认值。
    /// </remarks>
    public String? Model { get; set; }

    /// <summary>温度。0~2，越高越随机</summary>
    /// <remarks>
    /// 控制模型输出的随机性（创造力），本质是对 softmax 分布做温度缩放：
    /// <list type="bullet">
    ///   <item><description>0：趋向贪心解码，每步选择概率最高的 Token，输出高度确定。适合数学计算、代码生成、结构化提取等需要稳定结果的场景</description></item>
    ///   <item><description>0.2~0.7：确定性与多样性的平衡区间，适合多数对话和摘要场景</description></item>
    ///   <item><description>0.8~1.2：随机性较高，适合创意写作、头脑风暴、风格多样化</description></item>
    ///   <item><description>1.2~2：高度随机，输出可能不连贯，一般不建议使用</description></item>
    /// </list>
    /// 通常与 <see cref="TopP"/> 二选一调整，同时设置两者可能互相干扰。
    /// </remarks>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与Temperature二选一</summary>
    /// <remarks>
    /// 又称 Top-P Sampling，每步只从累积概率达到 P 的候选 Token 集合中采样：
    /// <list type="bullet">
    ///   <item><description>0.1：仅考虑概率最高的前 10% Token，输出保守集中</description></item>
    ///   <item><description>0.5：覆盖前 50% 概率的 Token，适中多样性</description></item>
    ///   <item><description>0.9（常用默认）：覆盖前 90% 概率的 Token，保留较丰富的表达多样性</description></item>
    ///   <item><description>1.0：不限制候选集，等同于纯温度采样</description></item>
    /// </list>
    /// TopP 的效果需大量样本统计才能直观感受到，单次调用无法验证其行为是否生效。
    /// 通常与 <see cref="Temperature"/> 二选一调整，避免同时设置。
    /// </remarks>
    public Double? TopP { get; set; }

    /// <summary>候选词数量。从概率最高的 TopK 个 Token 中采样，默认不限制</summary>
    /// <remarks>
    /// 每步只从概率排名前 K 的 Token 中采样，是比 TopP 更硬性的截断方式：
    /// <list type="bullet">
    ///   <item><description>1：每步强制取概率最高的 Token，等同于贪心解码，输出完全确定</description></item>
    ///   <item><description>10~50：限制候选词数量，减少低概率噪声 Token</description></item>
    ///   <item><description>不设置（null）：通常由模型内部默认值控制，部分服务商不支持此参数</description></item>
    /// </list>
    /// 注意：并非所有服务商都支持 TopK，传递给不支持的 API 可能返回参数错误。
    /// </remarks>
    public Int32? TopK { get; set; }

    /// <summary>最大生成令牌数</summary>
    /// <remarks>
    /// 限制模型单次响应的输出 Token 上限，达到上限时模型会截断生成并将 finish_reason 设为 "length"：
    /// <list type="bullet">
    ///   <item><description>设置较小值（如 10~50）可严格控制响应长度，适合提取关键词、分类等短输出任务</description></item>
    ///   <item><description>设置较大值（如 4096~8192）适合长文生成、代码补全等任务</description></item>
    ///   <item><description>null：使用模型默认上限，通常为 2048~4096，不同模型差异较大</description></item>
    /// </list>
    /// 此参数不影响计费中的输入 Token 数，仅限制输出 Token 数。
    /// </remarks>
    public Int32? MaxTokens { get; set; }

    /// <summary>停止词列表</summary>
    /// <remarks>
    /// 模型在生成过程中一旦即将输出停止词，立即停止生成，停止词本身不会出现在输出中：
    /// <list type="bullet">
    ///   <item><description>适用场景：生成结构化文本时在特定分隔符处截断，如 JSON 生成时停在 "}"</description></item>
    ///   <item><description>计数截断：如 Stop=["5"] 配合"从1数到10"的提示词，输出将在 5 之前停止</description></item>
    ///   <item><description>大多数服务商最多支持 4 个停止词</description></item>
    /// </list>
    /// 注意：停止词是精确字符串匹配，非正则表达式，且区分大小写。
    /// </remarks>
    public IList<String>? Stop { get; set; }

    /// <summary>存在惩罚。-2~2</summary>
    /// <remarks>
    /// 对已出现过的所有 Token 施加固定惩罚分，与出现频率无关：
    /// <list type="bullet">
    ///   <item><description>正值（如 1.0~1.5）：降低重复话题的概率，鼓励模型引入新概念，适合需要内容多样性的场景</description></item>
    ///   <item><description>负值（如 -0.5）：鼓励重复使用已出现的词，适合需要保持一致术语的场景</description></item>
    ///   <item><description>0（默认）：不施加惩罚</description></item>
    /// </list>
    /// 与 <see cref="FrequencyPenalty"/> 的区别：PresencePenalty 是"出现过就惩罚一次"（0/1 开关），
    /// FrequencyPenalty 是"出现越多惩罚越重"（按频率累积）。两者效果需大量样本统计才能验证。
    /// </remarks>
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚。-2~2</summary>
    /// <remarks>
    /// 按 Token 在当前输出中已出现的次数动态施加惩罚，出现越多惩罚越重：
    /// <list type="bullet">
    ///   <item><description>正值（如 0.5~1.0）：有效抑制模型的"复读机"行为，减少重复短语和句子</description></item>
    ///   <item><description>负值：鼓励重复，一般不使用</description></item>
    ///   <item><description>0（默认）：不施加惩罚</description></item>
    /// </list>
    /// 与 <see cref="PresencePenalty"/> 的区别：FrequencyPenalty 随重复次数线性增强，
    /// 对高频重复的抑制效果更显著；PresencePenalty 只要出现过就施加固定惩罚。
    /// 两者效果需大量样本统计才能验证，单次调用无法直观感受。
    /// </remarks>
    public Double? FrequencyPenalty { get; set; }

    /// <summary>可用工具列表。用于函数调用</summary>
    /// <remarks>
    /// 定义模型可以调用的外部工具（即 Function Calling）：
    /// <list type="bullet">
    ///   <item><description>每个 <see cref="ChatTool"/> 描述一个函数的名称、说明和参数 JSON Schema</description></item>
    ///   <item><description>模型根据上下文自行决定是否调用工具及传入什么参数</description></item>
    ///   <item><description>调用结果需由调用方执行后以 tool 角色消息回传，模型再生成最终回复</description></item>
    /// </list>
    /// 配合 <see cref="ToolChoice"/> 可控制工具调用策略。
    /// </remarks>
    public IList<ChatTool>? Tools { get; set; }

    /// <summary>工具选择策略。auto/none/required 或指定工具名</summary>
    /// <remarks>
    /// 控制模型是否以及如何使用工具，仅在 <see cref="Tools"/> 非空时有效：
    /// <list type="bullet">
    ///   <item><description>"auto"（默认）：模型自行判断是否调用工具</description></item>
    ///   <item><description>"none"：禁止调用任何工具，模型直接生成文本回复</description></item>
    ///   <item><description>"required"：强制模型必须调用至少一个工具</description></item>
    ///   <item><description>指定工具对象（如 {"type":"function","function":{"name":"xxx"}}）：强制调用特定工具</description></item>
    /// </list>
    /// </remarks>
    public Object? ToolChoice { get; set; }

    /// <summary>用户标识。用于服务商侧的请求追踪和限流</summary>
    /// <remarks>
    /// 透传给服务商 API 的 user 字段，不影响模型生成内容：
    /// <list type="bullet">
    ///   <item><description>可用于服务商控制台的用量分析和异常用户追踪</description></item>
    ///   <item><description>部分服务商依据此字段实施用户级限流策略</description></item>
    ///   <item><description>建议传入应用内的用户唯一标识（非敏感信息），如哈希后的用户 ID</description></item>
    /// </list>
    /// 注意：此字段是透传给 LLM 服务商的标识，与 <see cref="UserId"/> 不同，
    /// UserId 仅在 SDK 内部中间件管道中流转，不发送给服务商。
    /// </remarks>
    public String? User { get; set; }

    /// <summary>是否启用思考模式。null=不设置，true=开启，false=关闭</summary>
    /// <remarks>
    /// 部分模型（如 QwQ、DeepSeek-R1、Claude 3.7 Sonnet）支持显式的"思考"（Chain-of-Thought）阶段，
    /// 在生成最终回复前先输出推理过程：
    /// <list type="bullet">
    ///   <item><description>true：开启思考模式，响应中会包含 <see cref="ChatMessage.ReasoningContent"/> 字段</description></item>
    ///   <item><description>false：关闭思考模式，仅返回最终回复</description></item>
    ///   <item><description>null：沿用模型默认行为（通常为关闭）</description></item>
    /// </list>
    /// 思考模式会显著增加输出 Token 数和响应延迟，建议仅在复杂推理任务中开启。
    /// 不支持思考模式的模型会忽略此参数。
    /// </remarks>
    public Boolean? EnableThinking { get; set; }

    /// <summary>响应格式。用于约束模型输出结构</summary>
    /// <remarks>
    /// 用于结构化输出（Structured Output），指示模型按指定格式生成响应：
    /// <list type="bullet">
    ///   <item><description>{"type":"json_object"}：强制输出合法 JSON，但不约束具体 Schema</description></item>
    ///   <item><description>{"type":"json_schema","json_schema":{...}}：按指定 JSON Schema 约束输出，最严格</description></item>
    ///   <item><description>{"type":"text"}（默认）：普通文本输出</description></item>
    /// </list>
    /// 使用 json_object 或 json_schema 时，建议在 System Prompt 中同时说明期望的 JSON 格式，
    /// 否则模型可能生成符合格式但内容不符合预期的 JSON。
    /// 注意：并非所有模型和服务商都支持 json_schema，请查阅对应文档。
    /// </remarks>
    public Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用。null=不设置，true=允许，false=禁止</summary>
    /// <remarks>
    /// 当模型判断需要调用多个工具时，控制是否允许在单次响应中同时发起多个工具调用：
    /// <list type="bullet">
    ///   <item><description>true：模型可在一次响应中返回多个 tool_calls，调用方并行执行后统一回传结果，减少往返次数</description></item>
    ///   <item><description>false：每次响应最多一个工具调用，适合有严格顺序依赖的工具链场景</description></item>
    ///   <item><description>null：沿用模型默认行为（通常为允许）</description></item>
    /// </list>
    /// 仅在 <see cref="Tools"/> 非空时有意义。部分服务商或模型不支持此参数。
    /// </remarks>
    public Boolean? ParallelToolCalls { get; set; }

    /// <summary>当前请求的用户编号。传递给过滤器链，供 LearningFilter 等中间件读取</summary>
    /// <remarks>
    /// SDK 内部中间件（Filter）管道专用字段，不会发送给 LLM 服务商 API：
    /// <list type="bullet">
    ///   <item><description>LearningFilter 用此字段将对话历史关联到具体用户，实现用户级记忆</description></item>
    ///   <item><description>审计、限流等中间件可据此字段实施用户粒度的策略</description></item>
    /// </list>
    /// 若需要透传用户标识给服务商，请使用 <see cref="User"/> 字段。
    /// </remarks>
    public String? UserId { get; set; }

    /// <summary>当前请求的会话编号。传递给过滤器链，供 LearningFilter 等中间件读取</summary>
    /// <remarks>
    /// SDK 内部中间件（Filter）管道专用字段，不会发送给 LLM 服务商 API：
    /// <list type="bullet">
    ///   <item><description>LearningFilter 用此字段将多轮消息归属到同一会话，维护会话级记忆和上下文</description></item>
    ///   <item><description>同一 ConversationId 的请求共享历史消息，不同 ConversationId 的请求彼此隔离</description></item>
    ///   <item><description>不设置（null）时，不进行会话级记忆聚合</description></item>
    /// </list>
    /// </remarks>
    public String? ConversationId { get; set; }

    /// <summary>扩展数据。用于在中间件管道中传递非结构化的自定义上下文</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
}
