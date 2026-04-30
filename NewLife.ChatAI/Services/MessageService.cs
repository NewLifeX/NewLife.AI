using NewLife.ChatAI.Helpers;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>消息生成服务（ChatAI 社区版）。直接继承 <see cref="MessageFlow"/> 模板方法基类，
/// 仅在基类降级实现上补充 <b>NewLife.Office 文档解析</b> 两项增强。</summary>
/// <remarks>
/// <para>核心流程（Validate → Prepare → Execute → Persist → PostProcess 五段式模板、4 大 public 入口）完全由基类提供，本类只需 override 扩展点。</para>
/// <para>业务数据模型 <see cref="MessageDto"/> / <see cref="SendMessageRequest"/> / <see cref="ToolCallDto"/> 位于 <c>NewLife.ChatAI.Models</c>。</para>
/// </remarks>
/// <param name="modelService">模型服务（用于模型解析和客户端创建）</param>
/// <param name="backgroundService">后台生成服务</param>
/// <param name="setting">AI对话系统配置</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class MessageService(ModelService modelService, BackgroundGenerationService? backgroundService, ChatSetting setting, ITracer tracer, ILog log)
    : MessageFlow(modelService, backgroundService, setting, tracer, log)
{
    #region 覆盖：完整多模态（图片 + Office 文档）

    /// <inheritdoc />
    protected override AiChatMessage? BuildHistoryMessage(DbChatMessage msg)
    {
        if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            return AttachmentContentBuilder.Build(msg.Attachments, msg.Content);
        return base.BuildHistoryMessage(msg);
    }

    #endregion
}
