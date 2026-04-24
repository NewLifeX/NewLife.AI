using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>模型控制器</summary>
[Route("api/models")]
public class ModelsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>查询可用模型列表</summary>
    [HttpGet]
    public async Task<ActionResult<ModelInfoDto[]>> QueryAsync(CancellationToken cancellationToken)
    {
        var user = ManageProvider.User;
        var roleIds = user?.Roles?.Select(e => e.ID).ToArray() ?? [];
        var departmentId = user?.DepartmentID ?? 0;

        var result = await chatService.GetModelsAsync(roleIds, departmentId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>管理员查询全部模型（含未启用）。含提供商名称及特性信息</summary>
    [HttpGet("manage")]
    public ActionResult<ModelManageDto[]> GetManage()
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可查看全部模型" });

        var models = ModelConfig.FindAll();
        var result = models
            .OrderBy(m => m.ProviderId)
            .ThenByDescending(m => m.Sort)
            .ThenBy(m => m.Name)
            .Select(m => new ModelManageDto
            {
                Id = m.Id,
                ProviderId = m.ProviderId,
                ProviderName = m.ProviderInfo?.Name ?? "",
                Code = m.Code ?? "",
                Name = m.Name ?? "",
                Enable = m.Enable,
                Sort = m.Sort,
                ContextLength = m.ContextLength,
                SupportThinking = m.SupportThinking,
                SupportFunctionCalling = m.SupportFunctionCalling,
                SupportVision = m.SupportVision,
                SupportAudio = m.SupportAudio,
                SupportImageGeneration = m.SupportImageGeneration,
                SupportVideoGeneration = m.SupportVideoGeneration,
            })
            .ToArray();

        return Ok(result);
    }

    /// <summary>更新模型配置。含启停及特性标记</summary>
    /// <param name="id">模型编号</param>
    /// <param name="dto">更新数据</param>
    [HttpPut("{id}/settings")]
    public ActionResult<ModelManageDto> UpdateSettings(Int32 id, [FromBody] ModelSettingsDto dto)
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可修改模型配置" });

        var model = ModelConfig.FindById(id);
        if (model == null)
            return NotFound(new { code = "NOT_FOUND", message = "模型不存在" });

        model.Enable = dto.Enable;
        model.ContextLength = dto.ContextLength;
        model.SupportThinking = dto.SupportThinking;
        model.SupportFunctionCalling = dto.SupportFunctionCalling;
        model.SupportVision = dto.SupportVision;
        model.SupportAudio = dto.SupportAudio;
        model.SupportImageGeneration = dto.SupportImageGeneration;
        model.SupportVideoGeneration = dto.SupportVideoGeneration;
        model.Save();

        return Ok(new ModelManageDto
        {
            Id = model.Id,
            ProviderId = model.ProviderId,
            ProviderName = model.ProviderInfo?.Name ?? "",
            Code = model.Code ?? "",
            Name = model.Name ?? "",
            Enable = model.Enable,
            Sort = model.Sort,
            ContextLength = model.ContextLength,
            SupportThinking = model.SupportThinking,
            SupportFunctionCalling = model.SupportFunctionCalling,
            SupportVision = model.SupportVision,
            SupportAudio = model.SupportAudio,
            SupportImageGeneration = model.SupportImageGeneration,
            SupportVideoGeneration = model.SupportVideoGeneration,
        });
    }
}
