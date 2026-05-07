using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using NewLife.Data;

namespace NewLife.ChatAI.Controllers;

/// <summary>记忆管理控制器。提供用户记忆和画像的查询、修改接口</summary>
[Route("api/memory")]
public class MemoryApiController(MemoryService memoryService) : ChatApiControllerBase
{
    #region 记忆接口
    /// <summary>获取当前用户的有效记忆列表</summary>
    /// <param name="category">分类过滤（可选）：偏好/习惯/兴趣/背景</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<MemoryListDto> GetMemories([FromQuery] String? category, [FromQuery] Int32 pageIndex = 1, [FromQuery] Int32 pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var pg = new PageParameter { PageIndex = pageIndex, PageSize = pageSize, RetrieveTotalCount = true };
        var memories = UserMemory.Search(userId, category, pg);
        var items = memories.Select(m => new MemoryItemDto
        {
            Id = m.Id,
            Category = m.Category,
            Key = m.Key,
            Value = m.Value,
            Confidence = m.Confidence,
            Enable = m.Enable,
            CreateTime = m.CreateTime,
            UpdateTime = m.UpdateTime,
        }).ToList();
        return Ok(new MemoryListDto { Total = (Int32)pg.TotalCount, Items = items, PageIndex = pg.PageIndex, PageSize = pg.PageSize });
    }

    /// <summary>更新记忆内容</summary>
    /// <param name="id">记忆ID</param>
    /// <param name="request">更新请求</param>
    /// <returns></returns>
    [HttpPut("{id:long}")]
    public ActionResult UpdateMemory([FromRoute] Int64 id, [FromBody] UpdateMemoryRequest request)
    {
        var userId = GetCurrentUserId();
        var memory = UserMemory.FindById(id);
        if (memory == null) return NotFound(new { code = "NOT_FOUND", message = "记忆不存在" });
        if (memory.UserId != userId) return Forbid();

        if (request.Value != null) memory.Value = request.Value;
        if (request.Confidence.HasValue) memory.Confidence = request.Confidence.Value;
        if (request.Category != null) memory.Category = request.Category;
        if (request.Enable.HasValue) memory.Enable = request.Enable.Value;
        memory.Update();

        return Ok(new { success = true });
    }

    /// <summary>停用（软删除）记忆</summary>
    /// <param name="id">记忆ID</param>
    /// <returns></returns>
    [HttpDelete("{id:long}")]
    public ActionResult DeactivateMemory([FromRoute] Int64 id)
    {
        var userId = GetCurrentUserId();
        var memory = UserMemory.FindById(id);
        if (memory == null) return NotFound(new { code = "NOT_FOUND", message = "记忆不存在" });
        if (memory.UserId != userId) return Forbid();

        memoryService.Deactivate(id);
        return Ok(new { success = true });
    }
    #endregion
}
