using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;

namespace NewLife.ChatAI.Controllers;

/// <summary>用量统计控制器。提供当前用户的对话使用量、Token 消耗等统计数据</summary>
[Route("api/usage")]
public class UsageApiController : ChatApiControllerBase
{
    /// <summary>获取用户累计用量摘要</summary>
    [HttpGet("summary")]
    public ActionResult<UsageSummaryDto> GetSummary()
    {
        var userId = GetCurrentUserId();
        var conversations = Conversation.FindAllByUserId(userId);

        var totalConversations = conversations.Count;
        var totalMessages = conversations.Sum(c => c.MessageCount);
        var promptTokens = conversations.Sum(c => (Int64)c.TotalPromptTokens);
        var completionTokens = conversations.Sum(c => (Int64)c.TotalCompletionTokens);
        var totalTokens = conversations.Sum(c => (Int64)c.TotalTokens);
        var lastActive = conversations.Count > 0
            ? conversations.Max(c => c.LastMessageTime)
            : (DateTime?)null;

        return Ok(new UsageSummaryDto
        {
            Conversations = totalConversations,
            Messages = totalMessages,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            LastActiveTime = lastActive,
        });
    }

    /// <summary>获取按日用量明细</summary>
    /// <param name="start">起始日期（yyyy-MM-dd），默认最近30天</param>
    /// <param name="end">截止日期（yyyy-MM-dd），默认今天</param>
    [HttpGet("daily")]
    public ActionResult<IList<DailyUsageDto>> GetDaily([FromQuery] String? start, [FromQuery] String? end)
    {
        var userId = GetCurrentUserId();

        var endDate = DateTime.TryParse(end, out var ed) ? ed.Date.AddDays(1) : DateTime.Today.AddDays(1);
        var startDate = DateTime.TryParse(start, out var sd) ? sd.Date : endDate.AddDays(-30);

        // 查询时间范围内该用户的所有会话中的 Assistant 消息（只有 AI 回复才有 Token 统计）
        var conversations = Conversation.FindAllByUserId(userId);
        var convIds = conversations.Select(c => c.Id).ToArray();
        if (convIds.Length == 0) return Ok(Array.Empty<DailyUsageDto>());

        var messages = ChatMessage.FindAll(
            ChatMessage._.ConversationId.In(convIds)
            & ChatMessage._.Role == "assistant"
            & ChatMessage._.CreateTime >= startDate
            & ChatMessage._.CreateTime < endDate,
            null, null, 0, 0);

        var daily = messages
            .GroupBy(m => m.CreateTime.Date)
            .Select(g => new DailyUsageDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Calls = g.Count(),
                PromptTokens = g.Sum(m => (Int64)m.PromptTokens),
                CompletionTokens = g.Sum(m => (Int64)m.CompletionTokens),
                TotalTokens = g.Sum(m => (Int64)m.TotalTokens),
            })
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(daily);
    }

    /// <summary>获取各模型使用分布</summary>
    [HttpGet("models")]
    public ActionResult<IList<ModelUsageDto>> GetModelUsage()
    {
        var userId = GetCurrentUserId();
        var conversations = Conversation.FindAllByUserId(userId);

        var result = conversations
            .Where(c => c.ModelId > 0)
            .GroupBy(c => c.ModelId)
            .Select(g => new ModelUsageDto
            {
                ModelId = g.Key,
                Calls = g.Sum(c => c.MessageCount),
                TotalTokens = g.Sum(c => (Int64)c.TotalTokens),
            })
            .ToList();

        return Ok(result);
    }
}
