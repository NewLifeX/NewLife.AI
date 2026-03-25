using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Configuration;
using XCode.Configuration;

namespace NewLife.ChatAI.Services;

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

    /// <summary>推荐问题。新会话页展示的推荐问题，多个用竖线分隔</summary>
    [Category("基本配置")]
    [Description("推荐问题。新会话页展示的推荐问题，多个用竖线分隔")]
    public String SuggestedQuestions { get; set; } = "帮我写一封邮件|解释量子计算|用C#写一个排序算法|帮我翻译一段英文";

    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    [Category("基本配置")]
    [Description("自动生成标题。首条消息后是否自动生成会话标题")]
    public Boolean AutoGenerateTitle { get; set; } = true;

    /// <summary>标题生成提示词模板</summary>
    [Category("基本配置")]
    [Description("标题生成提示词模板")]
    public String TitlePrompt { get; set; } = "请用10个字以内为以下对话生成一个简短标题，只输出标题文字，不要加任何标点和引号：";
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
    public String AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.txt,.md,.csv";

    /// <summary>图像生成默认尺寸</summary>
    [Category("上传与分享")]
    [Description("图像生成默认尺寸")]
    public String DefaultImageSize { get; set; } = "1024x1024";

    /// <summary>分享有效期。共享链接有效天数，0 表示永不过期</summary>
    [Category("上传与分享")]
    [Description("分享有效期。共享链接有效天数，0 表示永不过期")]
    public Int32 ShareExpireDays { get; set; } = 30;
    #endregion

    #region 功能开关
    /// <summary>启用函数调用</summary>
    [Category("功能开关")]
    [Description("启用函数调用")]
    public Boolean EnableFunctionCalling { get; set; } = true;

    /// <summary>启用 MCP 工具调用</summary>
    [Category("功能开关")]
    [Description("启用 MCP 工具调用")]
    public Boolean EnableMcp { get; set; } = true;

    /// <summary>启用用量统计</summary>
    [Category("功能开关")]
    [Description("启用用量统计")]
    public Boolean EnableUsageStats { get; set; } = true;

    /// <summary>后台继续生成。浏览器关闭后模型继续生成</summary>
    [Category("功能开关")]
    [Description("后台继续生成。浏览器关闭后模型继续生成")]
    public Boolean BackgroundGeneration { get; set; } = true;

    /// <summary>启用 API 网关</summary>
    [Category("功能开关")]
    [Description("启用 API 网关")]
    public Boolean EnableGateway { get; set; } = true;

    /// <summary>API网关管道增强。API网关请求是否走完整能力扩展管道（技能/工具/提示词注入等），关闭后退回到直接代理转发</summary>
    [Category("功能开关")]
    [Description("API网关管道增强。API网关请求是否走完整能力扩展管道（技能/工具/提示词注入等），关闭后退回到直接代理转发")]
    public Boolean EnableGatewayPipeline { get; set; } = true;

    /// <summary>聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制</summary>
    [Category("功能开关")]
    [Description("聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制")]
    public Int32 MaxMessagesPerMinute { get; set; } = 20;

    /// <summary>网关限流。每分钟每用户最大请求次数</summary>
    [Category("功能开关")]
    [Description("网关限流。每分钟每用户最大请求次数")]
    public Int32 GatewayRateLimit { get; set; } = 60;

    /// <summary>启用自学习。对话结束后异步提取用户记忆，构建用户画像</summary>
    [Category("功能开关")]
    [Description("启用自学习。对话结束后异步提取用户记忆，构建用户画像")]
    public Boolean EnableSelfLearning { get; set; } = false;

    /// <summary>上游重试次数。模型返回 429 时最大重试</summary>
    [Category("功能开关")]
    [Description("上游重试次数。模型返回 429 时最大重试")]
    public Int32 UpstreamRetryCount { get; set; } = 5;

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

    /// <summary>启用消息渠道集成。支持钉钉、企业微信、飞书等</summary>
    [Category("功能开关")]
    [Description("启用消息渠道集成。支持钉钉、企业微信、飞书等")]
    public Boolean EnableChannels { get; set; } = true;

    /// <summary>启用工具调用审批。桌面客户端中敏感工具调用需用户确认</summary>
    [Category("功能开关")]
    [Description("启用工具调用审批。桌面客户端中敏感工具调用需用户确认")]
    public Boolean EnableToolApproval { get; set; } = false;
    #endregion

    #region 自学习配置
    /// <summary>评分阈值。对话质量低于此分值不触发记忆提取，默认 0.6</summary>
    [Category("自学习配置")]
    [Description("评分阈值。对话质量低于此分值不触发记忆提取，默认 0.6")]
    public Double LearningScoreThreshold { get; set; } = 0.6;

    /// <summary>每用户最大记忆条数。超出后按置信度淘汰旧记忆，默认 500</summary>
    [Category("自学习配置")]
    [Description("每用户最大记忆条数。超出后按置信度淘汰旧记忆，默认 500")]
    public Int32 MaxMemoryPerUser { get; set; } = 500;

    /// <summary>记忆保留天数。长期未强化的记忆自动过期，默认 365</summary>
    [Category("自学习配置")]
    [Description("记忆保留天数。长期未强化的记忆自动过期，默认 365")]
    public Int32 MemoryRetentionDays { get; set; } = 365;

    /// <summary>每次对话最大注入记忆条数。注入到 System Prompt 的记忆上限，默认 5</summary>
    [Category("自学习配置")]
    [Description("每次对话最大注入记忆条数。注入到 System Prompt 的记忆上限，默认 5")]
    public Int32 MemoryInjectionCount { get; set; } = 5;

    /// <summary>记忆占上下文窗口百分比上限。防止记忆注入挤占对话内容，默认 10</summary>
    [Category("自学习配置")]
    [Description("记忆占上下文窗口百分比上限。防止记忆注入挤占对话内容，默认 10")]
    public Int32 MemoryTokenBudgetPercent { get; set; } = 10;

    /// <summary>学习分析模型。用于提取记忆的模型编码，为空时复用当前对话模型</summary>
    [Category("自学习配置")]
    [Description("学习分析模型。用于提取记忆的模型编码，为空时复用当前对话模型")]
    public String LearningModel { get; set; } = "";

    /// <summary>评分提示词。自定义对话质量评分模板，为空时使用内置默认模板</summary>
    [Category("自学习配置")]
    [Description("评分提示词。自定义对话质量评分模板，为空时使用内置默认模板")]
    public String ScoringPrompt { get; set; } = "";

    /// <summary>提取提示词。自定义记忆提取模板，为空时使用内置默认模板</summary>
    [Category("自学习配置")]
    [Description("提取提示词。自定义记忆提取模板，为空时使用内置默认模板")]
    public String ExtractionPrompt { get; set; } = "";

    /// <summary>启用知识融合。定时将碎片记忆融合为更高质量的知识条目</summary>
    [Category("自学习配置")]
    [Description("启用知识融合。定时将碎片记忆融合为更高质量的知识条目")]
    public Boolean EnableKnowledgeFusion { get; set; } = false;

    /// <summary>融合最小记忆数。同分类记忆达到此数才触发融合，默认 5</summary>
    [Category("自学习配置")]
    [Description("融合最小记忆数。同分类记忆达到此数才触发融合，默认 5")]
    public Int32 FusionMinMemories { get; set; } = 5;

    /// <summary>启用知识审核。受限和普通用户的低置信度知识需人工审核</summary>
    [Category("自学习配置")]
    [Description("启用知识审核。受限和普通用户的低置信度知识需人工审核")]
    public Boolean EnableKnowledgeReview { get; set; } = false;
    #endregion
}
