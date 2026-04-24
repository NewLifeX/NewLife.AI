using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>提供商管理控制器。仅 IsSystem 系统角色管理员可访问</summary>
[Route("api/providers")]
public class ProviderApiController(ModelService modelService) : ChatApiControllerBase
{
    #region 提供商管理
    /// <summary>获取所有提供商配置列表。ApiKey 脱敏展示</summary>
    /// <returns>提供商列表，ApiKey 脱敏为 前4位***后4位</returns>
    [HttpGet]
    public ActionResult<ProviderDto[]> GetAll()
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可访问提供商配置" });

        var providers = ProviderConfig.FindAll();
        var result = providers
            .OrderByDescending(p => p.Sort)
            .ThenBy(p => p.Id)
            .Select(p => MapToDto(p))
            .ToArray();

        return Ok(result);
    }

    /// <summary>更新提供商配置。仅支持 Enable/ApiKey/Remark 三个字段，其他字段由魔方后台管理</summary>
    /// <param name="id">提供商编号</param>
    /// <param name="dto">更新数据</param>
    [HttpPut("{id}")]
    public ActionResult<ProviderDto> Update(Int32 id, [FromBody] ProviderUpdateDto dto)
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可修改提供商配置" });

        var provider = ProviderConfig.FindById(id);
        if (provider == null)
            return NotFound(new { code = "NOT_FOUND", message = "提供商不存在" });

        provider.Enable = dto.Enable;

        // ApiKey 为空字符串时保持不变，非空才更新
        if (!dto.ApiKey.IsNullOrEmpty())
            provider.ApiKey = dto.ApiKey;

        if (dto.Remark != null)
            provider.Remark = dto.Remark;

        provider.Save();

        return Ok(MapToDto(provider));
    }

    /// <summary>刷新指定提供商的模型列表。调用 ModelService.DiscoverAsync 即时同步</summary>
    /// <param name="id">提供商编号</param>
    [HttpPost("{id}/refresh")]
    public async Task<ActionResult> RefreshAsync(Int32 id)
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可刷新模型" });

        var provider = ProviderConfig.FindById(id);
        if (provider == null)
            return NotFound(new { code = "NOT_FOUND", message = "提供商不存在" });

        var message = await modelService.DiscoverAsync(provider).ConfigureAwait(false);
        return Ok(new { message });
    }
    #endregion

    #region 辅助
    private static ProviderDto MapToDto(ProviderConfig p) => new()
    {
        Id = p.Id,
        Code = p.Code ?? "",
        Name = p.Name ?? "",
        Provider = p.Provider ?? "",
        Endpoint = p.Endpoint ?? "",
        ApiKeyMasked = MaskApiKey(p.ApiKey),
        Enable = p.Enable,
        Sort = p.Sort,
        Remark = p.Remark ?? "",
    };

    /// <summary>脱敏 ApiKey。保留前4位和后4位，中间替换为 ***</summary>
    private static String MaskApiKey(String? apiKey)
    {
        if (apiKey.IsNullOrEmpty()) return "";
        if (apiKey.Length <= 8) return "***";
        return $"{apiKey[..4]}***{apiKey[^4..]}";
    }
    #endregion
}
