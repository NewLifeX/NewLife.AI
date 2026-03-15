using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Data;

namespace NewLife.ChatAI.Controllers;

/// <summary>记忆管理控制器。提供用户记忆和画像的查询、修改接口</summary>
[Route("api/memory")]
public class MemoryApiController(MemoryService memoryService, UserProfileService profileService) : ChatApiControllerBase
{
    #region 记忆接口
    /// <summary>获取当前用户的有效记忆列表</summary>
    /// <param name="category">分类过滤（可选）：preference/habit/interest/background</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<MemoryListDto> GetMemories([FromQuery] String? category, [FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var memories = category.IsNullOrEmpty()
            ? memoryService.GetActiveMemories(userId)
            : memoryService.GetMemoriesByCategory(userId, category!);

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
    #endregion

    #region 画像接口
    /// <summary>获取当前用户画像</summary>
    /// <returns></returns>
    [HttpGet("/api/profile")]
    public ActionResult<UserProfileDto> GetProfile()
    {
        var userId = GetCurrentUserId();
        var profile = profileService.GetOrCreateProfile(userId);
        var tags = profileService.GetTags(userId);

        return Ok(new UserProfileDto
        {
            Id = profile.Id,
            Summary = profile.Summary,
            Preferences = profile.Preferences,
            Habits = profile.Habits,
            Interests = profile.Interests,
            MemoryCount = profile.MemoryCount,
            LastAnalyzeTime = profile.LastAnalyzeTime,
            AnalyzeCount = profile.AnalyzeCount,
            Tags = tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Category = t.Category, Weight = t.Weight }).ToList(),
        });
    }

    /// <summary>获取当前用户标签列表</summary>
    /// <param name="category">分类过滤（可选）</param>
    /// <returns></returns>
    [HttpGet("/api/profile/tags")]
    public ActionResult<IList<TagDto>> GetTags([FromQuery] String? category)
    {
        var userId = GetCurrentUserId();
        var tags = profileService.GetTags(userId, category);
        return Ok(tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Category = t.Category, Weight = t.Weight }).ToList());
    }

    /// <summary>删除标签</summary>
    /// <param name="id">标签ID</param>
    /// <returns></returns>
    [HttpDelete("/api/profile/tags/{id:int}")]
    public ActionResult DeleteTag([FromRoute] Int32 id)
    {
        var tag = UserTag.FindById(id);
        if (tag == null) return NotFound(new { code = "NOT_FOUND", message = "标签不存在" });
        if (tag.UserId != GetCurrentUserId()) return Forbid();

        profileService.DeleteTag(id);
        return Ok(new { success = true });
    }
    #endregion

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
        public String? Category { get; set; }
        /// <summary>键</summary>
        public String? Key { get; set; }
        /// <summary>值</summary>
        public String? Value { get; set; }
        /// <summary>置信度</summary>
        public Int32 Confidence { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; }
        /// <summary>更新时间</summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>更新记忆请求</summary>
    public class UpdateMemoryRequest
    {
        /// <summary>新的值</summary>
        public String? Value { get; set; }
        /// <summary>新的置信度</summary>
        public Int32? Confidence { get; set; }
        /// <summary>新的分类</summary>
        public String? Category { get; set; }
    }

    /// <summary>用户画像 DTO</summary>
    public class UserProfileDto
    {
        /// <summary>画像ID</summary>
        public Int32 Id { get; set; }
        /// <summary>总结</summary>
        public String? Summary { get; set; }
        /// <summary>偏好 JSON</summary>
        public String? Preferences { get; set; }
        /// <summary>习惯 JSON</summary>
        public String? Habits { get; set; }
        /// <summary>兴趣 JSON</summary>
        public String? Interests { get; set; }
        /// <summary>记忆数量</summary>
        public Int32 MemoryCount { get; set; }
        /// <summary>最后分析时间</summary>
        public DateTime LastAnalyzeTime { get; set; }
        /// <summary>分析次数</summary>
        public Int32 AnalyzeCount { get; set; }
        /// <summary>标签列表</summary>
        public IList<TagDto> Tags { get; set; } = [];
    }

    /// <summary>标签 DTO</summary>
    public class TagDto
    {
        /// <summary>标签ID</summary>
        public Int32 Id { get; set; }
        /// <summary>标签名</summary>
        public String? Name { get; set; }
        /// <summary>分类</summary>
        public String? Category { get; set; }
        /// <summary>权重</summary>
        public Int32 Weight { get; set; }
    }
    #endregion
}
