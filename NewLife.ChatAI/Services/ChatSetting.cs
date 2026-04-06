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

    /// <summary>Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标</summary>
    [Category("基本配置")]
    [Description("Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标")]
    public String LogoUrl { get; set; } = "";

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

    /// <summary>推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型</summary>
    [Category("工具与能力")]
    [Description("推荐问题缓存。开启后用户提问命中推荐问题且缓存有效（当天更新）时，直接返回缓存响应而不请求大模型")]
    public Boolean EnableSuggestedQuestionCache { get; set; } = false;

    /// <summary>流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟</summary>
    [Category("工具与能力")]
    [Description("流式输出速度。缓存命中时的分块节流等级，1~5，默认3（约500字/秒）；超过5时直接一次性输出全部内容，不做延迟")]
    public Int32 StreamingSpeed { get; set; } = 3;

    /// <summary>工具渐进式发现阈值。工具总数超过此值时切换为Advertise模式，仅向模型展示工具摘要而非完整Schema，模型按需加载，默认15</summary>
    [Category("工具与能力")]
    [Description("工具渐进式发现阈值。工具总数超过此值时切换为Advertise模式，仅向模型展示工具摘要而非完整Schema，模型按需加载，默认15")]
    public Int32 ToolAdvertiseThreshold { get; set; } = 15;
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

    #endregion

    #region 自学习
    /// <summary>启用自动学习。开启后对话结束时自动提取用户记忆</summary>
    [Category("自学习")]
    [Description("启用自动学习。开启后对话结束时自动提取用户记忆")]
    public Boolean EnableAutoLearning { get; set; }

    /// <summary>学习分析模型。用于记忆提取的模型编码，为空时自动选择轻量模型（mini/flash/lite）</summary>
    [Category("自学习")]
    [Description("学习分析模型。用于记忆提取的模型编码，为空时自动选择轻量模型（mini/flash/lite）")]
    public String LearningModel { get; set; } = "";

    #endregion
}
