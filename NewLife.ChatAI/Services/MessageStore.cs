namespace NewLife.ChatAI.Services;

/// <summary>消息实体落库助手。统一对话流各入口的消息创建/软删持久化细节，
/// 消除 <see cref="MessageFlow"/> 四个入口间复制粘贴，供各来源（Web/Gateway/Channel/Schedule）消息流复用</summary>
/// <remarks>
/// <para>本类只封装"实体创建与落库"的公共形态，不承载业务决策：
/// 正文写入由 <c>PersistMessageHandler</c> / 后台兜底完成，占位消息只预分配行。</para>
/// <para>实体类型通过各程序集全局别名（NAI/StarChat 各自的 <c>DbChatMessage</c>）解析到本产品实体，
/// 与 <see cref="MessageFlow"/> 的解析方式一致，文件可被两仓源码链接共用。</para>
/// </remarks>
public static class MessageStore
{
    /// <summary>创建并插入一条用户消息</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="content">消息正文</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <param name="modelName">模型代码（可空）</param>
    /// <param name="attachmentsJson">附件 JSON（可空，空字符串等价于 null）</param>
    /// <returns>已插入的用户消息实体</returns>
    public static DbChatMessage CreateUser(Int64 conversationId, String content, ThinkingMode thinkingMode, String? modelName, String? attachmentsJson = null)
    {
        var msg = new DbChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = content,
            ThinkingMode = thinkingMode,
            ModelName = modelName,
            Enable = true,
        };
        if (!attachmentsJson.IsNullOrEmpty()) msg.Attachments = attachmentsJson;
        msg.Insert();

        return msg;
    }

    /// <summary>创建并插入助手消息占位。正文与用量由 <c>PersistMessageHandler</c> 或后台兜底事后写入</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <param name="modelName">模型代码（可空，重生成/编辑等入口沿用旧消息时不传）</param>
    /// <returns>已插入的助手消息实体</returns>
    public static DbChatMessage CreateAssistant(Int64 conversationId, ThinkingMode thinkingMode, String? modelName = null)
    {
        var msg = new DbChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = thinkingMode,
            ModelName = modelName,
            Enable = true,
        };
        msg.Insert();

        return msg;
    }

    /// <summary>软删除消息（置 Enable=false 保留历史版本）。重生成时旧回复行保留供审计与回看</summary>
    /// <param name="msg">消息实体</param>
    public static void SoftDelete(DbChatMessage msg)
    {
        if (msg == null) return;

        msg.Enable = false;
        msg.Update();
    }
}
