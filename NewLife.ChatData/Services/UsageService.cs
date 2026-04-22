using NewLife.AI.Models;
using NewLife.ChatData.Entity;
using NewLife.Data;
using NewLife.Log;
using XCode;
using ChatMessage = NewLife.ChatData.Entity.ChatMessage;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatData.Services;

/// <summary>用量统计服务。记录和查询 AI 调用的 Token 消耗，支持按用户和 AppKey 双维度统计</summary>
/// <remarks>
/// <para>本服务只负责把基础 Token 用量写入 UsageRecord，不做计费与配额校验。</para>
/// <para>计费、配额扣减、超额拒绝等增强逻辑由 StarChat 商用版的 PricingService/QuotaService 完成；
/// 开源版 ChatAI 仅保留最简单的用量记录能力。</para>
/// </remarks>
/// <param name="chatSetting">AI对话系统配置</param>
/// <param name="log">日志</param>
public class UsageService(IChatSetting chatSetting, ILog log)
{
    #region 写入用量
    /// <summary>记录一次 AI 调用的用量（携带 UsageDetails，自动填充所有 Token 详情字段）</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="appKeyId">应用密钥编号，无则为0</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="messageId">消息编号</param>
    /// <param name="modelId">模型编号</param>
    /// <param name="usage">用量详情</param>
    /// <param name="source">请求来源。Chat=对话/Gateway=网关/Title/Compact/Memory/Knowledge/Image/Video/Embedding</param>
    /// <param name="projectId">项目编号，无则为0</param>
    /// <param name="parentMessageId">触发本次调用的主消息编号，0=主调用本身</param>
    public void Record(Int32 userId, Int32 appKeyId, Int64 conversationId, Int64 messageId, Int32 modelId, UsageDetails usage, String source,
        Int32 projectId = 0, Int64 parentMessageId = 0)
    {
        if (!chatSetting.EnableUsageStats) return;

        try
        {
            var entity = new UsageRecord
            {
                UserId = userId,
                ProjectId = projectId,
                AppKeyId = appKeyId,
                ConversationId = conversationId,
                MessageId = messageId,
                ParentMessageId = parentMessageId,
                ModelId = modelId,
                ModelName = ModelConfig.FindById(modelId)?.Name,
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
            entity.Insert();
        }
        catch (Exception ex)
        {
            log?.Error("写入用量记录失败: {0}", ex.Message);
        }
    }

    /// <summary>记录一次 AI 调用的用量</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="appKeyId">应用密钥编号，无则为0</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="messageId">消息编号</param>
    /// <param name="modelId">模型编号</param>
    /// <param name="inputTokens">输入Token数</param>
    /// <param name="outputTokens">输出Token数</param>
    /// <param name="totalTokens">总Token数</param>
    /// <param name="source">请求来源。Chat=对话/Gateway=网关</param>
    public void Record(Int32 userId, Int32 appKeyId, Int64 conversationId, Int64 messageId,
        Int32 modelId, Int32 inputTokens, Int32 outputTokens, Int32 totalTokens, String source)
        => Record(userId, appKeyId, conversationId, messageId, modelId,
            new UsageDetails { InputTokens = inputTokens, OutputTokens = outputTokens, TotalTokens = totalTokens },
            source);
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
        var list = UsageRecord.Search(userId, -1, -1, -1, -1, -1, null, start, end, null, new PageParameter { PageSize = 0 });

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
        var list = UsageRecord.Search(-1, -1, appKeyId, -1, -1, -1, null, start, end, null, new PageParameter { PageSize = 0 });

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
public record UsageSummaryDto(Int32 Conversations, Int32 Messages, Int32 InputTokens, Int32 OutputTokens, Int32 TotalTokens, DateTime LastActiveTime, Decimal TotalCost = 0m);

/// <summary>按日用量</summary>
public record DailyUsageDto(DateTime Date, Int32 Calls, Int32 InputTokens, Int32 OutputTokens, Int32 TotalTokens, Decimal Cost = 0m);

/// <summary>模型使用分布</summary>
public record ModelUsageDto(Int32 ModelId, Int32 Calls, Int32 TotalTokens, String ModelName = "", Decimal Cost = 0m);

/// <summary>AppKey 用量</summary>
public record AppKeyUsageDto(Int32 AppKeyId, String Name, Int32 Calls, Int32 TotalTokens, DateTime LastCallTime);

/// <summary>用量限额及当前已用量</summary>
public record UsageQuotaDto(
    Int64 DailyTokenUsed,
    Int64 MonthlyTokenUsed,
    Int64 TotalTokenUsed,
    Decimal DailyCostUsed,
    Decimal MonthlyCostUsed,
    Decimal TotalCostUsed,
    Int64 DailyTokenLimit,
    Int64 MonthlyTokenLimit,
    Int64 TotalTokenLimit,
    Decimal DailyCostLimit,
    Decimal MonthlyCostLimit,
    Decimal TotalCostLimit,
    Int32 RateLimitPerMinute);
#endregion
