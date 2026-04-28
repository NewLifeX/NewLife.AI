using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>系统提示词处理器。在调用模型前，将技能提示词注入 system 消息、解析 @工具引用</summary>
/// <remarks>
/// <para>事前职责：调用 <see cref="IChatPipeline.PrepareContext"/> 完成技能注入与工具集合解析；
/// 把构建完成的 system 文本写入 <see cref="IChatContext.SystemPrompt"/>，供持久化层使用。</para>
/// <para>事后无操作。短路场景：技能服务未注册时直接 next。</para>
/// <para>本 Handler 当前作为薄包装委托既有 <see cref="IChatPipeline"/>；后续 Step 5 将逻辑内联，
/// 届时再删除对 IChatPipeline 的依赖。</para>
/// </remarks>
/// <param name="pipeline">既有对话管道（提供 PrepareContext 实现）</param>
/// <param name="tracer">追踪器</param>
public class SystemPromptHandler(IChatPipeline pipeline, ITracer tracer) : IChatHandler
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (var span = tracer?.NewSpan("handler:SystemPrompt"))
        {
            // 适配桥接：当前 IChatPipeline 仍以 ChatPipelineContext 工作；通过 MessageFlowContext 取出复用
            if (context is MessageFlowContext flow)
            {
                pipeline.PrepareContext(context.ContextMessages, flow.PipelineContext);

                // 同步解析结果到 IChatContext 字段（Step 5 后字段统一，可去掉）
                if (flow.PipelineContext.SystemPrompt != null) context.SystemPrompt = flow.PipelineContext.SystemPrompt;
                foreach (var t in flow.PipelineContext.SelectedTools) context.SelectedTools.Add(t);
                foreach (var s in flow.PipelineContext.ResolvedSkillNames) context.ResolvedSkillNames.Add(s);
            }
        }

        await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }
    }
}
