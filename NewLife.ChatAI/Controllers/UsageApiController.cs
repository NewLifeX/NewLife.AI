using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>用量统计控制器</summary>
[Route("api/usage")]
public class UsageApiController(UsageService usageService) : ChatApiControllerBase
{
    /// <summary>获取用户累计用量摘要</summary>
    /// <returns></returns>
    [HttpGet("summary")]
    public ActionResult<UsageSummaryDto> GetSummary()
    {
        var result = usageService.GetSummary(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>获取按日用量明细</summary>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns></returns>
    [HttpGet("daily")]
    public ActionResult<IList<DailyUsageDto>> GetDailyUsage([FromQuery] DateTime? start, [FromQuery] DateTime? end)
    {
        var s = start ?? DateTime.Today.AddDays(-30);
        var e = end ?? DateTime.Today.AddDays(1);
        var result = usageService.GetDailyUsage(GetCurrentUserId(), s, e);
        return Ok(result);
    }

    /// <summary>获取各模型使用分布</summary>
    /// <returns></returns>
    [HttpGet("models")]
    public ActionResult<IList<ModelUsageDto>> GetModelUsage()
    {
        var result = usageService.GetModelUsage(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>获取各 AppKey 用量明细</summary>
    /// <returns></returns>
    [HttpGet("appkeys")]
    public ActionResult<IList<AppKeyUsageDto>> GetAppKeyUsage()
    {
        var result = usageService.GetAppKeyUsage(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>获取指定 AppKey 的按日用量</summary>
    /// <param name="id">AppKey 编号</param>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns></returns>
    [HttpGet("appkeys/{id:int}/daily")]
    public ActionResult<IList<DailyUsageDto>> GetAppKeyDailyUsage([FromRoute] Int32 id, [FromQuery] DateTime? start, [FromQuery] DateTime? end)
    {
        var s = start ?? DateTime.Today.AddDays(-30);
        var e = end ?? DateTime.Today.AddDays(1);
        var result = usageService.GetAppKeyDailyUsage(id, s, e);
        return Ok(result);
    }
}
