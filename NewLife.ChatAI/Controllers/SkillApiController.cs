using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>技能API控制器。提供技能列表、SkillBar数据和会话技能切换</summary>
[Route("api/skills")]
public class SkillApiController(SkillService skillService) : ChatApiControllerBase
{
    /// <summary>获取全部启用的技能列表</summary>
    /// <param name="category">分类筛选（可选）</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IList<SkillDto>> GetAll([FromQuery] String? category)
    {
        var list = skillService.GetAllSkills(category);
        return Ok(list.Select(ToDto).ToList());
    }

    /// <summary>获取SkillBar展示列表（最近使用+系统技能）</summary>
    /// <returns></returns>
    [HttpGet("/api/user/skills")]
    public ActionResult<IList<SkillDto>> GetUserSkills()
    {
        var userId = GetCurrentUserId();
        var list = skillService.GetSkillBarList(userId);
        return Ok(list.Select(ToDto).ToList());
    }

    /// <summary>获取技能详情</summary>
    /// <param name="id">技能编号</param>
    /// <returns></returns>
    [HttpGet("{id:int}")]
    public ActionResult<SkillDto> GetById([FromRoute] Int32 id)
    {
        var skill = Skill.FindById(id);
        if (skill == null || !skill.Enable) return NotFound();
        return Ok(ToDto(skill));
    }

    /// <summary>获取分类列表</summary>
    /// <returns></returns>
    [HttpGet("categories")]
    public ActionResult<IDictionary<String, String>> GetCategories()
    {
        return Ok(skillService.GetCategories());
    }

    /// <summary>切换会话使用的技能</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">技能切换请求</param>
    /// <returns></returns>
    [HttpPut("/api/conversations/{conversationId:long}/skill")]
    public ActionResult SetConversationSkill([FromRoute] Int64 conversationId, [FromBody] SetSkillRequest request)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null) return NotFound();

        var userId = GetCurrentUserId();
        if (conversation.UserId != userId) return Forbid();

        if (request.SkillId > 0)
        {
            var skill = Skill.FindById(request.SkillId);
            if (skill == null || !skill.Enable) return BadRequest("技能不存在或已禁用");
        }

        conversation.SkillId = request.SkillId;
        conversation.Update();

        // 记录使用
        if (request.SkillId > 0)
            skillService.RecordUsage(userId, request.SkillId);

        return Ok(new { conversationId, skillId = request.SkillId });
    }

    /// <summary>转换为DTO</summary>
    private static SkillDto ToDto(Skill skill) => new()
    {
        Id = skill.Id,
        Code = skill.Code,
        Name = skill.Name,
        Icon = skill.Icon,
        Category = skill.Category,
        Description = skill.Description,
        IsSystem = skill.IsSystem,
    };
}

/// <summary>技能DTO</summary>
public class SkillDto
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>编码</summary>
    public String? Code { get; set; }

    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>图标</summary>
    public String? Icon { get; set; }

    /// <summary>分类</summary>
    public String? Category { get; set; }

    /// <summary>描述</summary>
    public String? Description { get; set; }

    /// <summary>系统内置</summary>
    public Boolean IsSystem { get; set; }
}

/// <summary>切换技能请求</summary>
public record SetSkillRequest(Int32 SkillId);
