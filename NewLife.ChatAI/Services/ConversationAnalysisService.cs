using System.Text;
using NewLife.AI.Providers;
using NewLife.ChatAI.Entity;
using NewLife.Log;
using NewLife.Serialization;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatCompletionRequest = NewLife.AI.Models.ChatCompletionRequest;
using ChatCompletionResponse = NewLife.AI.Models.ChatCompletionResponse;

namespace NewLife.ChatAI.Services;

/// <summary>对话分析服务。对完整的对话内容执行 AI 记忆提取，并将结果写入 MemoryService</summary>
/// <remarks>
/// 工作流程：
/// 1. 将对话消息串联成文本送入专用分析模型
/// 2. AI 按 JSON 格式返回提取到的记忆条目和用户标签
/// 3. 通过 MemoryService 将结果持久化到数据库
/// 4. 写入 LearningLog 记录执行历史
/// </remarks>
public class ConversationAnalysisService
{
    #region 属性
    private readonly GatewayService _gatewayService;
    /// <summary>记忆服务（同时对外暴露给 LearningFilter 注入上下文）</summary>
    public MemoryService MemoryService { get; }
    private readonly ILog _log;

    /// <summary>记忆提取系统提示词</summary>
    private static readonly String ExtractionSystemPrompt = """
        你是一个用户记忆提取助手。分析以下对话内容，提取用户的个人偏好、习惯、兴趣和背景信息。
        
        请以 JSON 格式返回，其中：
        - memories：记忆条目列表，每条包含 category（preference/habit/interest/background）、key（简短标识）、value（具体内容）、confidence（0-100置信度）
        - tags：用户标签列表，每条包含 name（标签名）、category（preference/habit/interest/background）、weight（0-100权重）
        
        只提取能明确从对话中推断的信息，无法确定的不要猜测。如果没有可提取的信息，返回空列表。
        
        返回格式：
        {"memories": [{"category": "preference", "key": "favorite_language", "value": "C#", "confidence": 90}], "tags": [{"name": "程序员", "category": "background", "weight": 80}]}
        """;

    /// <summary>最短触发提取的消息轮数（避免对单条消息做无意义分析）</summary>
    private const Int32 MinMessageRounds = 2;
    #endregion

    #region 构造
    /// <summary>实例化对话分析服务</summary>
    /// <param name="gatewayService">网关服务</param>
    /// <param name="memoryService">记忆服务</param>
    /// <param name="log">日志</param>
    public ConversationAnalysisService(GatewayService gatewayService, MemoryService memoryService, ILog log)
    {
        _gatewayService = gatewayService;
        MemoryService = memoryService;
        _log = log;
    }
    #endregion

    #region 分析
    /// <summary>对话分析入口。提取记忆并写入 LearningLog</summary>
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
        ChatCompletionResponse response,
        String triggerReason = "Chat",
        CancellationToken cancellationToken = default)
    {
        // 消息轮数不足时跳过
        var userMessages = messages.Count(m => m.Role == "user");
        if (userMessages < MinMessageRounds)
        {
            LearningLog.CreateFailed(userId, conversationId, triggerReason, $"消息轮数不足({userMessages}<{MinMessageRounds})，跳过", 0);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var extractedCount = await ExtractAndSaveMemoriesAsync(userId, conversationId, messages, response, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            LearningLog.CreateSuccess(userId, conversationId, triggerReason, extractedCount, (Int32)sw.ElapsedMilliseconds);
            _log?.Info("用户 {0} 记忆提取完成，提取 {1} 条，耗时 {2}ms", userId, extractedCount, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LearningLog.CreateFailed(userId, conversationId, triggerReason, ex.Message, (Int32)sw.ElapsedMilliseconds);
            _log?.Error("用户 {0} 记忆提取失败: {1}", userId, ex.Message);
        }
    }
    #endregion

    #region 辅助
    private async Task<Int32> ExtractAndSaveMemoriesAsync(
        Int32 userId,
        Int64 conversationId,
        IList<AiChatMessage> messages,
        ChatCompletionResponse response,
        CancellationToken cancellationToken)
    {
        // 构建对话文本摘要（只取 user/assistant 消息）
        var dialogText = BuildDialogText(messages, response);

        // 获取可用于分析的模型配置（使用最小模型以降低成本）
        var modelConfig = GetAnalysisModel();
        if (modelConfig == null)
        {
            _log?.Warn("未找到可用于分析的模型配置，跳过记忆提取");
            return 0;
        }

        // 调用 AI 提取记忆
        var extractRequest = new ChatCompletionRequest
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

        var result = await _gatewayService.ChatAsync(extractRequest, modelConfig, null, cancellationToken).ConfigureAwait(false);
        var jsonText = result.Choices?.FirstOrDefault()?.Message?.Content as String;
        if (jsonText.IsNullOrWhiteSpace()) return 0;

        // 解析 JSON 并保存
        return await ParseAndSaveAsync(userId, conversationId, jsonText!, cancellationToken).ConfigureAwait(false);
    }

    private static String BuildDialogText(IList<AiChatMessage> messages, ChatCompletionResponse response)
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
        var responseContent = response.Choices?.FirstOrDefault()?.Message?.Content as String;
        if (!responseContent.IsNullOrWhiteSpace())
            sb.Append("助手: ").AppendLine(responseContent);

        return sb.ToString();
    }

    private static ModelConfig? GetAnalysisModel()
    {
        // 优先使用 Code 含 "mini" / "flash" / "lite" 的轻量模型以节省成本
        var all = ModelConfig.FindAll();
        var enabled = all.Where(m => m.Enable).ToList();
        return enabled.FirstOrDefault(m => m.Code.Contains("mini") || m.Code.Contains("flash") || m.Code.Contains("lite"))
               ?? enabled.FirstOrDefault(m => m.Enable);
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
            _log?.Warn("记忆提取 JSON 解析失败: {0}", jsonText.Cut(200));
            return 0;
        }

        if (parsed == null) return 0;

        var count = 0;

        // 保存记忆条目
        if (parsed.Memories != null)
        {
            foreach (var m in parsed.Memories)
            {
                if (m.Key.IsNullOrEmpty() || m.Value.IsNullOrEmpty()) continue;
                await MemoryService.UpsertMemoryAsync(userId, m.Category ?? "general", m.Key!, m.Value!, m.Confidence, conversationId, cancellationToken).ConfigureAwait(false);
                count++;
            }
        }

        // 保存用户标签
        if (parsed.Tags != null)
        {
            foreach (var t in parsed.Tags)
            {
                if (t.Name.IsNullOrEmpty()) continue;
                UserTag.Upsert(userId, t.Name!, t.Category ?? "general", t.Weight);
            }
        }

        // 更新用户画像统计
        if (count > 0 || (parsed.Tags?.Count ?? 0) > 0)
            await MemoryService.RefreshProfileAsync(userId, cancellationToken).ConfigureAwait(false);

        return count;
    }
    #endregion

    #region 内部模型
    private class ExtractionResult
    {
        public IList<MemoryItem>? Memories { get; set; }
        public IList<TagItem>? Tags { get; set; }
    }

    private class MemoryItem
    {
        public String? Category { get; set; }
        public String? Key { get; set; }
        public String? Value { get; set; }
        public Int32 Confidence { get; set; } = 70;
    }

    private class TagItem
    {
        public String? Name { get; set; }
        public String? Category { get; set; }
        public Int32 Weight { get; set; } = 50;
    }
    #endregion
}
