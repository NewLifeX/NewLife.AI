using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatData.Entity;
using NewLife.ChatData.Models;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Serialization;
using XCode.Membership;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using AiFunctionCall = NewLife.AI.Models.FunctionCall;
using AiToolCall = NewLife.AI.Models.ToolCall;
using ChatMessage = NewLife.ChatData.Entity.ChatMessage;
using ILog = NewLife.Log.ILog;

namespace NewLife.ChatData.Services;

/// <summary>消息生成流程基类。承载 <b>Validate → Prepare → Execute → Persist → PostProcess</b> 五段式模板方法，
/// 对外暴露 4 大入口：<see cref="StreamMessageAsync"/> / <see cref="RegenerateMessageAsync"/> /
/// <see cref="RegenerateStreamAsync"/> / <see cref="EditAndResendStreamAsync"/></summary>
/// <remarks>
/// <para>
/// 所有关键步骤方法均为 <c>protected virtual</c>，派生类（ChatAI 社区版 / StarChat 商用版）可按需覆盖以注入差异化逻辑。
/// 能力扩展（工具调用、技能注入）与知识进化（记忆、自学习）通过 <see cref="IChatPipeline"/> 透明接入。
/// </para>
/// <para>
/// 本基类提供 <b>简化版</b> 系统提示词与多模态构建，仅依赖 NewLife.Core / XCode / ChatData 实体，
/// 不引入 Cube.Entity.Department 与 NewLife.Office 等上层依赖；派生类按需增强。
/// </para>
/// </remarks>
public class MessageFlow
{
    #region 字段

    /// <summary>对话执行管道</summary>
    protected readonly IChatPipeline Pipeline;

    /// <summary>模型服务（模型解析与客户端创建）</summary>
    protected readonly ModelService ModelService;

    /// <summary>后台生成服务（支持断线重连恢复）</summary>
    protected readonly BackgroundGenerationService? BackgroundService;

    /// <summary>用量统计服务</summary>
    protected readonly UsageService? UsageService;

    /// <summary>追踪器</summary>
    protected readonly ITracer? Tracer;

    /// <summary>日志</summary>
    protected readonly ILog? Log;

    /// <summary>上下文增强器链（DI 注册的 <see cref="IContextEnricher"/>，按 Order 升序执行）</summary>
    protected readonly IReadOnlyList<IContextEnricher> Enrichers;

    /// <summary>消息流后处理器链（DI 注册的 <see cref="IMessageFlowPostProcessor"/>，按 Order 升序执行）</summary>
    protected readonly IReadOnlyList<IMessageFlowPostProcessor> PostProcessors;

    /// <summary>AI对话配置</summary>
    protected readonly IChatSetting Setting;

    #endregion

    #region 构造

    /// <summary>实例化消息流基类</summary>
    /// <param name="pipeline">对话执行管道</param>
    /// <param name="modelService">模型服务</param>
    /// <param name="backgroundService">后台生成服务</param>
    /// <param name="usageService">用量统计服务</param>
    /// <param name="tracer">追踪器</param>
    /// <param name="log">日志</param>
    /// <param name="enrichers">上下文增强器链（可选）</param>
    /// <param name="postProcessors">消息流后处理器链（可选）</param>
    public MessageFlow(IChatPipeline pipeline, ModelService modelService, BackgroundGenerationService? backgroundService, UsageService? usageService, IChatSetting setting, ITracer? tracer, ILog? log, IEnumerable<IContextEnricher>? enrichers = null, IEnumerable<IMessageFlowPostProcessor>? postProcessors = null)
    {
        Pipeline = pipeline;
        ModelService = modelService;
        BackgroundService = backgroundService;
        UsageService = usageService;
        Setting = setting;
        Tracer = tracer;
        Log = log;
        Enrichers = enrichers?.OrderBy(e => e.Order).ToArray() ?? [];
        PostProcessors = postProcessors?.OrderBy(p => p.Order).ToArray() ?? [];
    }

    #endregion

    #region 生成入口

    /// <summary>非流式重新生成 AI 回复。构建上下文后委托管道完成，结果直接写回消息记录</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的消息 DTO，失败时返回 null</returns>
    public virtual async Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        // Step1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "assistant", null, null, userId);
        flow.Kind = FlowKind.Regenerate;
        flow.OriginalMessageId = messageId;
        if (flow.Error != null) return null;

        try
        {
            // Step2: 构建对话上下文
            await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

            // Step3: 初始化管道上下文 + 执行 Enricher 链 + 执行
            InitPipelineContext(flow);
            await InvokeContextEnrichersAsync(flow, cancellationToken).ConfigureAwait(false);
            var sw = Stopwatch.StartNew();
            var response = await Pipeline.CompleteAsync(flow.ContextMessages, flow.ModelConfig, flow.PipelineContext, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            // Step4: 持久化结果
            PersistCompleteResult(flow, response, (Int32)sw.ElapsedMilliseconds);

            // Step5: 后处理链
            await InvokePostProcessorsAsync(flow, cancellationToken).ConfigureAwait(false);

            return ToMessageDto(flow.AssistantMessage);
        }
        catch (Exception ex)
        {
            DefaultSpan.Current?.SetError(ex);
            Log?.Error("重新生成回复失败: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>编辑用户消息并流式重新发送。依次：更新消息内容 → 删除后续所有消息 → 构建上下文 → 委托管道生成</summary>
    /// <param name="messageId">用户消息编号</param>
    /// <param name="newContent">编辑后的内容</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> EditAndResendStreamAsync(Int64 messageId, String newContent, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "user", null, null, userId);
        flow.Kind = FlowKind.EditAndResendStream;
        flow.OriginalMessageId = messageId;
        flow.NewUserContent = newContent;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        // 更新消息内容
        flow.UserMessage!.Content = newContent;
        flow.UserMessage.Update();

        // 预分配 AI 回复消息
        var assistantMsg = new ChatMessage
        {
            ConversationId = flow.UserMessage.ConversationId,
            Role = "assistant",
            ThinkingMode = flow.UserMessage.ThinkingMode,
        };
        assistantMsg.Insert();
        flow.AssistantMessage = assistantMsg;

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, newContent, cancellationToken).ConfigureAwait(false);

        yield return ChatStreamEvent.MessageStart(flow.AssistantMessage.Id, flow.ModelConfig.Code ?? String.Empty, flow.UserMessage!.ThinkingMode);

        // Step3: 初始化管道上下文 + 执行 Enricher 链 + 执行流式生成
        InitPipelineContext(flow);
        await InvokeContextEnrichersAsync(flow, cancellationToken).ConfigureAwait(false);
        await foreach (var ev in ExecuteStreamAsync(flow, flow.ContextMessages, cancellationToken).ConfigureAwait(false))
            yield return ev;

        // Step4: 持久化结果
        PersistStreamResult(flow);

        // Step5: 后处理链
        await InvokePostProcessorsAsync(flow, cancellationToken).ConfigureAwait(false);

        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
            yield return new ChatStreamEvent { Type = "message_done", MessageId = flow.AssistantMessage.Id, Usage = flow.Usage, };
    }

    /// <summary>流式重新生成 AI 回复。替换当前 AI 回复并通过 SSE 事件流返回新内容</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> RegenerateStreamAsync(Int64 messageId, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "assistant", null, null, userId);
        flow.Kind = FlowKind.RegenerateStream;
        flow.OriginalMessageId = messageId;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        // Step2: 构建对话上下文
        await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

        // message_start
        yield return ChatStreamEvent.MessageStart(flow.AssistantMessage.Id, flow.ModelConfig.Code ?? String.Empty, flow.AssistantMessage.ThinkingMode);

        // Step3: 初始化管道上下文 + 执行 Enricher 链 + 执行流式生成
        InitPipelineContext(flow);
        await InvokeContextEnrichersAsync(flow, cancellationToken).ConfigureAwait(false);
        await foreach (var ev in ExecuteStreamAsync(flow, flow.ContextMessages, cancellationToken).ConfigureAwait(false))
            yield return ev;

        // Step4: 持久化结果
        PersistStreamResult(flow);

        // Step5: 后处理链
        await InvokePostProcessorsAsync(flow, cancellationToken).ConfigureAwait(false);

        // message_done
        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
            yield return new ChatStreamEvent { Type = "message_done", MessageId = flow.AssistantMessage.Id, Usage = flow.Usage };
    }

    /// <summary>流式发送消息并获取 AI 回复。依次：保存用户消息 → 构建上下文 → 委托管道流式生成 → 持久化结果 → 推送 SSE 事件</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流，含 message_start / thinking_delta / content_delta / tool_call_* / message_done / error</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(null, null, conversationId, request.ModelId, userId);
        flow.Kind = FlowKind.Stream;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        var conversation = flow.Conversation;

        // 更新会话绑定的模型（首次发消息或 model_id=0 自动选模型时持久化实际使用的模型）
        if (conversation.ModelId != flow.ModelConfig.Id)
        {
            conversation.ModelId = flow.ModelConfig.Id;
            conversation.Update();
        }

        // 保存用户消息
        var userMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = request.Content,
            ThinkingMode = request.ThinkingMode,
            ModelName = flow.ModelConfig.Code,
        };
        if (request.AttachmentIds is { Count: > 0 })
            userMsg.Attachments = request.AttachmentIds.ToJson();
        userMsg.Insert();

        // 推荐问题缓存匹配：精确匹配且当天有缓存时，直接返回缓存响应，不请求大模型
        var cached = MatchSuggestedCache(request.Content);
        if (cached != null)
        {
            using var span2 = Tracer?.NewSpan("ai:SuggestedCache", cached.Question);
            await foreach (var ev in StreamSuggestedCacheAsync(conversationId, conversation, cached, request.ThinkingMode, cancellationToken))
                yield return ev;
            yield break;
        }

        // 处理技能激活：每轮均可切换技能，sticky 更新会话绑定（仅更新会话元数据；技能提示词由管道注入）
        var (skillId, skillName) = ResolveSkillActivation(conversation, request.SkillCode);

        // 记录本轮激活的技能名称到用户消息
        if (skillId > 0 && !skillName.IsNullOrEmpty())
        {
            userMsg.SkillNames = skillName;
            userMsg.Update();
        }

        // 预分配AI回复消息编号
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = request.ThinkingMode,
            ModelName = flow.ModelConfig.Code,
        };
        assistantMsg.Insert();

        flow.UserMessage = userMsg;
        flow.AssistantMessage = assistantMsg;
        flow.SkillId = skillId;
        flow.SkillName = skillName;

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, request.Content, cancellationToken).ConfigureAwait(false);

        // message_start
        using var span = Tracer?.NewSpan($"ai:Stream:{flow.ModelConfig.Code}", request.Content);
        yield return ChatStreamEvent.MessageStart(flow.AssistantMessage.Id, flow.ModelConfig.Code ?? String.Empty, request.ThinkingMode);

        // 提前启动标题生成（与流式内容并行执行，不阻塞 SSE 流）
        TryStartTitleGeneration(conversation, conversationId, request.Content, request.AttachmentIds is { Count: > 0 });

        // Step3: 初始化管道上下文 + 执行 Enricher 链 + 执行流式生成
        InitPipelineContext(flow, request.Options);
        await InvokeContextEnrichersAsync(flow, cancellationToken).ConfigureAwait(false);

        // 预处理：注入技能提示词、解析@引用，生成 SystemPrompt
        Pipeline.PrepareContext(flow.ContextMessages, flow.PipelineContext);

        // 注册系统消息就绪回调
        flow.PipelineContext.OnSystemReady = sysContent =>
        {
            if (!sysContent.IsNullOrEmpty())
            {
                flow.UserMessage!.ThinkingContent = sysContent;
                flow.UserMessage.Update();
            }
        };

        await foreach (var ev in ExecuteStreamAsync(flow, flow.ContextMessages, cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        // Step4: 持久化结果
        PersistStreamResult(flow);

        // Step5: 后处理链
        await InvokePostProcessorsAsync(flow, cancellationToken).ConfigureAwait(false);

        // 推荐问题缓存回写
        if (!flow.HasError && Setting.EnableSuggestedQuestionCache && flow.ContentBuilder.Length > 0)
            TryWriteBackSuggestedQuestionCache(request.Content, flow.ContentBuilder.ToString(), flow.ThinkingBuilder.Length > 0 ? flow.ThinkingBuilder.ToString() : null, flow.ModelConfig.Id);

        // message_done
        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = flow.AssistantMessage.Id, Usage = flow.Usage, };
        }
    }

    /// <summary>中断生成。停止后台正在运行的流式生成任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        BackgroundService?.Stop(messageId);
        return Task.CompletedTask;
    }

    /// <summary>获取后台生成任务状态。用户切换会话再切回时，可获取后台已生成的内容</summary>
    /// <param name="messageId">消息编号</param>
    /// <returns>后台任务状态信息，不存在返回 null</returns>
    public virtual BackgroundTask? GetBackgroundTask(Int64 messageId) => BackgroundService?.GetTask(messageId);

    /// <summary>异步生成会话标题。根据用户首条消息内容，调用模型生成简短标题</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户首条消息内容（已提取纯文本）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null) return null;

        // 防御：若调用方未预处理，再做一次多模态文本提取
        userMessage = ExtractTitleText(userMessage) ?? userMessage;

        // 短文本无需调用模型，直接用作标题
        var cleanMsg = userMessage.Replace("\n", " ").Replace("\r", "").Trim();
        if (cleanMsg.Length <= 16)
        {
            if (!String.IsNullOrWhiteSpace(cleanMsg) && cleanMsg != conversation.Title)
            {
                conversation.Title = cleanMsg;
                conversation.Update();
            }
            return cleanMsg;
        }

        using var span = Tracer?.NewSpan("ai:GenerateTitle");

        // 尝试通过模型生成标题
        var modelConfig = ModelService.ResolveModel(conversation.ModelId);
        if (modelConfig != null)
        {
            using var titleClient = ModelService.CreateClient(modelConfig);
            if (titleClient != null)
            {
                try
                {
                    var prompt = "请用16个字以内为以下对话生成一个简短标题，只输出标题文字，不要加任何标点和引号：";
                    var title = await titleClient.ChatAsync(
                        $"{prompt}\n{userMessage}",
                        new ChatOptions { MaxTokens = 30, Temperature = 0.3 },
                        cancellationToken).ConfigureAwait(false);

                    if (!String.IsNullOrWhiteSpace(title))
                    {
                        title = title.Trim().Trim('"', '\u201c', '\u201d', '\'', '\u300a', '\u300b');
                        if (title.Length > 30) title = title[..30];
                        span?.AppendTag(title!);

                        conversation.Title = title;
                        conversation.Update();
                        return title;
                    }
                }
                catch (Exception ex)
                {
                    span?.SetError(ex);
                    Log?.Warn("模型生成标题失败，回退截取: {0}", ex.Message);
                }
            }
        }

        // 回退：截取前16个字符
        var fallbackTitle = userMessage.Length > 16 ? userMessage[..16] : userMessage;
        fallbackTitle = fallbackTitle.Replace("\n", " ").Replace("\r", "").Trim();

        if (!String.IsNullOrWhiteSpace(fallbackTitle) && fallbackTitle != conversation.Title)
        {
            conversation.Title = fallbackTitle;
            conversation.Update();
        }

        return fallbackTitle;
    }

    /// <summary>从用户消息中提取纯文本，支持 JSON 编码的多模态内容数组。用于标题生成等场景</summary>
    /// <param name="userMessage">用户消息文本，可能是纯文本或 JSON 多模态内容数组</param>
    /// <returns>提取到的纯文本，无文本时返回 null</returns>
    public static String? ExtractTitleText(String? userMessage)
    {
        if (userMessage.IsNullOrEmpty()) return null;

        // 快速判断：不以 [ 开头则视为普通文本
        if (!userMessage.StartsWith('[')) return userMessage;

        // 尝试按 OpenAI 多模态格式解析 [{"type":"text","text":"..."},...]，提取文本片段
        var contents = AiChatMessage.ParseMultimodalContent(userMessage);
        if (contents == null || contents.Count == 0) return userMessage;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in contents)
        {
            if (item is TextContent text && !String.IsNullOrEmpty(text.Text))
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(text.Text);
            }
        }
        var result = sb.Return(true);

        // 有文本则返回；全是图片等非文本内容时返回 "[图片] 对话"
        return !result.IsNullOrEmpty() ? result : null;
    }

    #endregion

    #region 步骤方法

    /// <summary>初始化流程上下文。按消息编号或会话编号查找实体、验证角色、解析模型，组装 <see cref="MessageFlowContext"/></summary>
    /// <remarks>
    /// 三种调用模式：
    /// <list type="bullet">
    /// <item>按消息：传 messageId + expectedRole，自动查找所属会话和模型</item>
    /// <item>按会话：传 conversationId + modelId，由调用方后续填充 UserMessage/AssistantMessage</item>
    /// <item>混合：两者都传时，messageId 优先</item>
    /// </list>
    /// </remarks>
    /// <param name="messageId">消息编号（可选，传入时查消息并验证角色）</param>
    /// <param name="expectedRole">期望消息角色（当 messageId 有值时必传，"user" 或 "assistant"）</param>
    /// <param name="conversationId">会话编号（可选，当 messageId 无值时使用）</param>
    /// <param name="modelId">请求指定的模型编号（0 或 null 时使用会话绑定模型，仅 conversationId 模式使用）</param>
    /// <param name="userId">当前用户编号</param>
    /// <returns>初始化后的流程上下文，验证失败时 <see cref="MessageFlowContext.Error"/> 不为 null</returns>
    protected virtual MessageFlowContext CreateFlowContext(Int64? messageId, String? expectedRole, Int64? conversationId, Int32? modelId, Int32 userId)
    {
        using var span = Tracer?.NewSpan("ai:CreateFlowContext", new { messageId, expectedRole, conversationId, modelId, userId });

        ChatMessage? entity = null;
        Conversation? conversation;

        if (messageId > 0)
        {
            // 按消息查找
            entity = ChatMessage.FindById(messageId.Value);
            if (entity == null || (!expectedRole.IsNullOrEmpty() && !entity.Role.EqualIgnoreCase(expectedRole)))
                return new MessageFlowContext { Error = new ChatException("MESSAGE_NOT_FOUND", "消息不存在或角色不匹配") };

            conversation = Conversation.FindById(entity.ConversationId);
        }
        else
        {
            // 按会话查找
            conversation = conversationId > 0 ? Conversation.FindById(conversationId.Value) : null;
        }

        if (conversation == null)
            return new MessageFlowContext { Error = new ChatException("CONVERSATION_NOT_FOUND", "会话不存在") };

        // 解析模型：按消息模式用 ResolveModel + IsAvailable；按会话模式用 ResolveModelOrDefault（支持降级）
        ModelConfig? modelConfig;
        if (messageId > 0)
        {
            modelConfig = ModelService.ResolveModel(conversation.ModelId);
            if (modelConfig == null || !ModelService.IsAvailable(modelConfig))
                return new MessageFlowContext { Error = new ChatException("MODEL_UNAVAILABLE", $"模型 '{conversation.ModelName}' 不可用") };
        }
        else
        {
            var effectiveModelId = modelId > 0 ? modelId.Value : conversation.ModelId;
            modelConfig = ModelService.ResolveModelOrDefault(effectiveModelId);
            if (modelConfig == null)
                return new MessageFlowContext { Error = new ChatException("MODEL_UNAVAILABLE", "系统暂无可用模型，请先在管理后台配置并启用至少一个模型") };
        }

        var flow = new MessageFlowContext
        {
            Conversation = conversation,
            ModelConfig = modelConfig,
            UserId = userId,
            SkillId = conversation.SkillId,
            SkillName = conversation.SkillName,
        };

        // 按消息模式：自动填充 UserMessage 或 AssistantMessage
        if (entity != null)
        {
            if (entity.Role.EqualIgnoreCase("user"))
                flow.UserMessage = entity;
            else
                flow.AssistantMessage = entity;
        }

        return flow;
    }

    /// <summary>检查并匹配推荐问题缓存。精确匹配当天内的缓存记录</summary>
    /// <param name="content">用户消息内容</param>
    /// <returns>匹配到的缓存记录，未命中返回 null</returns>
    protected virtual SuggestedQuestion? MatchSuggestedCache(String content)
    {
        if (!Setting.EnableSuggestedQuestionCache) return null;

        return SuggestedQuestion.FindCachedTodayByQuestion(content);
    }

    /// <summary>处理技能激活。根据请求中的 SkillCode 切换或清除会话绑定技能</summary>
    /// <param name="conversation">当前会话</param>
    /// <param name="skillCode">请求指定的技能编码（null/空=不变，"none"=清除，其他=切换）</param>
    /// <returns>本轮激活的技能编号和技能名称</returns>
    protected virtual (Int32 SkillId, String? SkillName) ResolveSkillActivation(Conversation conversation, String? skillCode)
    {
        var skillId = conversation.SkillId;
        var skillName = conversation.SkillName;

        if (String.IsNullOrEmpty(skillCode)) return (skillId, skillName);

        if (skillCode.EqualIgnoreCase("none"))
        {
            // 清除技能绑定，回到通用对话
            skillId = 0;
            skillName = null;
            if (conversation.SkillId != 0)
            {
                conversation.SkillId = 0;
                conversation.SkillName = null;
                conversation.Update();
            }
        }
        else
        {
            var skill = Skill.FindByCode(skillCode);
            if (skill != null && skill.Enable)
            {
                skillId = skill.Id;
                skillName = skill.Name;
                if (conversation.SkillId != skillId)
                {
                    conversation.SkillId = skillId;
                    conversation.SkillName = skillName;
                    conversation.Update();
                }
            }
        }

        return (skillId, skillName);
    }

    /// <summary>启动异步标题生成（仅会话首条消息且启用自动标题时触发）</summary>
    /// <param name="conversation">当前会话</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="content">用户消息内容</param>
    /// <param name="hasAttachments">是否包含附件</param>
    protected virtual void TryStartTitleGeneration(Conversation conversation, Int64 conversationId, String content, Boolean hasAttachments)
    {
        if (conversation.MessageCount != 0 || !Setting.AutoGenerateTitle) return;

        var titleText = ExtractTitleText(content);
        if (titleText.IsNullOrEmpty() && hasAttachments)
            titleText = "[图片] 对话";

        if (!titleText.IsNullOrEmpty())
            _ = Task.Run(() => GenerateTitleAsync(conversationId, titleText, CancellationToken.None));
    }

    /// <summary>构建对话上下文。从历史消息构建 AI 对话上下文，包含系统提示词注入</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="currentContent">当前用户消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual Task<IList<AiChatMessage>> BuildContextAsync(MessageFlowContext flow, String currentContent, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan("ai:BuildContext");

        var contextMessages = BuildContextMessages(flow.UserId, flow.Conversation.Id, currentContent, flow.ModelConfig);
        flow.ContextMessages = contextMessages;
        return Task.FromResult(contextMessages);
    }

    /// <summary>为重新生成场景构建上下文。取目标消息之前的历史消息，注入系统提示词</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual Task<IList<AiChatMessage>> BuildContextForRegenerateAsync(MessageFlowContext flow, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan("ai:BuildContextForRegenerate");

        var entity = flow.AssistantMessage;
        var beforeMessages = ChatMessage.FindAllBeforeId(entity.ConversationId, entity.Id);

        var setting = Setting;
        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = new List<AiChatMessage>();

        // 注入系统提示词（技能提示词由管道注入）
        var systemMsg = BuildSystemMessage(flow.UserId, flow.ModelConfig, beforeMessages.Count(e => e.Role == "user"));
        if (systemMsg != null) contextMessages.Add(systemMsg);

        foreach (var msg in beforeMessages)
        {
            if (ShouldSkipHistoryMessage(msg)) continue;

            if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            {
                contextMessages.Add(BuildMultimodalUserMessage(msg.Attachments, msg.Content));
                continue;
            }
            contextMessages.Add(new AiChatMessage { Role = msg.Role ?? "user", Content = msg.Content });
        }

        flow.ContextMessages = contextMessages;
        return Task.FromResult<IList<AiChatMessage>>(contextMessages);
    }

    /// <summary>初始化管道执行上下文。从 flow 提取 UserId/ConversationId/SkillId 创建 ChatPipelineContext</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="options">请求选项（可选）</param>
    protected virtual void InitPipelineContext(MessageFlowContext flow, IDictionary<String, Object?>? options = null)
    {
        flow.PipelineContext = new ChatPipelineContext
        {
            UserId = flow.UserId + "",
            ConversationId = flow.Conversation.Id + "",
            SkillId = flow.SkillId
        };
        if (options != null) flow.PipelineContext.Items = options;
    }

    /// <summary>执行流式对话管道。创建管道流并消费事件，结果写入 flow</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="contextMessages">对话上下文消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    protected virtual IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(MessageFlowContext flow, IList<AiChatMessage> contextMessages, CancellationToken cancellationToken)
    {
        var eventSource = Pipeline.StreamAsync(contextMessages, flow.ModelConfig, flow.AssistantMessage.ThinkingMode, flow.PipelineContext, cancellationToken);
        return ExecuteStreamAsync(flow, eventSource, cancellationToken);
    }

    /// <summary>消费流式事件源。将事件透传给调用方，同时将 content/thinking/usage/error 写入 flow</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="eventSource">事件流源（来自管道或后台服务）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(MessageFlowContext flow, IAsyncEnumerable<ChatStreamEvent> eventSource, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var ev in DrainPipelineAsync(eventSource, flow.ContentBuilder, flow.ThinkingBuilder, flow.ToolCalls,
            u => flow.Usage = u, (err, e) => { flow.HasError = err; flow.DeferredError = e; }, "流式生成失败", cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
            if (flow.HasError) break;
        }

        if (flow.DeferredError != null)
            yield return flow.DeferredError;
    }

    /// <summary>持久化流式生成结果。统一处理消息、会话和用量保存</summary>
    /// <param name="flow">流程上下文（包含 Content/Thinking/ToolCalls/Usage/Error 等收集结果）</param>
    protected virtual void PersistStreamResult(MessageFlowContext flow)
    {
        using var span = Tracer?.NewSpan("ai:PersistStreamResult");

        var assistantMsg = flow.AssistantMessage;
        var conversation = flow.Conversation;
        var pipelineCtx = flow.PipelineContext;

        // 写入消息内容
        assistantMsg.Content = flow.ContentBuilder.Length > 0 ? flow.ContentBuilder.ToString() : null;
        if (flow.ThinkingBuilder.Length > 0)
            assistantMsg.ThinkingContent = flow.ThinkingBuilder.ToString();
        if (flow.ToolCalls.Count > 0)
        {
            assistantMsg.ToolCalls = flow.ToolCalls.ToJson();
            assistantMsg.ToolNames = String.Join(",", flow.ToolCalls.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        // 技能名称（ResolvedSkillNames 为 ISet 已自动去重）
        var skillNames = new HashSet<String>(pipelineCtx.ResolvedSkillNames, StringComparer.OrdinalIgnoreCase);
        if (flow.SkillId > 0 && !flow.SkillName.IsNullOrEmpty())
            skillNames.Add(flow.SkillName);

        ApplyUsageToMessage(assistantMsg, flow.Usage, flow.HasError, flow.DeferredError?.Error);
        ApplyRequestParams(assistantMsg, flow.ModelConfig, pipelineCtx);
        assistantMsg.Update();

        // 技能名称与可用工具名称写入用户消息
        if (flow.UserMessage != null)
        {
            if (skillNames.Count > 0)
                flow.UserMessage.SkillNames = String.Join(",", skillNames);
            if (pipelineCtx.AvailableToolNames.Count > 0)
                flow.UserMessage.ToolNames = String.Join(",", pipelineCtx.AvailableToolNames);
            flow.UserMessage.Update();
        }

        // 更新会话
        ApplyUsageToConversation(conversation, assistantMsg.ConversationId, flow.Usage);
        conversation.ModelName = flow.ModelConfig.Name;
        conversation.Update();

        // 记录用量
        if (flow.Usage != null)
            UsageService?.Record(flow.UserId, 0, assistantMsg.ConversationId, assistantMsg.Id, flow.ModelConfig.Id, flow.Usage, "Chat");
    }

    /// <summary>持久化非流式生成结果。写入 AI 回复内容、用量统计和会话累计</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="response">管道完整响应</param>
    /// <param name="elapsedMs">执行耗时（毫秒）</param>
    protected virtual void PersistCompleteResult(MessageFlowContext flow, ChatResponse response, Int32 elapsedMs)
    {
        using var span = Tracer?.NewSpan("ai:PersistCompleteResult");

        var entity = flow.AssistantMessage;
        var conversation = flow.Conversation;

        var newContent = response.Messages?.FirstOrDefault()?.Message?.Content as String ?? String.Empty;
        var reasoning = response.Messages?.FirstOrDefault()?.Message?.ReasoningContent;

        entity.Content = newContent;
        if (!String.IsNullOrEmpty(reasoning)) entity.ThinkingContent = reasoning;
        entity.ElapsedMs = elapsedMs;
        entity.Update();

        if (response.Usage != null)
        {
            UsageService?.Record(flow.UserId, 0, entity.ConversationId, entity.Id, flow.ModelConfig.Id, response.Usage, "Chat");
            conversation.InputTokens += response.Usage.InputTokens;
            conversation.OutputTokens += response.Usage.OutputTokens;
            conversation.TotalTokens += response.Usage.TotalTokens;
            conversation.ElapsedMs += elapsedMs;
            conversation.Update();
        }
    }

    #endregion

    #region 上下文构建

    /// <summary>构建上下文消息列表。按配置的轮数截取历史消息，并注入系统提示词</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="currentContent">当前用户消息内容</param>
    /// <param name="modelConfig">模型配置（可选，用于注入模型级系统提示词）</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual IList<AiChatMessage> BuildContextMessages(Int32 userId, Int64 conversationId, String currentContent, ModelConfig? modelConfig = null)
    {
        var setting = Setting;
        var maxRounds = setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;

        var history = ChatMessage.FindAllByConversationIdDesc(conversationId, maxRounds * 2);
        //history.Reverse();
        //!!! 不能使用 Reverse ，它未能让列表完全倒置
        history = history.OrderBy(e => e.Id).ToList();

        var messages = new List<AiChatMessage>();

        // 注入系统提示词
        var systemMsg = BuildSystemMessage(userId, modelConfig, history.Count);
        if (systemMsg != null) messages.Add(systemMsg);

        foreach (var msg in history)
        {
            if (ShouldSkipHistoryMessage(msg)) continue;

            if (msg.Role == "assistant" && !msg.ToolCalls.IsNullOrEmpty())
            {
                IList<ToolCallDto>? storedDtos = null;
                try
                {
                    storedDtos = msg.ToolCalls.ToJsonEntity<List<ToolCallDto>>();
                }
                catch { }
                if (storedDtos != null && storedDtos.Count > 0)
                {
                    messages.Add(new AiChatMessage
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = storedDtos.Select(tc => new AiToolCall
                        {
                            Id = tc.Id,
                            Function = new AiFunctionCall { Name = tc.Name, Arguments = tc.Arguments },
                        }).ToList(),
                    });
                    foreach (var tc in storedDtos)
                    {
                        messages.Add(new AiChatMessage
                        {
                            Role = "tool",
                            ToolCallId = tc.Id,
                            Content = tc.Result ?? String.Empty,
                        });
                    }
                    if (!String.IsNullOrEmpty(msg.Content))
                        messages.Add(new AiChatMessage { Role = "assistant", Content = msg.Content });
                    continue;
                }
            }
            if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            {
                messages.Add(BuildMultimodalUserMessage(msg.Attachments, msg.Content));
                continue;
            }
            messages.Add(new AiChatMessage
            {
                Role = msg.Role!,
                Content = msg.Content,
            });
        }

        if (history.Count > 4 && !currentContent.IsNullOrEmpty())
        {
            messages.Add(new AiChatMessage
            {
                Role = "system",
                Content = $"请直接针对用户最新的问题进行回答：{currentContent}",
            });
        }

        return messages;
    }

    /// <summary>构建系统提示词消息。合并用户全局级和模型级系统提示词（技能提示词由管道注入）</summary>
    /// <remarks>基类提供简化版：只拼接 IUser 基础信息与 UserSetting 个性化。
    /// 派生类可 override 增强（例如 ChatAI 社区版注入部门信息，StarChat 商用版注入三明治等）。</remarks>
    /// <param name="userId">当前用户编号</param>
    /// <param name="modelConfig">模型配置（可选）</param>
    /// <param name="historyCount">当前上下文中历史消息条数，大于 0 时才注入多轮优先级提示</param>
    /// <returns>系统消息，无提示词时返回 null</returns>
    protected virtual AiChatMessage? BuildSystemMessage(Int32 userId, ModelConfig? modelConfig, Int32 historyCount = 0)
    {
        using var span = Tracer?.NewSpan("ai:BuildSystemMessage", new { userId, modelConfig?.Name, historyCount });
        var parts = new List<String>();

        // 0. 当前用户基础信息（基类只拼 DisplayName/Name/Roles，不查部门——派生类按需增强）
        if (userId > 0 && ManageProvider.Provider?.FindByID(userId) is IUser user)
        {
            var sb = Pool.StringBuilder.Get();
            sb.Append($"当前用户：{user.DisplayName}（{user.Name}）");
            var roles = user.Roles;
            if (roles?.Length > 0) sb.Append($"，角色：{roles.Join(",")}");
            var dept = Department.FindByID(user.DepartmentID);
            if (dept != null) sb.Append($"，部门：{dept.Name}");

            parts.Add(sb.Return(true));
        }

        // 1. 个性化定制
        var userSetting = UserSetting.FindByUserId(userId);
        if (userSetting != null)
        {
            if (!String.IsNullOrWhiteSpace(userSetting.Nickname))
                parts.Add($"用户希望你称呼他为「{userSetting.Nickname.Trim()}」");

            if (!String.IsNullOrWhiteSpace(userSetting.UserBackground))
                parts.Add($"## 用户背景信息\n{userSetting.UserBackground.Trim()}");

            var stylePrompt = userSetting.ResponseStyle switch
            {
                ResponseStyle.Precise => "请给出准确、确定性高的回答。优先引用事实和数据，避免模糊表述和不确定的推测。回答简洁有条理。",
                ResponseStyle.Vivid => "请用丰富的表达方式回答，善于使用类比、举例和故事来解释概念。让回答有温度、易于理解，适当展开讨论。",
                ResponseStyle.Creative => "请大胆发散思维，提供新颖独特的视角和创意方案。鼓励联想、跨界类比和非常规思路，不必拘泥于常规答案。",
                _ => null
            };
            if (stylePrompt != null) parts.Add(stylePrompt);
        }

        // 2. 用户自定义指令
        if (userSetting != null && !String.IsNullOrWhiteSpace(userSetting.SystemPrompt))
            parts.Add(userSetting.SystemPrompt.Trim());

        // 3. 模型级系统提示词
        if (modelConfig != null && !String.IsNullOrWhiteSpace(modelConfig.SystemPrompt))
            parts.Add(modelConfig.SystemPrompt.Trim());

        // 4. 多轮对话时强调最新消息优先级
        if (historyCount > 1)
            parts.Add("请优先回应用户的最新消息。如果最新消息与之前的对话内容存在矛盾或方向变化，以最新消息为准。");

        if (parts.Count == 0) return null;
        span?.AppendTag(parts.Count);

        return new AiChatMessage
        {
            Role = "system",
            Content = String.Join("\n\n", parts),
        };
    }

    /// <summary>将用户消息的附件与文本内容组合为多模态消息。基类为**降级实现**：仅保留文本内容，
    /// 不解析任何附件。派生类（例如 ChatAI 社区版读取 Cube <c>Attachment</c> 实体 + NewLife.Office 解析文档）应 override 增强</summary>
    /// <param name="attachmentsJson">附件ID列表 JSON（Int64/String 数组）</param>
    /// <param name="textContent">文本内容</param>
    /// <returns>多模态 AiChatMessage，基类默认退化为纯文本消息</returns>
    protected virtual AiChatMessage BuildMultimodalUserMessage(String attachmentsJson, String? textContent)
        => new() { Role = "user", Content = textContent };

    /// <summary>判断是否应跳过历史消息。用于过滤预分配但尚未写入正文的 assistant 占位消息，避免发送非法上下文给上游模型</summary>
    /// <param name="message">历史消息实体</param>
    /// <returns>应跳过返回 true，否则返回 false</returns>
    protected static Boolean ShouldSkipHistoryMessage(ChatMessage message)
    {
        if (!message.Role.EqualIgnoreCase("assistant")) return false;

        return message.Content.IsNullOrEmpty() && message.ToolCalls.IsNullOrEmpty();
    }

    /// <summary>解析附件ID列表 JSON。兼容字符串数组和整数数组两种格式</summary>
    /// <param name="json">附件ID列表 JSON</param>
    /// <returns>ID 列表，解析失败返回 null</returns>
    protected static IList<Int64>? ParseAttachmentIds(String json)
    {
        // 优先尝试 Int64 数组
        var ids = json.ToJsonEntity<List<Int64>>();
        if (ids != null && ids.Count > 0 && ids[0] != 0) return ids;

        // 前端 attachmentIds.map(String) 产生字符串数组 ["123","456"]
        var strIds = json.ToJsonEntity<List<String>>();
        if (strIds != null && strIds.Count > 0)
            return strIds.Select(s => s.ToLong()).Where(v => v > 0).ToList();

        return null;
    }

    #endregion

    #region 推荐缓存

    /// <summary>命中推荐问题缓存时，流式输出缓存响应。插入 assistant 消息，按节流配置逐块推送内容，最后更新会话计数</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="conversation">会话实体</param>
    /// <param name="cached">命中的推荐问题缓存条目</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> StreamSuggestedCacheAsync(Int64 conversationId, Conversation conversation, SuggestedQuestion cached, ThinkingMode thinkingMode, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cachedMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            Content = cached.Response,
            ThinkingContent = cached.ThinkingResponse.IsNullOrEmpty() ? null : cached.ThinkingResponse,
        };
        cachedMsg.Insert();

        var streamingSpeed = Setting.StreamingSpeed;

        yield return ChatStreamEvent.MessageStart(cachedMsg.Id, cached.Model?.Code ?? String.Empty, thinkingMode);

        if (!cached.ThinkingResponse.IsNullOrEmpty())
        {
            if (streamingSpeed > 5)
            {
                yield return new ChatStreamEvent { Type = "thinking_delta", Content = cached.ThinkingResponse };
            }
            else
            {
                var (tChunkSize, tDelayMs) = GetCachedStreamingParams(streamingSpeed);
                await foreach (var chunk in ThrottleTextAsync(cached.ThinkingResponse, tChunkSize, tDelayMs, cancellationToken))
                    yield return new ChatStreamEvent { Type = "thinking_delta", Content = chunk };
            }
        }

        if (streamingSpeed > 5)
        {
            yield return new ChatStreamEvent { Type = "content_delta", Content = cached.Response };
        }
        else
        {
            var (chunkSize, delayMs) = GetCachedStreamingParams(streamingSpeed);
            await foreach (var chunk in ThrottleTextAsync(cached.Response, chunkSize, delayMs, cancellationToken))
                yield return new ChatStreamEvent { Type = "content_delta", Content = chunk };
        }

        // 更新会话
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        conversation.Update();

        yield return new ChatStreamEvent { Type = "message_done", MessageId = cachedMsg.Id };
    }

    /// <summary>回写推荐问题缓存。将本次 AI 回复写入匹配的推荐问题，供下次直接命中，当天已更新时跳过</summary>
    /// <param name="question">用户提问内容</param>
    /// <param name="content">AI 回复正文</param>
    /// <param name="thinking">AI 思考内容（可为 null）</param>
    /// <param name="modelId">使用的模型编号</param>
    protected static void TryWriteBackSuggestedQuestionCache(String question, String content, String? thinking, Int32 modelId)
    {
        var sq = SuggestedQuestion.FindCachedByQuestion(question);
        if (sq == null || (!sq.Response.IsNullOrEmpty() && sq.UpdateTime.Date >= DateTime.Today)) return;

        sq.Response = content;
        sq.ThinkingResponse = thinking;
        sq.ModelId = modelId;
        sq.Update();
    }

    /// <summary>根据流式速度等级（1~5）返回缓存回放时的分块参数</summary>
    /// <param name="speed">速度等级，1=慢，3=默认，5=快</param>
    /// <returns>(每块字符数, 块间延迟毫秒数)</returns>
    protected static (Int32 ChunkSize, Int32 DelayMs) GetCachedStreamingParams(Int32 speed) => speed switch
    {
        1 => (4, 60),
        2 => (6, 30),
        4 => (14, 16),
        5 => (24, 10),
        _ => (10, 20),
    };

    /// <summary>将文本按指定块大小拆分后逐块延迟输出，模拟逐 token 打字机效果</summary>
    /// <param name="text">待输出文本</param>
    /// <param name="chunkSize">每块字符数</param>
    /// <param name="delayMs">块间延迟毫秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected static async IAsyncEnumerable<String> ThrottleTextAsync(String text, Int32 chunkSize, Int32 delayMs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (text.IsNullOrEmpty()) yield break;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var buf = new StringBuilder(chunkSize * 4);
        var count = 0;
        while (enumerator.MoveNext())
        {
            buf.Append(enumerator.GetTextElement());
            count++;
            if (count >= chunkSize)
            {
                yield return buf.ToString();
                buf.Clear();
                count = 0;
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    #endregion

    #region 流事件 Drain

    /// <summary>枚举管道流，收集 content/thinking/usage，将可透传事件实时 yield，并在循环结束后写入收集结果</summary>
    /// <param name="source">管道事件流</param>
    /// <param name="contentBuilder">正文内容收集器</param>
    /// <param name="thinkingBuilder">思考内容收集器</param>
    /// <param name="toolCallsCollector">工具调用收集器（可为 null）</param>
    /// <param name="setFinalUsage">用量回调：收到 message_done 事件时调用</param>
    /// <param name="setErrorState">错误回调：发生异常时调用，传入 (hasError, deferredErrorEvent)</param>
    /// <param name="errorLogPrefix">错误日志前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected async IAsyncEnumerable<ChatStreamEvent> DrainPipelineAsync(
        IAsyncEnumerable<ChatStreamEvent> source,
        StringBuilder contentBuilder,
        StringBuilder thinkingBuilder,
        List<ToolCallDto>? toolCallsCollector,
        Action<UsageDetails?> setFinalUsage,
        Action<Boolean, ChatStreamEvent?> setErrorState,
        String errorLogPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var artifactDetector = new ArtifactDetector();

        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                Boolean moved;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log?.Error("{0}: {1}", errorLogPrefix, ex.Message);
                    setErrorState(true, ChatStreamEvent.ErrorEvent("STREAM_ERROR", ex.Message));
                    break;
                }

                if (!moved) break;

                var ev = enumerator.Current;
                switch (ev.Type)
                {
                    case "thinking_delta":
                        thinkingBuilder.Append(ev.Content);
                        yield return ev;
                        break;
                    case "content_delta":
                        contentBuilder.Append(ev.Content);

                        // Artifact 检测：识别可预览代码块（html/svg/mermaid）
                        var artifactEvents = artifactDetector.Process(ev.Content!);
                        foreach (var ae in artifactEvents)
                        {
                            switch (ae.Kind)
                            {
                                case ArtifactEventKind.ArtifactStart:
                                    yield return ChatStreamEvent.ArtifactStart(ae.Language!);
                                    break;
                                case ArtifactEventKind.ArtifactDelta:
                                    yield return ChatStreamEvent.ArtifactDelta(ae.Content!);
                                    break;
                                case ArtifactEventKind.ArtifactEnd:
                                    yield return ChatStreamEvent.ArtifactEnd();
                                    break;
                            }
                        }

                        yield return ev;
                        break;
                    case "message_done":
                        setFinalUsage(ev.Usage);
                        break;
                    case "error":
                        setErrorState(true, ev);
                        break;
                    case "tool_call_start" when toolCallsCollector != null:
                        toolCallsCollector.Add(new ToolCallDto(ev.ToolCallId + "", ev.Name + "", ToolCallStatus.Calling, ev.Arguments));
                        yield return ev;
                        break;
                    case "tool_call_done" when toolCallsCollector != null:
                        UpdateToolCallStatus(toolCallsCollector, ev.ToolCallId, ToolCallStatus.Done, ev.Result);
                        yield return ev;
                        break;
                    case "tool_call_error" when toolCallsCollector != null:
                        UpdateToolCallStatus(toolCallsCollector, ev.ToolCallId, ToolCallStatus.Error, ev.Error);
                        yield return ev;
                        break;
                    default:
                        yield return ev;
                        break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>更新工具调用列表中指定 id 的状态与结果</summary>
    /// <param name="collector">工具调用收集器</param>
    /// <param name="toolCallId">工具调用编号</param>
    /// <param name="status">新状态</param>
    /// <param name="value">结果或错误信息</param>
    protected static void UpdateToolCallStatus(List<ToolCallDto> collector, String? toolCallId, ToolCallStatus status, String? value)
    {
        for (var i = collector.Count - 1; i >= 0; i--)
        {
            if (collector[i].Id == toolCallId)
            {
                var orig = collector[i];
                collector[i] = new ToolCallDto(orig.Id, orig.Name, status, orig.Arguments, value);
                break;
            }
        }
    }

    #endregion

    #region Enricher / PostProcessor 调用

    /// <summary>遍历上下文增强器链。按 Order 升序依次调用，单个 Enricher 失败只记录日志，不影响后续与主流程</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    protected virtual async Task InvokeContextEnrichersAsync(MessageFlowContext flow, CancellationToken cancellationToken)
    {
        if (Enrichers.Count == 0) return;
        using var span = Tracer?.NewSpan("ai:InvokeEnrichers", Enrichers.Count);
        foreach (var enricher in Enrichers)
        {
            if (!enricher.IsApplicable(flow)) continue;
            try
            {
                await enricher.EnrichAsync(flow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                Log?.Warn("上下文增强器 {0} 执行失败：{1}", enricher.GetType().Name, ex.Message);
            }
        }
    }

    /// <summary>遍历消息流后处理器链。按 Order 升序依次调用；单个 PostProcessor 失败只记录日志，不影响后续执行与主流程返回</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    protected virtual async Task InvokePostProcessorsAsync(MessageFlowContext flow, CancellationToken cancellationToken)
    {
        if (PostProcessors.Count == 0) return;
        using var span = Tracer?.NewSpan("ai:InvokePostProcessors", PostProcessors.Count);
        foreach (var processor in PostProcessors)
        {
            if (!processor.IsApplicable(flow)) continue;
            try
            {
                await processor.ProcessAsync(flow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                Log?.Warn("消息流后处理器 {0} 执行失败：{1}", processor.GetType().Name, ex.Message);
            }
        }
    }

    #endregion

    #region 实体映射

    /// <summary>将用量统计写入 AI 回复消息实体（不保存，调用方负责 Update）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="usage">用量统计</param>
    /// <param name="hasError">是否有错误</param>
    /// <param name="errorDetail">错误详情</param>
    protected static void ApplyUsageToMessage(ChatMessage msg, UsageDetails? usage, Boolean hasError, String? errorDetail = null)
    {
        if (msg.Content.IsNullOrEmpty())
        {
            if (hasError)
                msg.Content = errorDetail.IsNullOrEmpty() ? "[生成失败]" : $"[生成失败] {errorDetail}";
            else if (!msg.ThinkingContent.IsNullOrEmpty())
            {
                // 小参数量推理模型有时将正文误写入思考字段（正文为空、思考字段有内容）
                // 不标记为"已中断"，ThinkingContent 中已有内容可供前端展示
            }
            else
                msg.Content = "[已中断]";
        }
        else if (hasError && !errorDetail.IsNullOrEmpty())
        {
            msg.Content += $"\n\n[错误] {errorDetail}";
        }
        if (usage != null)
        {
            msg.InputTokens = usage.InputTokens;
            msg.OutputTokens = usage.OutputTokens;
            msg.TotalTokens = usage.TotalTokens;
            msg.ElapsedMs = usage.ElapsedMs;
        }
    }

    /// <summary>将请求参数写入 AI 回复消息实体（不保存，调用方负责 Update）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="modelConfig">模型配置</param>
    /// <param name="context">管道上下文（携带 MaxTokens/Temperature/FinishReason）</param>
    protected static void ApplyRequestParams(ChatMessage msg, ModelConfig modelConfig, ChatPipelineContext context)
    {
        msg.ModelName = modelConfig.Code;
        if (context.MaxTokens > 0) msg.MaxTokens = context.MaxTokens;
        if (context.Temperature != null) msg.Temperature = context.Temperature.Value;
        if (!context.FinishReason.IsNullOrEmpty()) msg.FinishReason = context.FinishReason;
    }

    /// <summary>将用量统计累加到会话实体并更新最后消息时间（不保存，调用方负责 Update）</summary>
    /// <param name="conversation">会话实体</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="usage">用量统计</param>
    protected static void ApplyUsageToConversation(Conversation conversation, Int64 conversationId, UsageDetails? usage)
    {
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        if (usage != null)
        {
            conversation.InputTokens += usage.InputTokens;
            conversation.OutputTokens += usage.OutputTokens;
            conversation.TotalTokens += usage.TotalTokens;
            conversation.ElapsedMs += usage.ElapsedMs;
        }
    }

    /// <summary>转换消息实体为 DTO。供派生类在 <see cref="RegenerateMessageAsync"/> 等场景转出</summary>
    /// <param name="entity">消息实体</param>
    /// <returns>消息 DTO</returns>
    protected static MessageDto ToMessageDto(ChatMessage entity)
    {
        IReadOnlyList<ToolCallDto>? toolCalls = null;
        if (!String.IsNullOrEmpty(entity.ToolCalls))
        {
            try { toolCalls = entity.ToolCalls.ToJsonEntity<List<ToolCallDto>>(); }
            catch { }
        }
        return new MessageDto(entity.Id, entity.ConversationId, entity.Role ?? String.Empty, entity.Content ?? String.Empty, entity.ThinkingContent, entity.ThinkingMode, entity.Attachments, entity.CreateTime)
        {
            ToolCalls = toolCalls,
            InputTokens = entity.InputTokens,
            OutputTokens = entity.OutputTokens,
            TotalTokens = entity.TotalTokens,
        };
    }

    #endregion
}
