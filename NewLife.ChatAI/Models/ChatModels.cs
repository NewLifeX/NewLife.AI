using NewLife.AI.Models;

namespace NewLife.ChatAI.Models;

/// <summary>模型信息</summary>
public record ModelInfoDto(Int32 Id, String Code, String Name, Boolean SupportThinking, Boolean SupportVision, Boolean SupportImageGeneration, Boolean SupportFunctionCalling, String Provider = "");

/// <summary>工具调用信息</summary>
public record ToolCallDto(String Id, String Name, ToolCallStatus Status, String? Arguments = null, String? Result = null);

/// <summary>会话摘要</summary>
public record ConversationSummaryDto(Int64 Id, String Title, Int32 ModelId, DateTime LastMessageTime, Boolean IsPinned)
{
    /// <summary>会话图标</summary>
    public String? Icon { get; set; }

    /// <summary>图标颜色</summary>
    public String? IconColor { get; set; }
};

/// <summary>消息数据</summary>
public record MessageDto(Int64 Id, Int64 ConversationId, String Role, String Content, String? ThinkingContent, ThinkingMode ThinkingMode, String? Attachments, DateTime CreateTime)
{
    /// <summary>消息状态</summary>
    public MessageStatus Status { get; set; } = MessageStatus.Done;

    /// <summary>工具调用列表</summary>
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; set; }

    /// <summary>提示Token数</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>回复Token数</summary>
    public Int32 CompletionTokens { get; set; }

    /// <summary>总Token数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>反馈类型。Like=1, Dislike=2, 0=无反馈</summary>
    public Int32 FeedbackType { get; set; }
};

/// <summary>附件信息</summary>
public record AttachmentInfoDto(Int64 Id, String FileName, Int64 Size, String Url, Boolean IsImage);

/// <summary>分页结果</summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, Int32 Total, Int32 Page, Int32 PageSize);

/// <summary>消息搜索结果</summary>
public record MessageSearchResultDto
{
    /// <summary>消息编号</summary>
    public String Id { get; set; } = null!;

    /// <summary>会话编号</summary>
    public String ConversationId { get; set; } = null!;

    /// <summary>会话标题</summary>
    public String ConversationTitle { get; set; } = "";

    /// <summary>角色</summary>
    public String Role { get; set; } = "user";

    /// <summary>消息内容</summary>
    public String Content { get; set; } = "";

    /// <summary>创建时间</summary>
    public String CreateTime { get; set; } = "";
}

/// <summary>分享链接</summary>
public record ShareLinkDto(String Url, DateTime CreateTime, DateTime? ExpireTime);

/// <summary>用户设置</summary>
public record UserSettingsDto(String Language, String Theme, Int32 FontSize, String SendShortcut, Int32 DefaultModel, ThinkingMode DefaultThinkingMode, Int32 ContextRounds, String SystemPrompt)
{
    /// <summary>是否启用 MCP</summary>
    public Boolean McpEnabled { get; set; } = true;

    /// <summary>流式输出速度</summary>
    public Int32 StreamingSpeed { get; set; } = 3;

    /// <summary>允许用于模型训练改进</summary>
    public Boolean AllowTraining { get; set; }
};

/// <summary>用户资料</summary>
public record UserProfileDto(String Nickname, String Account, String? Avatar);

/// <summary>系统公开配置。前端初始化时无需登录即可拉取</summary>
public class SystemConfigDto
{
    /// <summary>应用名称，显示在侧边栏左上角</summary>
    public String AppName { get; set; }
    /// <summary>站点标题，显示在浏览器标签和 /chat 页面</summary>
    public String SiteTitle { get; set; }
    /// <summary>欢迎页推荐问题列表</summary>
    public SuggestedQuestionDto[] SuggestedQuestions { get; set; }
}

/// <summary>推荐问题DTO</summary>
public class SuggestedQuestionDto
{
    /// <summary>问题内容</summary>
    public String Question { get; set; }
    /// <summary>图标</summary>
    public String Icon { get; set; }
    /// <summary>颜色</summary>
    public String Color { get; set; }
}

/// <summary>用量汇总</summary>
public class UsageSummaryDto
{
    /// <summary>会话数</summary>
    public Int32 Conversations { get; set; }
    /// <summary>消息数</summary>
    public Int32 Messages { get; set; }
    /// <summary>提示Token数</summary>
    public Int64 PromptTokens { get; set; }
    /// <summary>回复Token数</summary>
    public Int64 CompletionTokens { get; set; }
    /// <summary>总Token数</summary>
    public Int64 TotalTokens { get; set; }
    /// <summary>最后活跃时间</summary>
    public DateTime? LastActiveTime { get; set; }
}

/// <summary>每日用量</summary>
public class DailyUsageDto
{
    /// <summary>日期（yyyy-MM-dd）</summary>
    public String Date { get; set; } = String.Empty;
    /// <summary>调用次数</summary>
    public Int32 Calls { get; set; }
    /// <summary>提示Token数</summary>
    public Int64 PromptTokens { get; set; }
    /// <summary>回复Token数</summary>
    public Int64 CompletionTokens { get; set; }
    /// <summary>总Token数</summary>
    public Int64 TotalTokens { get; set; }
}

/// <summary>模型使用分布</summary>
public class ModelUsageDto
{
    /// <summary>模型编号</summary>
    public Int32 ModelId { get; set; }
    /// <summary>调用次数</summary>
    public Int32 Calls { get; set; }
    /// <summary>总Token数</summary>
    public Int64 TotalTokens { get; set; }
}

/// <summary>MCP 服务器信息</summary>
public class McpServerDto
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }
    /// <summary>名称</summary>
    public String Name { get; set; } = String.Empty;
    /// <summary>服务端点</summary>
    public String Endpoint { get; set; } = String.Empty;
    /// <summary>传输类型（sse/stdio）</summary>
    public String TransportType { get; set; } = String.Empty;
    /// <summary>认证类型</summary>
    public String AuthType { get; set; } = String.Empty;
    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; }
    /// <summary>排序</summary>
    public Int32 Sort { get; set; }
    /// <summary>备注</summary>
    public String? Remark { get; set; }
}
