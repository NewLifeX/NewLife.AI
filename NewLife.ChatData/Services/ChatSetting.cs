using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Configuration;
using XCode.Configuration;

namespace NewLife.ChatData.Services;

/// <summary>AI对话系统配置。继承 Config 自动加载保存到 Config/ChatSetting.config</summary>
[DisplayName("AI对话配置")]
public class ChatSetting : Config<ChatSetting>
{
    #region 静态
    /// <summary>指向数据库参数字典表</summary>
    static ChatSetting() => Provider = new DbConfigProvider { UserId = 0, Category = "Chat" };
    #endregion

    #region 基本配置
    /// <summary>应用名称。显示在 /chat 左上角侧边栏顶部</summary>
    [Category("基本配置")]
    [Description("应用名称。显示在 /chat 左上角侧边栏顶部")]
    public String Name { get; set; } = "星语";

    /// <summary>站点标题。显示在浏览器标签页和 /chat 页面顶部</summary>
    [Category("基本配置")]
    [Description("站点标题。显示在浏览器标签页和 /chat 页面顶部")]
    public String SiteTitle { get; set; } = "智能助手";

    /// <summary>Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标</summary>
    [Category("基本配置")]
    [Description("Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标")]
    public String LogoUrl { get; set; } = "";

    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    [Category("基本配置")]
    [Description("自动生成标题。首条消息后是否自动生成会话标题")]
    public Boolean AutoGenerateTitle { get; set; } = true;
    #endregion

    #region 对话默认
    /// <summary>默认模型。新用户的默认模型配置Id，0表示使用第一个可用模型</summary>
    [Category("对话默认")]
    [Description("默认模型。新用户的默认模型配置Id，0表示使用第一个可用模型")]
    public Int32 DefaultModel { get; set; }

    /// <summary>默认思考模式</summary>
    [Category("对话默认")]
    [Description("默认思考模式。Auto=自动，Think=深度思考，Fast=快速响应")]
    public ThinkingMode DefaultThinkingMode { get; set; } = ThinkingMode.Auto;

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    [Category("对话默认")]
    [Description("上下文轮数。每次请求携带的历史对话轮数，默认10")]
    public Int32 DefaultContextRounds { get; set; } = 10;

    /// <summary>压缩触发阈值。历史消息Token占比超过模型上下文窗口此比例时自动触发压缩，默认0.8</summary>
    [Category("对话默认")]
    [Description("压缩触发阈值。历史消息Token占比超过模型上下文窗口此比例时自动触发压缩，默认0.8")]
    public Double CompactionThreshold { get; set; } = 0.8;
    #endregion

    #region 多轮注意力
    /// <summary>三明治引导阈值。历史消息条数超过此值时，在末尾追加system消息引导LLM聚焦最新问题。0表示禁用，默认2（即1轮完整对话后生效）</summary>
    [Category("多轮注意力")]
    [Description("三明治引导阈值。历史消息条数超过此值时，在末尾追加system消息引导LLM聚焦最新问题。0表示禁用，默认2（即1轮完整对话后生效）")]
    public Int32 SandwichThreshold { get; set; } = 2;

    /// <summary>启用追问检测。检测AI回复末尾是否包含追问，自动生成上下文感知的三明治引导消息，使AI基于用户回答继续深入而非重复回答</summary>
    [Category("多轮注意力")]
    [Description("启用追问检测。检测AI回复末尾是否包含追问，自动生成上下文感知的三明治引导消息，使AI基于用户回答继续深入而非重复回答")]
    public Boolean EnableFollowUpDetection { get; set; } = true;

    /// <summary>启用早期轮次截断。对非最近N轮的assistant长回复截断为摘要性前缀，减少注意力稀释。与CompactionService互补：截断零成本立即生效，压缩需调用轻量模型</summary>
    [Category("多轮注意力")]
    [Description("启用早期轮次截断。对非最近N轮的assistant长回复截断为摘要性前缀，减少注意力稀释。与CompactionService互补：截断零成本立即生效，压缩需调用轻量模型")]
    public Boolean EnableEarlyTruncation { get; set; } = true;

    /// <summary>早期截断保留字符数。早期轮次assistant回复截断后保留的字符数，默认200</summary>
    [Category("多轮注意力")]
    [Description("早期截断保留字符数。早期轮次assistant回复截断后保留的字符数，默认200")]
    public Int32 EarlyTruncationChars { get; set; } = 200;

    /// <summary>完整保留轮数。最近N轮对话保留完整内容不截断，默认2</summary>
    [Category("多轮注意力")]
    [Description("完整保留轮数。最近N轮对话保留完整内容不截断，默认2")]
    public Int32 RecentPreserveRounds { get; set; } = 2;

    /// <summary>启用对话流状态机。由轻量模型判断每轮对话的流向（AI含追问/用户回应追问/用户转向新话题），生成精确的三明治引导。开启后增加一次轻量模型调用，但效果最佳</summary>
    [Category("多轮注意力")]
    [Description("启用对话流状态机。由轻量模型判断每轮对话的流向（AI含追问/用户回应追问/用户转向新话题），生成精确的三明治引导。开启后增加一次轻量模型调用，但效果最佳")]
    public Boolean EnableConversationStateMachine { get; set; } = false;

    /// <summary>多轮引导提示词。注入到System Prompt头部的多轮对话行为指令，为空时使用内置默认模板</summary>
    [Category("多轮注意力")]
    [Description("多轮引导提示词。注入到System Prompt头部的多轮对话行为指令，为空时使用内置默认模板")]
    public String MultiTurnPrompt { get; set; } = "";
    #endregion

    #region 上传与分享
    /// <summary>最大附件大小（MB）</summary>
    [Category("上传与分享")]
    [Description("最大附件大小（MB）")]
    public Int32 MaxAttachmentSize { get; set; } = 20;

    /// <summary>单次最多上传附件数</summary>
    [Category("上传与分享")]
    [Description("单次最多上传附件数")]
    public Int32 MaxAttachmentCount { get; set; } = 5;

    /// <summary>允许的文件扩展名</summary>
    [Category("上传与分享")]
    [Description("允许的文件扩展名")]
    public String AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.doc,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv";

    /// <summary>图像生成默认尺寸</summary>
    [Category("上传与分享")]
    [Description("图像生成默认尺寸")]
    public String DefaultImageSize { get; set; } = "1024x1024";

    /// <summary>分享有效期。共享链接有效天数，0 表示永不过期</summary>
    [Category("上传与分享")]
    [Description("分享有效期。共享链接有效天数，0 表示永不过期")]
    public Int32 ShareExpireDays { get; set; } = 30;
    #endregion

    #region API 网关
    /// <summary>启用 API 网关</summary>
    [Category("API 网关")]
    [Description("启用 API 网关")]
    public Boolean EnableGateway { get; set; } = true;

    /// <summary>API网关管道增强。API网关请求是否走完整能力扩展管道（技能/工具/提示词注入等），关闭后退回到直接代理转发</summary>
    [Category("API 网关")]
    [Description("API网关管道增强。API网关请求是否走完整能力扩展管道（技能/工具/提示词注入等），关闭后退回到直接代理转发")]
    public Boolean EnableGatewayPipeline { get; set; } = true;

    /// <summary>网关限流。每分钟每用户最大请求次数</summary>
    [Category("API 网关")]
    [Description("网关限流。每分钟每用户最大请求次数")]
    public Int32 GatewayRateLimit { get; set; } = 60;

    /// <summary>上游重试次数。模型返回 429 时最大重试</summary>
    [Category("API 网关")]
    [Description("上游重试次数。模型返回 429 时最大重试")]
    public Int32 UpstreamRetryCount { get; set; } = 5;

    /// <summary>网关对话记录。开启后API网关的对话将同步记录为Conversation和ChatMessage，用于数据分析和知识进化</summary>
    [Category("API 网关")]
    [Description("网关对话记录。开启后API网关的对话将同步记录为Conversation和ChatMessage，用于数据分析和知识进化")]
    public Boolean EnableGatewayRecording { get; set; } = false;
    #endregion

    #region 工具与能力
    /// <summary>启用函数调用</summary>
    [Category("工具与能力")]
    [Description("启用函数调用")]
    public Boolean EnableFunctionCalling { get; set; } = true;

    /// <summary>启用 MCP 工具调用</summary>
    [Category("工具与能力")]
    [Description("启用 MCP 工具调用")]
    public Boolean EnableMcp { get; set; } = true;

    /// <summary>启用消息渠道集成。支持钉钉、企业微信、飞书等</summary>
    [Category("工具与能力")]
    [Description("启用消息渠道集成。支持钉钉、企业微信、飞书等")]
    public Boolean EnableChannels { get; set; } = true;

    /// <summary>推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型</summary>
    [Category("工具与能力")]
    [Description("推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型")]
    public Boolean EnableSuggestedQuestionCache { get; set; } = true;

    /// <summary>流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟</summary>
    [Category("工具与能力")]
    [Description("流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟")]
    public Int32 StreamingSpeed { get; set; } = 3;

    /// <summary>工具渐进式发现阈值。工具总数超过此值时切换为Advertise模式，仅向模型展示工具摘要而非完整Schema，模型按需加载，默认15</summary>
    [Category("工具与能力")]
    [Description("工具渐进式发现阈值。工具总数超过此值时切换为Advertise模式，仅向模型展示工具摘要而非完整Schema，模型按需加载，默认15")]
    public Int32 ToolAdvertiseThreshold { get; set; } = 15;

    /// <summary>工具结果最大字符数。工具返回结果超过此长度时自动截断并追加摘要提示，默认8000</summary>
    [Category("工具与能力")]
    [Description("工具结果最大字符数。工具返回结果超过此长度时自动截断并追加摘要提示，默认8000")]
    public Int32 ToolResultMaxChars { get; set; } = 8000;

    /// <summary>技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认8000</summary>
    [Category("工具与能力")]
    [Description("技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认8000")]
    public Int32 SkillBudgetChars { get; set; } = 8000;

    /// <summary>记忆上下文最大字符数。记忆注入内容超过此值时截断尾部条目，默认4000</summary>
    [Category("工具与能力")]
    [Description("记忆上下文最大字符数。记忆注入内容超过此值时截断尾部条目，默认4000")]
    public Int32 MemoryBudgetChars { get; set; } = 4000;
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    [Category("功能开关")]
    [Description("启用用量统计")]
    public Boolean EnableUsageStats { get; set; } = true;

    /// <summary>后台继续生成。浏览器关闭后模型继续生成</summary>
    [Category("功能开关")]
    [Description("后台继续生成。浏览器关闭后模型继续生成")]
    public Boolean BackgroundGeneration { get; set; } = true;

    /// <summary>聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制</summary>
    [Category("功能开关")]
    [Description("聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制")]
    public Int32 MaxMessagesPerMinute { get; set; } = 20;

    /// <summary>启用定时对话作业</summary>
    [Category("功能开关")]
    [Description("启用定时对话作业")]
    public Boolean EnableScheduledJobs { get; set; } = true;

    /// <summary>每用户最大作业数。防止滥用，默认 10</summary>
    [Category("功能开关")]
    [Description("每用户最大作业数。防止滥用，默认 10")]
    public Int32 MaxJobsPerUser { get; set; } = 10;

    /// <summary>作业连续失败禁用次数。连续失败达到此数后自动禁用作业，默认 3</summary>
    [Category("功能开关")]
    [Description("作业连续失败禁用次数。连续失败达到此数后自动禁用作业，默认 3")]
    public Int32 JobFailDisableCount { get; set; } = 3;

    /// <summary>启用痛觉记忆。自动检测对话失败和危险操作模式，并在相似情形下提前注入预警信息</summary>
    [Category("功能开关")]
    [Description("启用痛觉记忆。自动检测对话失败和危险操作模式，并在相似情形下提前注入预警信息")]
    public Boolean EnableNociception { get; set; } = false;
    #endregion

    #region 自学习
    /// <summary>启用自动学习。对话结束后异步提取用户记忆，构建用户画像</summary>
    [Category("自学习")]
    [Description("启用自动学习。对话结束后异步提取用户记忆，构建用户画像")]
    public Boolean EnableAutoLearning { get; set; } = true;

    /// <summary>评分阈值。对话质量低于此分值不触发记忆提取，默认 0.6</summary>
    [Category("自学习")]
    [Description("评分阈值。对话质量低于此分值不触发记忆提取，默认 0.6")]
    public Double LearningScoreThreshold { get; set; } = 0.6;

    /// <summary>每用户最大记忆条数。超出后按置信度淘汰旧记忆，默认 500</summary>
    [Category("自学习")]
    [Description("每用户最大记忆条数。超出后按置信度淘汰旧记忆，默认 500")]
    public Int32 MaxMemoryPerUser { get; set; } = 500;

    /// <summary>记忆保留天数。长期未强化的记忆自动过期，默认 365</summary>
    [Category("自学习")]
    [Description("记忆保留天数。长期未强化的记忆自动过期，默认 365")]
    public Int32 MemoryRetentionDays { get; set; } = 365;

    /// <summary>每次对话最大注入记忆条数。注入到 System Prompt 的记忆上限，默认 5</summary>
    [Category("自学习")]
    [Description("每次对话最大注入记忆条数。注入到 System Prompt 的记忆上限，默认 5")]
    public Int32 MemoryInjectionCount { get; set; } = 5;

    /// <summary>记忆占上下文窗口百分比上限。防止记忆注入挤占对话内容，默认 10</summary>
    [Category("自学习")]
    [Description("记忆占上下文窗口百分比上限。防止记忆注入挤占对话内容，默认 10")]
    public Int32 MemoryTokenBudgetPercent { get; set; } = 10;

    /// <summary>学习分析模型。用于提取记忆的模型编码，为空时复用当前对话模型</summary>
    [Category("自学习")]
    [Description("学习分析模型。用于提取记忆的模型编码，为空时复用当前对话模型")]
    public String LearningModel { get; set; } = "";

    /// <summary>轻量模型。用于标题生成、摘要压缩、知识蒸馏等简单任务的模型编码，为空时复用LearningModel或当前对话模型</summary>
    [Category("自学习")]
    [Description("轻量模型。用于标题生成、摘要压缩、知识蒸馏等简单任务的模型编码，为空时复用LearningModel或当前对话模型")]
    public String LightweightModel { get; set; } = "";

    /// <summary>评分提示词。自定义对话质量评分模板，为空时使用内置默认模板</summary>
    [Category("自学习")]
    [Description("评分提示词。自定义对话质量评分模板，为空时使用内置默认模板")]
    public String ScoringPrompt { get; set; } = "";

    /// <summary>提取提示词。自定义记忆提取模板，为空时使用内置默认模板</summary>
    [Category("自学习")]
    [Description("提取提示词。自定义记忆提取模板，为空时使用内置默认模板")]
    public String ExtractionPrompt { get; set; } = "";

    /// <summary>启用知识融合。定时将碎片记忆融合为更高质量的知识条目</summary>
    [Category("自学习")]
    [Description("启用知识融合。定时将碎片记忆融合为更高质量的知识条目")]
    public Boolean EnableKnowledgeFusion { get; set; } = false;

    /// <summary>融合最小记忆数。同分类记忆达到此数才触发融合，默认 5</summary>
    [Category("自学习")]
    [Description("融合最小记忆数。同分类记忆达到此数才触发融合，默认 5")]
    public Int32 FusionMinMemories { get; set; } = 5;

    /// <summary>启用知识审核。受限和普通用户的低置信度知识需人工审核</summary>
    [Category("自学习")]
    [Description("启用知识审核。受限和普通用户的低置信度知识需人工审核")]
    public Boolean EnableKnowledgeReview { get; set; } = false;

    /// <summary>学习最低字数。用户消息总字数低于该值且仅 1 轮时跳过记忆提取</summary>
    [Category("自学习")]
    [Description("学习最低字数。用户消息总字数低于该值且仅 1 轮时跳过记忆提取")]
    public Int32 MinLearningContentLength { get; set; } = 50;

    /// <summary>蒸馏碎片阈值。用户碎片记忆超过此数时触发即时蒸馏，默认 20</summary>
    [Category("自学习")]
    [Description("蒸馏碎片阈值。用户碎片记忆超过此数时触发即时蒸馏，默认 20")]
    public Int32 DistillationThreshold { get; set; } = 20;
    #endregion

    #region 知识库
    /// <summary>启用知识库。开启后对话前自动检索知识库并注入上下文</summary>
    [Category("知识库")]
    [Description("启用知识库。开启后对话前自动检索知识库并注入上下文")]
    public Boolean EnableKnowledge { get; set; }

    /// <summary>知识注入字符预算。知识库注入到System Prompt的最大字符数，默认4000</summary>
    [Category("知识库")]
    [Description("知识注入字符预算。知识库注入到System Prompt的最大字符数，默认4000")]
    public Int32 KnowledgeBudgetChars { get; set; } = 4000;

    /// <summary>单次注入最大文章数。每次对话注入的知识文章上限，默认3</summary>
    [Category("知识库")]
    [Description("单次注入最大文章数。每次对话注入的知识文章上限，默认3")]
    public Int32 KnowledgeMaxArticles { get; set; } = 3;

    /// <summary>知识清洗模型。用于文档清洗的模型编码，为空时复用LightweightModel</summary>
    [Category("知识库")]
    [Description("知识清洗模型。用于文档清洗的模型编码，为空时复用LightweightModel")]
    public String KnowledgeCleanModel { get; set; } = "";

    /// <summary>知识检索模式。keyword=关键词/vector=向量/hybrid=混合，默认keyword</summary>
    [Category("知识库")]
    [Description("知识检索模式。keyword=关键词/vector=向量/hybrid=混合，默认keyword")]
    public String KnowledgeRetrievalMode { get; set; } = "keyword";

    /// <summary>爬取最大深度。网页爬取的最大递归深度，默认2</summary>
    [Category("知识库")]
    [Description("爬取最大深度。网页爬取的最大递归深度，默认2")]
    public Int32 CrawlMaxDepth { get; set; } = 2;

    /// <summary>爬取最大页数。单次爬取任务的最大页面数，默认50</summary>
    [Category("知识库")]
    [Description("爬取最大页数。单次爬取任务的最大页面数，默认50")]
    public Int32 CrawlMaxPages { get; set; } = 50;
    #endregion
}
