using System.ComponentModel;
using NewLife.Configuration;

namespace NewLife.ChatAI.Services;

/// <summary>AI对话系统配置。继承 Config 自动加载保存到 Config/ChatSetting.config</summary>
[DisplayName("AI对话配置")]
public class ChatSetting : Config<ChatSetting>
{
    #region 属性
    /// <summary>分享有效期。共享链接有效天数，0 表示永不过期</summary>
    [Description("分享有效期。共享链接有效天数，0 表示永不过期")]
    public Int32 ShareExpireDays { get; set; } = 30;

    /// <summary>默认模型。新用户的默认模型编码</summary>
    [Description("默认模型。新用户的默认模型编码")]
    public String DefaultModel { get; set; } = "qwen-max";

    /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
    [Description("默认思考模式。Auto=0, Think=1, Fast=2")]
    public Int32 DefaultThinkingMode { get; set; }

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    [Description("上下文轮数。每次请求携带的历史对话轮数，默认10")]
    public Int32 DefaultContextRounds { get; set; } = 10;

    /// <summary>最大附件大小（MB）</summary>
    [Description("最大附件大小（MB）")]
    public Int32 MaxAttachmentSize { get; set; } = 20;

    /// <summary>单次最多上传附件数</summary>
    [Description("单次最多上传附件数")]
    public Int32 MaxAttachmentCount { get; set; } = 5;

    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    [Description("自动生成标题。首条消息后是否自动生成会话标题")]
    public Boolean AutoGenerateTitle { get; set; } = true;

    /// <summary>标题生成提示词模板</summary>
    [Description("标题生成提示词模板")]
    public String TitlePrompt { get; set; } = "请用10个字以内为以下对话生成一个简短标题，只输出标题文字，不要加任何标点和引号：";

    /// <summary>允许的文件扩展名</summary>
    [Description("允许的文件扩展名")]
    public String AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.txt,.md,.csv";

    /// <summary>启用 API 网关</summary>
    [Description("启用 API 网关")]
    public Boolean EnableGateway { get; set; } = true;

    /// <summary>网关限流。每分钟每用户最大请求次数</summary>
    [Description("网关限流。每分钟每用户最大请求次数")]
    public Int32 GatewayRateLimit { get; set; } = 60;

    /// <summary>上游重试次数。模型返回 429 时最大重试</summary>
    [Description("上游重试次数。模型返回 429 时最大重试")]
    public Int32 UpstreamRetryCount { get; set; } = 5;

    /// <summary>启用函数调用</summary>
    [Description("启用函数调用")]
    public Boolean EnableFunctionCalling { get; set; } = true;

    /// <summary>启用 MCP 工具调用</summary>
    [Description("启用 MCP 工具调用")]
    public Boolean EnableMcp { get; set; } = true;

    /// <summary>图像生成默认尺寸</summary>
    [Description("图像生成默认尺寸")]
    public String DefaultImageSize { get; set; } = "1024x1024";

    /// <summary>启用用量统计</summary>
    [Description("启用用量统计")]
    public Boolean EnableUsageStats { get; set; } = true;

    /// <summary>后台继续生成。浏览器关闭后模型继续生成</summary>
    [Description("后台继续生成。浏览器关闭后模型继续生成")]
    public Boolean BackgroundGeneration { get; set; } = true;

    /// <summary>推荐问题。新会话页展示的推荐问题，多个用竖线分隔</summary>
    [Description("推荐问题。新会话页展示的推荐问题，多个用竖线分隔")]
    public String SuggestedQuestions { get; set; } = "帮我写一封邮件|解释量子计算|用Python写一个排序算法|帮我翻译一段英文";
    #endregion
}
