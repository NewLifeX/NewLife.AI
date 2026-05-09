using NewLife.AI.Interfaces;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace NewLife.ChatAI.Services;

/// <summary>用量统计服务。记录和查询 AI 调用的 Token 消耗，支持按用户和 AppKey 双维度统计</summary>
/// <remarks>实例化用量统计服务</remarks>
/// <param name="chatSetting">AI对话系统配置</param>
/// <param name="log">日志</param>
public class UsageService(IChatSetting chatSetting, ILog log)
{
    #region 写入用量
    /// <summary>记录一次 AI 调用的用量（携带 UsageDetails，自动填充所有 Token 详情字段）</summary>
    /// <param name="conversation">会话</param>
    /// <param name="message">消息</param>
    /// <param name="model">模型</param>
    /// <param name="usage">用量详情</param>
    /// <param name="source">请求来源。Chat=对话/Gateway=网关</param>
    /// <returns>写入的用量记录，已禁用或异常时返回 null</returns>
    public virtual UsageRecord? Record(IConversation conversation, IChatMessage? message, IModelConfig? model, UsageDetails usage, String source)
    {
        if (!chatSetting.EnableUsageStats) return null;

        try
        {
            var rec = new UsageRecord
            {
                UserId = conversation.UserId,
                AppKeyId = conversation.AppKeyId,
                ConversationId = conversation.Id,
                MessageId = message?.Id ?? 0,
                ModelId = model?.Id ?? 0,
                ModelName = model?.Name,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                TotalTokens = usage.TotalTokens,
                CachedInputTokens = usage.CachedInputTokens,
                ReasoningTokens = usage.ReasoningTokens,
                InputAudioTokens = usage.InputAudioTokens,
                InputTextTokens = usage.InputTextTokens,
                OutputAudioTokens = usage.OutputAudioTokens,
                OutputTextTokens = usage.OutputTextTokens,
                ElapsedMs = usage.ElapsedMs,
                Source = source,
            };
            rec.Insert();
            return rec;
        }
        catch (Exception ex)
        {
            log?.Error("写入用量记录失败: {0}", ex.Message);
            return null;
        }
    }
    #endregion

    #region 用户维度查询
    /// <summary>获取用户累计用量摘要</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    public UsageSummaryDto GetSummary(Int32 userId)
    {
        var conversations = Conversation.CountByUserId(userId);
        var messages = DbChatMessage.CountByUserId(userId);

        var records = UsageRecord.FindAllByUserId(userId);
        var totalPrompt = records.Sum(e => e.InputTokens);
        var totalCompletion = records.Sum(e => e.OutputTokens);
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
        var list = UsageRecord.Search(userId, -1, -1, -1, null, start, end, null, new PageParameter { PageSize = 0 });

        return list
            .GroupBy(e => e.CreateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyUsageDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.InputTokens),
                g.Sum(e => e.OutputTokens),
                g.Sum(e => e.TotalTokens)))
            .ToList();
    }

    /// <summary>获取各模型使用分布</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    public IList<ModelUsageDto> GetModelUsage(Int32 userId)
    {
        var list = UsageRecord.FindAllByUserId(userId);

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
            var records = UsageRecord.FindAllByAppKeyId(key.Id);
            result.Add(new AppKeyUsageDto(
                key.Id,
                key.Name ?? "",
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
        var list = UsageRecord.Search(-1, appKeyId, -1, -1, null, start, end, null, new PageParameter { PageSize = 0 });

        return list
            .GroupBy(e => e.CreateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyUsageDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.InputTokens),
                g.Sum(e => e.OutputTokens),
                g.Sum(e => e.TotalTokens)))
            .ToList();
    }
    #endregion
}

#region DTO 定义
/// <summary>用量摘要</summary>
public record UsageSummaryDto(Int32 Conversations, Int32 Messages, Int32 InputTokens, Int32 OutputTokens, Int32 TotalTokens, DateTime LastActiveTime);

/// <summary>按日用量</summary>
public record DailyUsageDto(DateTime Date, Int32 Calls, Int32 InputTokens, Int32 OutputTokens, Int32 TotalTokens);

/// <summary>模型使用分布</summary>
public record ModelUsageDto(Int32 ModelId, Int32 Calls, Int32 TotalTokens);

/// <summary>AppKey 用量</summary>
public record AppKeyUsageDto(Int32 AppKeyId, String Name, Int32 Calls, Int32 TotalTokens, DateTime LastCallTime);
#endregion
