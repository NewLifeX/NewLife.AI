namespace NewLife.AI.Services;

/// <summary>AI 对话系统核心配置接口。NewLife.AI 通用编排层通过此接口取用配置项，
/// 与具体实现解耦，由 ChatAI/StarChat 各自注册 &lt;IChatSetting, ChatSetting&gt; 实现注入</summary>
public interface IChatSetting
{
    #region 基本配置
    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    Boolean AutoGenerateTitle { get; }
    #endregion

    #region 对话默认
    /// <summary>默认模型。新用户的默认模型配置Id，0表示自动选择优先级最高的可用文本模型</summary>
    Int32 DefaultModel { get; }

    /// <summary>轻量模型。标题生成、摘要压缩、知识蒸馏等简单任务调用的模型编码（ModelConfig.Code）；为空时自动选择优先级最高的 flash/lite/mini/small 轻量文本模型，没有时回退到主模型</summary>
    String LightweightModel { get; }

    /// <summary>嵌入模型。向量检索/向量嵌入场景调用的模型编码（ModelConfig.Code）；为空时自动选择优先级最高的嵌入模型（SupportEmbedding=true），没有时退化为本地哈希嵌入</summary>
    String EmbedModel { get; }

    /// <summary>重排序模型。CrossEncoder 二次精排场景调用的模型编码（ModelConfig.Code）；为空时跳过重排序步骤，直接使用 BM25+向量融合分</summary>
    String RerankModel { get; }

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    Int32 DefaultContextRounds { get; }
    #endregion

    #region 工具与能力
    /// <summary>工具调用最大轮次。防止工具调用无限递归，默认10</summary>
    Int32 ToolMaxIterations { get; }

    /// <summary>工具结果最大字符数。工具返回结果超过此长度时自动截断并追加摘要提示，0表示不限制</summary>
    Int32 ToolResultMaxChars { get; }

    /// <summary>推荐问题缓存。开启后命中当天缓存时直接返回，不请求大模型</summary>
    Boolean EnableSuggestedQuestionCache { get; }

    /// <summary>流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容</summary>
    Int32 StreamingSpeed { get; }

    /// <summary>技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认8000</summary>
    Int32 SkillBudgetChars { get; }
    #endregion

    #region 系统指令
    /// <summary>全局系统指令。注入每一个用户的每一次对话，置于 System Prompt 首部作为全局行为准则</summary>
    String SystemInstruction { get; }
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    Boolean EnableUsageStats { get; }

    /// <summary>启用函数调用</summary>
    Boolean EnableFunctionCalling { get; }

    /// <summary>后台继续生成。浏览器关闭后模型继续生成，切换会话后再切回可恢复</summary>
    Boolean BackgroundGeneration { get; }

    /// <summary>用户隔离。启用后向LLM服务商透传User字段，用于服务商侧KVCache隔离</summary>
    Boolean EnableUserIsolation { get; }

    #endregion
}
