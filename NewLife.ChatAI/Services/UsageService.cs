using NewLife.ChatAI.Entity;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace NewLife.ChatAI.Services;

/// <summary>用量统计服务。记录和查询 AI 调用的 Token 消耗，支持按用户和 AppKey 双维度统计</summary>
public class UsageService
{
    #region 属性
    private readonly ILog _log;
    #endregion

    #region 构造
    /// <summary>实例化用量统计服务</summary>
    /// <param name="log">日志</param>
    public UsageService(ILog log) => _log = log;
    #endregion

    #region 写入用量
    /// <summary>记录一次 AI 调用的用量</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="appKeyId">应用密钥编号，无则为0</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="messageId">消息编号</param>
    /// <param name="modelId">模型编号</param>
    /// <param name="promptTokens">提示Token数</param>
    /// <param name="completionTokens">回复Token数</param>
    /// <param name="totalTokens">总Token数</param>
    /// <param name="source">请求来源。Chat=对话/Gateway=网关</param>
    public void Record(Int32 userId, Int32 appKeyId, Int64 conversationId, Int64 messageId,
        Int32 modelId, Int32 promptTokens, Int32 completionTokens, Int32 totalTokens, String source)
    {
        if (!ChatSetting.Current.EnableUsageStats) return;

        try
        {
            var entity = new UsageRecord
            {
                UserId = userId,
                AppKeyId = appKeyId,
                ConversationId = conversationId,
                MessageId = messageId,
                ModelId = modelId,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                Source = source,
            };
            entity.Insert();
        }
        catch (Exception ex)
        {
            _log?.Error("写入用量记录失败: {0}", ex.Message);
        }
    }
    #endregion

    #region 用户维度查询
    /// <summary>获取用户累计用量摘要</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    public UsageSummaryDto GetSummary(Int32 userId)
    {
        var conversations = (Int32)Conversation.FindCount(Conversation._.UserId == userId);
        var messages = (Int32)ChatMessage.FindCount(
            ChatMessage._.ConversationId.In(Conversation.FindSQLWithKey(Conversation._.UserId == userId)));

        // 聚合用量
        var records = UsageRecord.FindAll(UsageRecord._.UserId == userId);
        var totalPrompt = records.Sum(e => e.PromptTokens);
        var totalCompletion = records.Sum(e => e.CompletionTokens);
        var totalTokens = records.Sum(e => e.TotalTokens);
        var lastActive = records.Count > 0 ? records.Max(e => e.CreateTime) : DateTime.MinValue;

        return new UsageSummaryDto(conversations, messages, totalPrompt, totalCompletion, totalTokens, lastActive);
    }

    /// <summary>获取按日用量明细</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns></returns>
    public IList<DailyUsageDto> GetDailyUsage(Int32 userId, DateTime start, DateTime end)
    {
        var list = UsageRecord.Search(userId, -1, -1, -1, start, end, null, new PageParameter { PageSize = 0 });

        return list
            .GroupBy(e => e.CreateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyUsageDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.PromptTokens),
                g.Sum(e => e.CompletionTokens),
                g.Sum(e => e.TotalTokens)))
            .ToList();
    }

    /// <summary>获取各模型使用分布</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    public IList<ModelUsageDto> GetModelUsage(Int32 userId)
    {
        var list = UsageRecord.FindAll(UsageRecord._.UserId == userId);

        return list
            .GroupBy(e => e.ModelId)
            .Select(g => new ModelUsageDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.TotalTokens)))
            .OrderByDescending(e => e.Calls)
            .ToList();
    }
    #endregion

    #region AppKey 维度查询
    /// <summary>获取各 AppKey 用量明细</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    public IList<AppKeyUsageDto> GetAppKeyUsage(Int32 userId)
    {
        var keys = AppKey.FindAllByUserId(userId);
        var result = new List<AppKeyUsageDto>();

        foreach (var key in keys)
        {
            var records = UsageRecord.FindAll(UsageRecord._.AppKeyId == key.Id);
            result.Add(new AppKeyUsageDto(
                key.Id,
                key.Name,
                records.Count,
                records.Sum(e => e.TotalTokens),
                key.LastCallTime));
        }

        return result.OrderByDescending(e => e.Calls).ToList();
    }

    /// <summary>获取指定 AppKey 的按日用量</summary>
    /// <param name="appKeyId">应用密钥编号</param>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns></returns>
    public IList<DailyUsageDto> GetAppKeyDailyUsage(Int32 appKeyId, DateTime start, DateTime end)
    {
        var list = UsageRecord.Search(-1, appKeyId, -1, -1, start, end, null, new PageParameter { PageSize = 0 });

        return list
            .GroupBy(e => e.CreateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyUsageDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.PromptTokens),
                g.Sum(e => e.CompletionTokens),
                g.Sum(e => e.TotalTokens)))
            .ToList();
    }
    #endregion
}

#region DTO 定义
/// <summary>用量摘要</summary>
public record UsageSummaryDto(Int32 Conversations, Int32 Messages, Int32 PromptTokens, Int32 CompletionTokens, Int32 TotalTokens, DateTime LastActiveTime);

/// <summary>按日用量</summary>
public record DailyUsageDto(DateTime Date, Int32 Calls, Int32 PromptTokens, Int32 CompletionTokens, Int32 TotalTokens);

/// <summary>模型使用分布</summary>
public record ModelUsageDto(Int32 ModelId, Int32 Calls, Int32 TotalTokens);

/// <summary>AppKey 用量</summary>
public record AppKeyUsageDto(Int32 AppKeyId, String Name, Int32 Calls, Int32 TotalTokens, DateTime LastCallTime);
#endregion
