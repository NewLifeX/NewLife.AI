namespace NewLife.ChatData;

/// <summary>AI对话系统核心配置接口。ChatData 层通过此接口取用配置项，与具体实现解耦，
/// 由 ChatAI/StarChat 各自注册 &lt;IChatSetting, ChatSetting&gt; 实现注入</summary>
public interface IChatSetting
{
    #region 基本配置
    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    Boolean AutoGenerateTitle { get; }
    #endregion

    #region 对话默认
    /// <summary>默认模型。新用户的默认模型配置Id，0表示使用第一个可用模型</summary>
    Int32 DefaultModel { get; }

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    Int32 DefaultContextRounds { get; }
    #endregion

    #region 工具与能力
    /// <summary>推荐问题缓存。开启后命中当天缓存时直接返回，不请求大模型</summary>
    Boolean EnableSuggestedQuestionCache { get; }

    /// <summary>流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容</summary>
    Int32 StreamingSpeed { get; }

    /// <summary>技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认8000</summary>
    Int32 SkillBudgetChars { get; }
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    Boolean EnableUsageStats { get; }

    /// <summary>启用函数调用</summary>
    Boolean EnableFunctionCalling { get; }
    #endregion

    #region 自学习
    /// <summary>学习分析模型。用于提取记忆的模型编码，为空时复用当前对话模型</summary>
    String LearningModel { get; }

    /// <summary>轻量模型。用于标题生成、摘要压缩、知识蒸馏等简单任务的模型编码，为空时复用LearningModel或当前对话模型</summary>
    String LightweightModel { get; }
    #endregion
}
