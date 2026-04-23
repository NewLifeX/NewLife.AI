using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.ChatAI.Entity;

namespace NewLife.ChatAI.Controllers;

/// <summary>对话预设控制器。管理模型+技能+SystemPrompt组合的预设模板</summary>
[Route("api/presets")]
public class PresetsController : ChatApiControllerBase
{
    /// <summary>获取用户预设列表（含系统级）</summary>
    [HttpGet]
    public ActionResult<IList<PresetDto>> GetAll()
    {
        var userId = GetCurrentUserId();
        var list = ChatPreset.FindAllAvailable(userId);
        var result = list.Select(ToDto).ToList();
        return Ok(result);
    }

    /// <summary>创建预设</summary>
    /// <param name="request">预设信息</param>
    [HttpPost]
    public ActionResult<PresetDto> Create([FromBody] PresetRequest request)
    {
        var userId = GetCurrentUserId();

        var modelId = request.ModelId;
        if (modelId == 0 && !request.ModelName.IsNullOrEmpty())
            modelId = ModelConfig.FindByCodeOrName(request.ModelName)?.Id ?? 0;

        var entity = new ChatPreset
        {
            UserId = userId,
            Name = request.Name,
            ModelId = modelId,
            ModelName = request.ModelName,
            SkillCode = request.SkillCode,
            SystemPrompt = request.SystemPrompt,
            Prompt = request.Prompt,
            ThinkingMode = (ThinkingMode)request.ThinkingMode,
            IsDefault = request.IsDefault,
            Sort = request.Sort,
            Enable = true,
        };
        entity.Insert();

        return Ok(ToDto(entity));
    }

    /// <summary>更新预设</summary>
    /// <param name="id">预设编号</param>
    /// <param name="request">预设信息</param>
    [HttpPut("{id}")]
    public ActionResult<PresetDto> Update(Int32 id, [FromBody] PresetRequest request)
    {
        var userId = GetCurrentUserId();
        var entity = ChatPreset.FindById(id);
        if (entity == null) return NotFound();
        if (entity.UserId != userId && entity.UserId != 0) return Forbid();

        // 系统级预设不允许普通用户修改
        if (entity.UserId == 0) return Forbid();

        var modelId = request.ModelId;
        if (modelId == 0 && !request.ModelName.IsNullOrEmpty())
            modelId = ModelConfig.FindByCodeOrName(request.ModelName)?.Id ?? 0;

        entity.Name = request.Name;
        entity.ModelId = modelId;
        entity.ModelName = request.ModelName;
        entity.SkillCode = request.SkillCode;
        entity.SystemPrompt = request.SystemPrompt;
        entity.Prompt = request.Prompt;
        entity.ThinkingMode = (ThinkingMode)request.ThinkingMode;
        entity.IsDefault = request.IsDefault;
        entity.Sort = request.Sort;
        entity.Update();

        return Ok(ToDto(entity));
    }

    /// <summary>删除预设</summary>
    /// <param name="id">预设编号</param>
    [HttpDelete("{id}")]
    public IActionResult Delete(Int32 id)
    {
        var userId = GetCurrentUserId();
        var entity = ChatPreset.FindById(id);
        if (entity == null) return NotFound();
        if (entity.UserId != userId) return Forbid();

        entity.Delete();
        return NoContent();
    }

    private static PresetDto ToDto(ChatPreset entity) => new(
        entity.Id,
        entity.Name ?? "",
        entity.ModelId,
        entity.ModelName,
        entity.SkillCode,
        entity.SystemPrompt,
        entity.Prompt,
        (Int32)entity.ThinkingMode,
        entity.IsDefault,
        entity.Sort
    );
}

/// <summary>预设DTO</summary>
public record PresetDto(
    Int32 Id,
    String Name,
    Int32 ModelId,
    String? ModelName,
    String? SkillCode,
    String? SystemPrompt,
    String? Prompt,
    Int32 ThinkingMode,
    Boolean IsDefault,
    Int32 Sort
);

/// <summary>预设请求模型</summary>
public class PresetRequest
{
    /// <summary>名称</summary>
    public String Name { get; set; } = "";

    /// <summary>模型</summary>
    public Int32 ModelId { get; set; }

    /// <summary>模型名称。ModelId为0时按此名称匹配Code或Name</summary>
    public String? ModelName { get; set; }

    /// <summary>技能编码</summary>
    public String? SkillCode { get; set; }

    /// <summary>系统提示词</summary>
    public String? SystemPrompt { get; set; }

    /// <summary>提示词。选中预设时自动填入用户输入框</summary>
    public String? Prompt { get; set; }

    /// <summary>思考模式</summary>
    public Int32 ThinkingMode { get; set; }

    /// <summary>是否默认</summary>
    public Boolean IsDefault { get; set; }

    /// <summary>排序</summary>
    public Int32 Sort { get; set; }
}
