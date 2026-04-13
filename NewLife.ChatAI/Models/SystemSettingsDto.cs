namespace NewLife.ChatAI.Models;

/// <summary>系统设置 DTO（读取）。包含 ChatSetting 开源版可配置项，供前端系统设置页展示和编辑</summary>
public class SystemSettingsDto
{
    #region 基本配置
    /// <summary>应用名称。显示在 /chat 左上角侧边栏顶部</summary>
    public String Name { get; set; } = "";

    /// <summary>站点标题。显示在浏览器标签页和 /chat 页面顶部</summary>
    public String SiteTitle { get; set; } = "";

    /// <summary>Logo地址。欢迎页自定义Logo图片URL，为空时显示默认图标</summary>
    public String LogoUrl { get; set; } = "";

    /// <summary>自动生成标题。首条消息后是否自动生成会话标题</summary>
    public Boolean AutoGenerateTitle { get; set; }
    #endregion

    #region 对话默认
    /// <summary>默认模型配置Id，0=第一个可用模型</summary>
    public Int32 DefaultModel { get; set; }

    /// <summary>默认思考模式</summary>
    public Int32 DefaultThinkingMode { get; set; }

    /// <summary>上下文轮数。默认10</summary>
    public Int32 DefaultContextRounds { get; set; }
    #endregion

    #region 上传与分享
    /// <summary>最大附件大小（MB）</summary>
    public Int32 MaxAttachmentSize { get; set; }

    /// <summary>单次最多上传附件数</summary>
    public Int32 MaxAttachmentCount { get; set; }

    /// <summary>允许的文件扩展名</summary>
    public String AllowedExtensions { get; set; } = "";

    /// <summary>图像生成默认尺寸</summary>
    public String DefaultImageSize { get; set; } = "";

    /// <summary>分享有效期（天），0=永不过期</summary>
    public Int32 ShareExpireDays { get; set; }
    #endregion

    #region API 网关
    /// <summary>启用 API 网关</summary>
    public Boolean EnableGateway { get; set; }

    /// <summary>API网关管道增强</summary>
    public Boolean EnableGatewayPipeline { get; set; }

    /// <summary>网关限流（每分钟每用户）</summary>
    public Int32 GatewayRateLimit { get; set; }

    /// <summary>上游重试次数</summary>
    public Int32 UpstreamRetryCount { get; set; }

    /// <summary>网关对话记录</summary>
    public Boolean EnableGatewayRecording { get; set; }
    #endregion

    #region 工具与能力
    /// <summary>启用函数调用</summary>
    public Boolean EnableFunctionCalling { get; set; }

    /// <summary>启用 MCP 工具调用</summary>
    public Boolean EnableMcp { get; set; }

    /// <summary>推荐问题缓存</summary>
    public Boolean EnableSuggestedQuestionCache { get; set; }

    /// <summary>流式输出速度（1~5）</summary>
    public Int32 StreamingSpeed { get; set; }

    /// <summary>工具渐进式发现阈值</summary>
    public Int32 ToolAdvertiseThreshold { get; set; }
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    public Boolean EnableUsageStats { get; set; }

    /// <summary>后台继续生成</summary>
    public Boolean BackgroundGeneration { get; set; }

    /// <summary>聊天消息限流（每用户每分钟）</summary>
    public Int32 MaxMessagesPerMinute { get; set; }
    #endregion

    #region 自学习
    /// <summary>启用自动学习</summary>
    public Boolean EnableAutoLearning { get; set; }

    /// <summary>学习分析模型</summary>
    public String LearningModel { get; set; } = "";

    /// <summary>学习最低字数</summary>
    public Int32 MinLearningContentLength { get; set; }
    #endregion

    /// <summary>可用模型列表。供 DefaultModel 下拉使用</summary>
    public ModelOptionDto[] Models { get; set; } = [];
}

/// <summary>系统设置更新 DTO（写入）</summary>
public class SystemSettingsUpdateDto
{
    #region 基本配置
    /// <summary>应用名称</summary>
    public String? Name { get; set; }

    /// <summary>站点标题</summary>
    public String? SiteTitle { get; set; }

    /// <summary>Logo地址</summary>
    public String? LogoUrl { get; set; }

    /// <summary>自动生成标题</summary>
    public Boolean? AutoGenerateTitle { get; set; }
    #endregion

    #region 对话默认
    /// <summary>默认模型</summary>
    public Int32? DefaultModel { get; set; }

    /// <summary>默认思考模式</summary>
    public Int32? DefaultThinkingMode { get; set; }

    /// <summary>上下文轮数</summary>
    public Int32? DefaultContextRounds { get; set; }
    #endregion

    #region 上传与分享
    /// <summary>最大附件大小（MB）</summary>
    public Int32? MaxAttachmentSize { get; set; }

    /// <summary>单次最多上传附件数</summary>
    public Int32? MaxAttachmentCount { get; set; }

    /// <summary>允许的文件扩展名</summary>
    public String? AllowedExtensions { get; set; }

    /// <summary>图像生成默认尺寸</summary>
    public String? DefaultImageSize { get; set; }

    /// <summary>分享有效期（天）</summary>
    public Int32? ShareExpireDays { get; set; }
    #endregion

    #region API 网关
    /// <summary>启用 API 网关</summary>
    public Boolean? EnableGateway { get; set; }

    /// <summary>API网关管道增强</summary>
    public Boolean? EnableGatewayPipeline { get; set; }

    /// <summary>网关限流</summary>
    public Int32? GatewayRateLimit { get; set; }

    /// <summary>上游重试次数</summary>
    public Int32? UpstreamRetryCount { get; set; }

    /// <summary>网关对话记录</summary>
    public Boolean? EnableGatewayRecording { get; set; }
    #endregion

    #region 工具与能力
    /// <summary>启用函数调用</summary>
    public Boolean? EnableFunctionCalling { get; set; }

    /// <summary>启用 MCP 工具调用</summary>
    public Boolean? EnableMcp { get; set; }

    /// <summary>推荐问题缓存</summary>
    public Boolean? EnableSuggestedQuestionCache { get; set; }

    /// <summary>流式输出速度（1~5）</summary>
    public Int32? StreamingSpeed { get; set; }

    /// <summary>工具渐进式发现阈值</summary>
    public Int32? ToolAdvertiseThreshold { get; set; }
    #endregion

    #region 功能开关
    /// <summary>启用用量统计</summary>
    public Boolean? EnableUsageStats { get; set; }

    /// <summary>后台继续生成</summary>
    public Boolean? BackgroundGeneration { get; set; }

    /// <summary>聊天消息限流</summary>
    public Int32? MaxMessagesPerMinute { get; set; }
    #endregion

    #region 自学习
    /// <summary>启用自动学习</summary>
    public Boolean? EnableAutoLearning { get; set; }

    /// <summary>学习分析模型</summary>
    public String? LearningModel { get; set; }

    /// <summary>学习最低字数</summary>
    public Int32? MinLearningContentLength { get; set; }
    #endregion
}

/// <summary>模型选项。用于系统设置页默认模型下拉列表</summary>
/// <param name="Id">模型配置Id</param>
/// <param name="Name">显示名称</param>
public record ModelOptionDto(Int32 Id, String Name);
