using System.Text;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.ChatAI.Entity;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatResponse = NewLife.AI.Models.ChatResponse;

namespace NewLife.ChatAI.Services;

/// <summary>对话分析服务。对完整的对话内容执行 AI 记忆提取，并将结果写入 MemoryService</summary>
/// <remarks>
/// 工作流程：
/// 1. 将对话消息串联成文本送入专用分析模型
/// 2. AI 按 JSON 格式返回提取到的记忆条目
/// 3. 通过 MemoryService 将结果持久化到数据库
/// </remarks>
/// <param name="gatewayService">网关服务</param>
/// <param name="memoryService">记忆服务</param>
/// <param name="log">日志</param>
public class ConversationAnalysisService(GatewayService gatewayService, MemoryService memoryService, ILog log)
{
    /// <summary>记忆服务（同时对外暴露给 LearningFilter 注入上下文）</summary>
    public MemoryService MemoryService { get; } = memoryService;

    /// <summary>记忆提取系统提示词</summary>
    private static readonly String ExtractionSystemPrompt = """
        你是一个用户记忆提取助手。分析以下对话内容，提取用户的个人偏好、习惯、兴趣和背景信息。
        
        请以 JSON 格式返回，其中：
        - memories：记忆条目列表，每条包含 category（preference/habit/interest/background）、key（简短标识）、value（具体内容）、confidence（0-100置信度）
        
        只提取能明确从对话中推断的信息，无法确定的不要猜测。如果没有可提取的信息，返回空列表。
        
        返回格式：
        {"memories": [{"category": "preference", "key": "favorite_language", "value": "C#", "confidence": 90}]}
        """;

    /// <summary>用户消息最低总字数（低于此值且仅 1 轮时跳过提取）</summary>
    private const Int32 MinContentLength = 50;

    #region 分析
    /// <summary>对话分析入口。提取记忆并通过日志记录结果</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="conversationId">触发会话ID</param>
    /// <param name="messages">对话消息列表</param>
    /// <param name="response">AI 响应</param>
    /// <param name="triggerReason">触发原因（Chat/Feedback/Manual）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public virtual async Task AnalyzeAsync(
        Int32 userId,
        Int64 conversationId,
        IList<AiChatMessage> messages,
        IChatResponse response,
        String triggerReason = "Chat",
        CancellationToken cancellationToken = default)
    {
        // 智能触发条件：用户消息总字数 < 50 且轮数 < 2 时跳过
        var userMessages = messages.Where(m => m.Role == "user").ToList();
        var totalChars = 0;
        foreach (var msg in userMessages)
        {
            totalChars += (msg.Content as String)?.Length ?? 0;
        }

        if (totalChars < MinContentLength && userMessages.Count < 2)
        {
            log?.Debug("用户 {0} 消息不足（{1}轮/{2}字），跳过记忆提取", userId, userMessages.Count, totalChars);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var extractedCount = await ExtractAndSaveMemoriesAsync(userId, conversationId, messages, response, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            log?.Info("用户 {0} 记忆提取完成，提取 {1} 条，耗时 {2}ms", userId, extractedCount, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            log?.Error("用户 {0} 记忆提取失败（耗时 {1}ms）: {2}", userId, sw.ElapsedMilliseconds, ex.Message);
        }
    }
    #endregion

    #region 辅助
    private async Task<Int32> ExtractAndSaveMemoriesAsync(
        Int32 userId,
        Int64 conversationId,
        IList<AiChatMessage> messages,
        IChatResponse response,
        CancellationToken cancellationToken)
    {
        // 构建对话文本摘要（只取 user/assistant 消息）
        var dialogText = BuildDialogText(messages, response);

        // 获取可用于分析的模型配置（使用最小模型以降低成本）
        var modelConfig = GetAnalysisModel();
        if (modelConfig == null)
        {
            log?.Warn("未找到可用于分析的模型配置，跳过记忆提取");
            return 0;
        }

        // 调用 AI 提取记忆
        var extractRequest = new ChatRequest
        {
            Model = modelConfig.Code,
            Messages =
            [
                new AiChatMessage { Role = "system", Content = ExtractionSystemPrompt },
                new AiChatMessage { Role = "user", Content = $"请分析以下对话内容：\n\n{dialogText}" },
            ],
            MaxTokens = 1024,
            Temperature = 0.2,
        };

        var result = await gatewayService.ChatAsync(extractRequest, modelConfig, null, cancellationToken).ConfigureAwait(false);
        var jsonText = result.Messages?.FirstOrDefault()?.Message?.Content as String;
        if (jsonText.IsNullOrWhiteSpace()) return 0;

        // 解析 JSON 并保存
        return await ParseAndSaveAsync(userId, conversationId, jsonText!, cancellationToken).ConfigureAwait(false);
    }

    private static String BuildDialogText(IList<AiChatMessage> messages, IChatResponse response)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            if (msg.Role is not ("user" or "assistant")) continue;
            var role = msg.Role == "user" ? "用户" : "助手";
            var content = msg.Content as String ?? String.Empty;
            if (content.IsNullOrWhiteSpace()) continue;
            sb.Append(role).Append(": ").AppendLine(content);
        }

        // 追加本次 AI 响应
        var responseContent = response.Messages?.FirstOrDefault()?.Message?.Content as String;
        if (!responseContent.IsNullOrWhiteSpace())
            sb.Append("助手: ").AppendLine(responseContent);

        return sb.ToString();
    }

    private ModelConfig? GetAnalysisModel()
    {
        // 优先使用配置的学习模型
        var setting = ChatSetting.Current;
        if (!setting.LearningModel.IsNullOrWhiteSpace())
        {
            var configured = gatewayService.ResolveModelByCode(setting.LearningModel);
            if (configured != null) return configured;
        }

        // 其次选择 Code 含 "mini" / "flash" / "lite" 的轻量模型以节省成本
        var all = ModelConfig.FindAll();
        var enabled = all.Where(m => m.Enable).ToList();
        return enabled.FirstOrDefault(m => m.Code.Contains("mini") || m.Code.Contains("flash") || m.Code.Contains("lite"))
               ?? enabled.FirstOrDefault();
    }

    private async Task<Int32> ParseAndSaveAsync(
        Int32 userId,
        Int64 conversationId,
        String jsonText,
        CancellationToken cancellationToken)
    {
        // 提取 JSON 块（有时 AI 会夹杂 markdown）
        var start = jsonText.IndexOf('{');
        var end = jsonText.LastIndexOf('}');
        if (start < 0 || end < start) return 0;
        jsonText = jsonText.Substring(start, end - start + 1);

        ExtractionResult? parsed;
        try
        {
            parsed = jsonText.ToJsonEntity<ExtractionResult>();
        }
        catch
        {
            log?.Warn("记忆提取 JSON 解析失败: {0}", jsonText.Cut(200));
            return 0;
        }

        if (parsed?.Memories == null) return 0;

        var count = 0;

        foreach (var m in parsed.Memories)
        {
            if (m.Key.IsNullOrEmpty() || m.Value.IsNullOrEmpty()) continue;
            await MemoryService.UpsertMemoryAsync(userId, m.Category ?? "general", m.Key!, m.Value!, m.Confidence, conversationId, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }
    #endregion

    #region 内部模型
    private class ExtractionResult
    {
        public IList<MemoryItem>? Memories { get; set; }
    }

    private class MemoryItem
    {
        public String? Category { get; set; }
        public String? Key { get; set; }
        public String? Value { get; set; }
        public Int32 Confidence { get; set; } = 70;
    }
    #endregion
}
