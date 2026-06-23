using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Configuration;
using XCode.Configuration;

namespace NewLife.ChatAI;

/// <summary>AI对话系统配置（ChatAI 社区版）。继承 Config 自动加载保存到数据库参数字典表</summary>
[DisplayName("AI对话配置")]
public class ChatSetting : Config<ChatSetting>, IChatSetting
{
    #region 静态
    /// <summary>指向数据库参数字典表</summary>
    static ChatSetting() => Provider = new DbConfigProvider { UserId = 0, Category = "Chat" };
    #endregion

    #region 外观与品牌
    /// <summary>应用名称。显示在 /chat 左上角侧边栏顶部</summary>
    [Category("外观与品牌")]
    [Description("应用名称。显示在 /chat 左上角侧边栏顶部")]
    public String Name { get; set; } = "星语";

    /// <summary>站点标题。显示在浏览器标签页和 /chat 页面顶部</summary>
    [Category("外观与品牌")]
    [Description("站点标题。显示在浏览器标签页和 /chat 页面顶部")]
    public String SiteTitle { get; set; } = "智能助手";

    /// <summary>Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标</summary>
    [Category("外观与品牌")]
    [Description("Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标")]
    public String LogoUrl { get; set; } = "";

    /// <summary>欢迎语。欢迎页大标题，为空时使用前端默认文案"有什么我能帮你的吗？"</summary>
    [Category("外观与品牌")]
    [Description("欢迎语。欢迎页大标题，为空时使用前端默认文案")]
    public String WelcomeMessage { get; set; } = "";

    /// <summary>客服文本。侧边栏或悬浮球中显示的帮助链接文字，如"联系客服"；为空时不展示</summary>
    [Category("外观与品牌")]
    [Description("客服文本。侧边栏或悬浮球中显示的帮助链接文字，如「联系客服」；为空时不展示")]
    public String SupportText { get; set; } = "";

    /// <summary>客服链接。点击客服文本跳转的 URL，支持 https://、mailto: 等</summary>
    [Category("外观与品牌")]
    [Description("客服链接。点击客服文本跳转的 URL，支持 https://、mailto: 等")]
    public String SupportUrl { get; set; } = "";

    /// <summary>客服入口位置。1=侧边栏底部，2=新对话按钮下方，3=右下角悬浮球（默认）</summary>
    [Category("外观与品牌")]
    [Description("客服入口位置。0=不显示，1=侧边栏底部，2=新对话按钮下方，3=右下角悬浮球（默认）")]
    public Int32 SupportPosition { get; set; } = 0;
    #endregion

    #region 对话行为
    /// <summary>全局系统指令。注入每一个用户的每一次对话，置于 System Prompt 首部作为全局行为准则。为空时不注入</summary>
    [Category("对话行为")]
    [Description("全局系统指令。注入每一个用户的每一次对话，置于 System Prompt 首部作为全局行为准则。为空时不注入")]
    public String SystemInstruction { get; set; } = "";

    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    [Category("对话行为")]
    [Description("自动生成标题。首条消息后是否自动生成会话标题")]
    public Boolean AutoGenerateTitle { get; set; } = true;

    /// <summary>默认模型。新用户的默认模型配置Id，0表示自动选择优先级最高的可用文本模型</summary>
    [Category("对话行为")]
    [Description("默认模型。新用户的默认模型配置Id，0表示自动选择优先级最高的可用文本模型")]
    public Int32 DefaultModel { get; set; }

    /// <summary>默认思考模式</summary>
    [Category("对话行为")]
    [Description("默认思考模式。Auto=自动，Think=深度思考，Fast=快速响应")]
    public ThinkingMode DefaultThinkingMode { get; set; } = ThinkingMode.Auto;

    /// <summary>轻量模型编码。简单任务（标题/摘要/蒸馏/记忆提取）使用，为空时自动选择</summary>
    /// <remarks>对应 ModelConfig.Code；自动选择策略：优先级最高的 flash/lite/mini/small 轻量文本模型，没有时回退到主模型。</remarks>
    [Category("对话行为")]
    [Description("轻量模型。标题生成、摘要压缩、知识蒸馏等简单任务调用的模型编码（ModelConfig.Code）；为空时自动选择优先级最高的 flash/lite/mini/small 轻量文本模型，没有时回退到主模型")]
    public String LightweightModel { get; set; } = "";

    /// <summary>嵌入模型编码。向量检索/嵌入场景使用，为空时自动选择</summary>
    /// <remarks>对应 ModelConfig.Code；自动选择策略：优先级最高的嵌入模型（SupportEmbedding=true），没有时退化为本地哈希嵌入。</remarks>
    [Category("对话行为")]
    [Description("嵌入模型。向量检索/向量嵌入场景调用的模型编码（ModelConfig.Code）；为空时自动选择优先级最高的嵌入模型（SupportEmbedding=true），没有时退化为本地哈希嵌入")]
    public String EmbedModel { get; set; } = "";

    /// <summary>重排序模型编码。CrossEncoder 二次精排场景使用，为空时跳过重排步骤</summary>
    /// <remarks>对应 ModelConfig.Code，模型必须支持 IRerankClient 接口（如 gte-rerank）。为空时召回结果直接按 BM25+向量融合分使用，不做 CrossEncoder 重排。</remarks>
    [Category("对话行为")]
    [Description("重排序模型。CrossEncoder 二次精排场景调用的模型编码（ModelConfig.Code）；为空时跳过重排序步骤，直接使用 BM25+向量融合分")]
    public String RerankModel { get; set; } = "";

    /// <summary>图像生成默认尺寸</summary>
    [Category("对话行为")]
    [Description("图像生成默认尺寸")]
    public String DefaultImageSize { get; set; } = "1024x1024";

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    [Category("对话行为")]
    [Description("上下文轮数。每次请求携带的历史对话轮数，默认10")]
    public Int32 DefaultContextRounds { get; set; } = 10;

    /// <summary>后台继续生成。浏览器关闭后模型继续生成</summary>
    [Category("对话行为")]
    [Description("后台继续生成。浏览器关闭后模型继续生成")]
    public Boolean BackgroundGeneration { get; set; } = true;

    /// <summary>用户隔离。启用后向LLM服务商透传User字段（Web=用户ID，Gateway/Channel=AppKey.UserId），用于服务商侧KVCache隔离</summary>
    [Category("对话行为")]
    [Description("用户隔离。启用后向LLM服务商透传User字段（Web=用户ID，Gateway/Channel=AppKey.UserId），用于服务商侧KVCache隔离；关闭时不发送User字段，减少对外信息暴露")]
    public Boolean EnableUserIsolation { get; set; } = false;

    /// <summary>聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制</summary>
    [Category("对话行为")]
    [Description("聊天消息限流。每用户每分钟最大消息发送次数，0 表示不限制")]
    public Int32 MaxMessagesPerMinute { get; set; } = 20;
    #endregion

    #region 附件与分享
    /// <summary>最大附件大小（MB）</summary>
    [Category("附件与分享")]
    [Description("最大附件大小（MB）")]
    public Int32 MaxAttachmentSize { get; set; } = 20;

    /// <summary>单次最多上传附件数</summary>
    [Category("附件与分享")]
    [Description("单次最多上传附件数")]
    public Int32 MaxAttachmentCount { get; set; } = 5;

    /// <summary>允许的文件扩展名</summary>
    [Category("附件与分享")]
    [Description("允许的文件扩展名")]
    public String AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.doc,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv";

    /// <summary>分享有效期（分钟）。共享链接有效时间，0 表示永不过期，默认 30 分钟</summary>
    [Category("附件与分享")]
    [Description("分享有效期（分钟）。共享链接有效时间，0 表示永不过期，默认 30 分钟")]
    public Int32 ShareExpireMinutes { get; set; } = 30;

    /// <summary>允许匿名访问分享。开启后未登录用户也可查看分享链接；关闭后必须登录才能访问</summary>
    [Category("附件与分享")]
    [Description("允许匿名访问分享。开启后未登录用户也可查看分享链接；关闭后必须登录才能访问")]
    public Boolean AllowAnonymousShare { get; set; } = false;
    #endregion

    #region API 网关
    /// <summary>启用 API 网关</summary>
    [Category("API 网关")]
    [Description("启用 API 网关")]
    public Boolean EnableGateway { get; set; } = true;

    /// <summary>网关限流。每分钟每用户最大请求次数</summary>
    [Category("API 网关")]
    [Description("网关限流。每分钟每用户最大请求次数")]
    public Int32 GatewayRateLimit { get; set; } = 60;

    /// <summary>网关对话记录。开启后API网关的对话将同步记录为Conversation和ChatMessage，用于数据分析</summary>
    [Category("API 网关")]
    [Description("网关对话记录。开启后API网关的对话将同步记录为Conversation和ChatMessage，用于数据分析")]
    public Boolean EnableGatewayRecording { get; set; } = false;

    /// <summary>网关处理器链。开启后网关对话与 Web 对话走完全相同的 IChatHandler 链（含知识进化、用户记忆等高级能力），默认关闭（轻量直连 LLM）</summary>
    [Category("API 网关")]
    [Description("网关处理器链。开启后网关对话与 Web 对话走完全相同的 IChatHandler 链（含知识进化、用户记忆等高级能力），默认关闭（轻量直连 LLM）")]
    public Boolean EnableGatewayHandlers { get; set; } = false;
    #endregion

    #region 工具与扩展
    /// <summary>启用函数调用</summary>
    [Category("工具与扩展")]
    [Description("启用函数调用")]
    public Boolean EnableFunctionCalling { get; set; } = true;

    /// <summary>启用 MCP 工具调用</summary>
    [Category("工具与扩展")]
    [Description("启用 MCP 工具调用")]
    public Boolean EnableMcp { get; set; } = true;

    /// <summary>推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型</summary>
    [Category("工具与扩展")]
    [Description("推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型")]
    public Boolean EnableSuggestedQuestionCache { get; set; } = true;

    /// <summary>流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟</summary>
    [Category("工具与扩展")]
    [Description("流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟")]
    public Int32 StreamingSpeed { get; set; } = 3;

    /// <summary>工具仓位上限。每次请求注入完整 Schema 的工具数上限，超出的工具降级为纯文本目录；0 表示不限制，默认15</summary>
    [Category("工具与扩展")]
    [Description("工具仓位上限。每次请求注入完整 Schema 的工具数上限；超出的工具降级为纯文本目录；0 表示不限制，默认15")]
    public Int32 ToolSlotLimit { get; set; } = 15;

    /// <summary>工具结果最大字符数。工具返回结果超过此长度时自动截断并追加摘要提示，0表示不限制，默认80000</summary>
    [Category("工具与扩展")]
    [Description("工具结果最大字符数。工具返回结果超过此长度时自动截断并追加摘要提示，0表示不限制，默认80000")]
    public Int32 ToolResultMaxChars { get; set; } = 80000;

    /// <summary>工具调用最大轮次。防止工具调用无限递归，提升此值可让 Agent 完成需要更多步骤的复杂任务，默认10</summary>
    [Category("工具与扩展")]
    [Description("工具调用最大轮次。防止工具调用无限递归，提升此值可让 Agent 完成需要更多步骤的复杂任务，默认10")]
    public Int32 ToolMaxIterations { get; set; } = 10;

    /// <summary>技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认80000</summary>
    [Category("工具与扩展")]
    [Description("技能内容最大字符数。技能提示词总长度超过此值时按优先级截断，默认80000")]
    public Int32 SkillBudgetChars { get; set; } = 80000;
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    [Category("功能开关")]
    [Description("启用用量统计")]
    public Boolean EnableUsageStats { get; set; } = true;

    #endregion

    #region 知识进化
    /// <summary>启用自动学习。对话结束后异步提取用户记忆，构建用户画像</summary>
    [Category("知识进化")]
    [Description("启用自动学习。对话结束后异步提取用户记忆，构建用户画像")]
    public Boolean EnableAutoLearning { get; set; } = true;

    /// <summary>学习最低字数。用户消息总字数低于该值且仅 1 轮时跳过记忆提取</summary>
    [Category("知识进化")]
    [Description("学习最低字数。用户消息总字数低于该值且仅 1 轮时跳过记忆提取")]
    public Int32 MinLearningContentLength { get; set; } = 50;
    #endregion
}
