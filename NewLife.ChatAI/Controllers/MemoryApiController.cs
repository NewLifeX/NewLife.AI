using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>记忆管理控制器。提供用户记忆的查询、修改接口</summary>
[Route("api/memory")]
public class MemoryApiController(MemoryService memoryService) : ChatApiControllerBase
{
    /// <summary>获取当前用户的有效记忆列表</summary>
    /// <param name="category">分类过滤（可选）：preference/habit/interest/background</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<MemoryListDto> GetMemories([FromQuery] String category, [FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var memories = category.IsNullOrEmpty()
            ? memoryService.GetActiveMemories(userId)
            : memoryService.GetMemoriesByCategory(userId, category);

        var total = memories.Count;
        var items = memories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MemoryItemDto
            {
                Id = m.Id,
                Category = m.Category,
                Key = m.Key,
                Value = m.Value,
                Confidence = m.Confidence,
                CreateTime = m.CreateTime,
                UpdateTime = m.UpdateTime,
            })
            .ToList();

        return Ok(new MemoryListDto { Total = total, Items = items, Page = page, PageSize = pageSize });
    }

    /// <summary>手动添加记忆</summary>
    /// <param name="request">添加请求</param>
    /// <returns></returns>
    [HttpPost]
    public ActionResult AddMemory([FromBody] AddMemoryRequest request)
    {
        if (request.Key.IsNullOrEmpty() || request.Value.IsNullOrEmpty())
            return BadRequest(new { code = "INVALID", message = "key/value不能为空" });

        var userId = GetCurrentUserId();
        var memory = memoryService.UpsertMemory(userId, request.Category ?? "general", request.Key, request.Value, request.Confidence, 0);
        return Ok(new { success = true, id = memory.Id });
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
}

#region DTO
/// <summary>记忆列表响应</summary>
public class MemoryListDto
{
    /// <summary>总条数</summary>
    public Int32 Total { get; set; }
    /// <summary>当前页</summary>
    public Int32 Page { get; set; }
    /// <summary>每页条数</summary>
    public Int32 PageSize { get; set; }
    /// <summary>记忆列表</summary>
    public IList<MemoryItemDto> Items { get; set; } = [];
}

/// <summary>记忆条目 DTO</summary>
public class MemoryItemDto
{
    /// <summary>记忆ID</summary>
    public Int64 Id { get; set; }
    /// <summary>分类</summary>
    public String Category { get; set; }
    /// <summary>键</summary>
    public String Key { get; set; }
    /// <summary>值</summary>
    public String Value { get; set; }
    /// <summary>置信度</summary>
    public Int32 Confidence { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>添加记忆请求</summary>
public class AddMemoryRequest
{
    /// <summary>分类</summary>
    public String Category { get; set; }
    /// <summary>键</summary>
    public String Key { get; set; }
    /// <summary>值</summary>
    public String Value { get; set; }
    /// <summary>置信度（0-100）</summary>
    public Int32 Confidence { get; set; } = 80;
}

/// <summary>更新记忆请求</summary>
public class UpdateMemoryRequest
{
    /// <summary>新的值</summary>
    public String Value { get; set; }
    /// <summary>新的置信度</summary>
    public Int32? Confidence { get; set; }
    /// <summary>新的分类</summary>
    public String Category { get; set; }
}
#endregion
