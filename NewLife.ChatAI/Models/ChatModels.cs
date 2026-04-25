using NewLife.AI.Models;

namespace NewLife.ChatAI.Models;

/// <summary>模型信息</summary>
public record ModelInfoDto(Int32 Id, String Code, String Name, Boolean SupportThinking, Boolean SupportFunctionCalling, Boolean SupportVision, Boolean SupportAudio, Boolean SupportImageGeneration, Boolean SupportVideoGeneration, Int32 ContextLength = 0, String Provider = "");

#region 提供商管理 DTO
/// <summary>提供商配置（管理视图）。ApiKey 已脱敏</summary>
public class ProviderDto
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }
    /// <summary>编码</summary>
    public String Code { get; set; } = "";
    /// <summary>名称</summary>
    public String Name { get; set; } = "";
    /// <summary>实现类描述符（如 OpenAI、Anthropic）</summary>
    public String Provider { get; set; } = "";
    /// <summary>接口地址</summary>
    public String Endpoint { get; set; } = "";
    /// <summary>脱敏密钥（前4位***后4位，无密钥时为空）</summary>
    public String ApiKeyMasked { get; set; } = "";
    /// <summary>启用</summary>
    public Boolean Enable { get; set; }
    /// <summary>排序</summary>
    public Int32 Sort { get; set; }
    /// <summary>备注</summary>
    public String Remark { get; set; } = "";
}

/// <summary>提供商更新请求。仅允许修改 Enable/ApiKey/Remark 三个字段</summary>
public class ProviderUpdateDto
{
    /// <summary>启用</summary>
    public Boolean Enable { get; set; }
    /// <summary>新密钥，为空时保持不变</summary>
    public String? ApiKey { get; set; }
    /// <summary>备注</summary>
    public String? Remark { get; set; }
}
#endregion

#region 模型管理 DTO
/// <summary>模型配置（管理视图）。含提供商名称及启停状态</summary>
public class ModelManageDto
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }
    /// <summary>所属提供商 ID</summary>
    public Int32 ProviderId { get; set; }
    /// <summary>所属提供商名称</summary>
    public String ProviderName { get; set; } = "";
    /// <summary>模型编码</summary>
    public String Code { get; set; } = "";
    /// <summary>显示名称</summary>
    public String Name { get; set; } = "";
    /// <summary>启用</summary>
    public Boolean Enable { get; set; }
    /// <summary>排序</summary>
    public Int32 Sort { get; set; }
    /// <summary>上下文长度（Token）</summary>
    public Int32 ContextLength { get; set; }
    /// <summary>支持思考模式</summary>
    public Boolean SupportThinking { get; set; }
    /// <summary>支持函数调用</summary>
    public Boolean SupportFunctionCalling { get; set; }
    /// <summary>支持视觉（图片输入）</summary>
    public Boolean SupportVision { get; set; }
    /// <summary>支持音频</summary>
    public Boolean SupportAudio { get; set; }
    /// <summary>支持文生图</summary>
    public Boolean SupportImageGeneration { get; set; }
    /// <summary>支持文生视频</summary>
    public Boolean SupportVideoGeneration { get; set; }
}

/// <summary>模型设置更新请求。含 Enable 及特性标记</summary>
public class ModelSettingsDto
{
    /// <summary>启用</summary>
    public Boolean Enable { get; set; }
    /// <summary>上下文长度（Token）</summary>
    public Int32 ContextLength { get; set; }
    /// <summary>支持思考模式</summary>
    public Boolean SupportThinking { get; set; }
    /// <summary>支持函数调用</summary>
    public Boolean SupportFunctionCalling { get; set; }
    /// <summary>支持视觉（图片输入）</summary>
    public Boolean SupportVision { get; set; }
    /// <summary>支持音频</summary>
    public Boolean SupportAudio { get; set; }
    /// <summary>支持文生图</summary>
    public Boolean SupportImageGeneration { get; set; }
    /// <summary>支持文生视频</summary>
    public Boolean SupportVideoGeneration { get; set; }
}
#endregion

/// <summary>会话摘要</summary>
public record ConversationSummaryDto(Int64 Id, String Title, Int32 ModelId, DateTime LastMessageTime, Boolean IsPinned)
{
    /// <summary>会话图标</summary>
    public String? Icon { get; set; }

    /// <summary>图标颜色</summary>
    public String? IconColor { get; set; }
};

/// <summary>附件信息</summary>
public record AttachmentInfoDto(Int64 Id, String FileName, Int64 Size, String Url, Boolean IsImage);

/// <summary>分页结果</summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, Int32 Total, Int32 Page, Int32 PageSize);

/// <summary>分享链接</summary>
public record ShareLinkDto(String Url, DateTime CreateTime, DateTime? ExpireTime);

/// <summary>用户设置</summary>
public record UserSettingsDto(String Language, String Theme, Int32 FontSize, String SendShortcut, Int32 DefaultModel, ThinkingMode DefaultThinkingMode, Int32 ContextRounds, String Nickname, String UserBackground, ResponseStyle ResponseStyle, String SystemPrompt, Boolean AllowTraining)
{
    /// <summary>是否启用 MCP</summary>
    public Boolean McpEnabled { get; set; } = true;

    /// <summary>显示工具调用。是否在对话中显示工具调用的入参和出参详情</summary>
    public Boolean ShowToolCalls { get; set; }

    /// <summary>默认技能</summary>
    public String DefaultSkill { get; set; } = "general";

    /// <summary>启用个人学习。用户级自学习开关，全局开关开启后此项生效</summary>
    public Boolean EnableLearning { get; set; } = true;


    /// <summary>内容区宽度。小屏800/标准960/宽屏1200，按范围匹配：&lt;960 小屏，&gt;=1200 宽屏，其余标准。0表示未设置，等效标准屏</summary>
    public Int32 ContentWidth { get; set; } = 0;
};

/// <summary>用户角色信息</summary>
/// <param name="Name">角色名称</param>
/// <param name="IsSystem">是否系统角色</param>
public record UserRoleDto(String Name, Boolean IsSystem);

/// <summary>用户资料</summary>
public record UserProfileDto(
    String Nickname,
    String Account,
    String? Avatar,
    String? Role = null,
    String? Department = null,
    String? Email = null,
    String? Mobile = null,
    String? Remark = null,
    UserRoleDto[]? Roles = null
);

/// <summary>系统公开配置。前端初始化时无需登录即可拉取</summary>
public class SystemConfigDto
{
    /// <summary>应用名称，显示在侧边栏左上角</summary>
    public String AppName { get; set; } = "";

    /// <summary>站点标题，显示在浏览器标签和 /chat 页面</summary>
    public String SiteTitle { get; set; } = "";

    /// <summary>Logo地址。欢迎页自定义Logo图片URL</summary>
    public String? LogoUrl { get; set; }

    /// <summary>欢迎语。欢迎页大标题，为空时前端使用默认文案</summary>
    public String? WelcomeMessage { get; set; }

    /// <summary>欢迎页推荐问题列表</summary>
    public SuggestedQuestionDto[] SuggestedQuestions { get; set; } = [];
}

/// <summary>推荐问题信息</summary>
public class SuggestedQuestionDto
{
    /// <summary>标题。欢迎页按钮显示的短标题</summary>
    public String? Title { get; set; }

    /// <summary>问题文本</summary>
    public String Question { get; set; } = "";

    /// <summary>Material 图标名称</summary>
    public String? Icon { get; set; }

    /// <summary>Tailwind 颜色类名</summary>
    public String? Color { get; set; }
}

/// <summary>消息搜索结果</summary>
public record MessageSearchResultDto
{
    /// <summary>消息编号</summary>
    public Int64 Id { get; set; }

    /// <summary>所属会话编号</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>所属会话标题</summary>
    public String ConversationTitle { get; set; } = "";

    /// <summary>消息角色</summary>
    public String Role { get; set; } = "user";

    /// <summary>消息内容</summary>
    public String Content { get; set; } = "";

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}
