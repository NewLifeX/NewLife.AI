using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.Security;

namespace NewLife.ChatAI.Controllers;

/// <summary>AppKey 管理控制器。应用密钥的创建、查看、更新、删除</summary>
[ApiController]
[Route("api/appkeys")]
public class AppKeyApiController : ControllerBase
{
    /// <summary>获取当前用户的 AppKey 列表</summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IList<AppKeyResponseDto>> GetList()
    {
        // 本期暂不启用登录鉴权，UserId 固定为 0
        var list = AppKey.FindAllByUserId(0);
        var items = list.Select(e => new AppKeyResponseDto(
            e.Id, e.Name, MaskSecret(e.Secret), e.Enable,
            e.ExpireTime.Year > 2000 ? e.ExpireTime : (DateTime?)null,
            e.Calls, e.TotalTokens, e.LastCallTime, e.CreateTime)).ToList();
        return Ok(items);
    }

    /// <summary>创建 AppKey</summary>
    /// <param name="request">创建请求</param>
    /// <returns></returns>
    [HttpPost]
    public ActionResult<AppKeyCreateResponseDto> Create([FromBody] CreateAppKeyRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { code = "INVALID_REQUEST", message = "名称不能为空" });

        // 生成 sk- 前缀的随机密钥
        var secret = "sk-" + Rand.NextString(48);

        var entity = new AppKey
        {
            UserId = 0,
            Name = request.Name.Trim(),
            Secret = secret,
            Enable = true,
        };

        if (request.ExpireTime != null)
            entity.ExpireTime = request.ExpireTime.Value;

        entity.Insert();

        // 创建时返回完整密钥（仅此一次）
        return Ok(new AppKeyCreateResponseDto(entity.Id, entity.Name, secret, entity.CreateTime));
    }

    /// <summary>更新 AppKey</summary>
    /// <param name="id">AppKey 编号</param>
    /// <param name="request">更新请求</param>
    /// <returns></returns>
    [HttpPut("{id:int}")]
    public ActionResult<AppKeyResponseDto> Update([FromRoute] Int32 id, [FromBody] UpdateAppKeyRequest request)
    {
        var entity = AppKey.FindById(id);
        if (entity == null) return NotFound();

        if (!String.IsNullOrWhiteSpace(request.Name))
            entity.Name = request.Name.Trim();
        if (request.Enable != null)
            entity.Enable = request.Enable.Value;
        if (request.ExpireTime != null)
            entity.ExpireTime = request.ExpireTime.Value;

        entity.Update();

        return Ok(new AppKeyResponseDto(
            entity.Id, entity.Name, MaskSecret(entity.Secret), entity.Enable,
            entity.ExpireTime.Year > 2000 ? entity.ExpireTime : null,
            entity.Calls, entity.TotalTokens, entity.LastCallTime, entity.CreateTime));
    }

    /// <summary>删除 AppKey</summary>
    /// <param name="id">AppKey 编号</param>
    /// <returns></returns>
    [HttpDelete("{id:int}")]
    public IActionResult Delete([FromRoute] Int32 id)
    {
        var entity = AppKey.FindById(id);
        if (entity == null) return NotFound();

        entity.Delete();
        return NoContent();
    }

    #region 辅助
    /// <summary>掩码密钥，仅显示前6位和后4位</summary>
    /// <param name="secret">密钥</param>
    /// <returns></returns>
    private static String MaskSecret(String secret)
    {
        if (String.IsNullOrEmpty(secret) || secret.Length <= 10)
            return "sk-****";
        return secret.Substring(0, 6) + "****" + secret.Substring(secret.Length - 4);
    }
    #endregion
}

#region DTO 定义
/// <summary>创建 AppKey 请求</summary>
public record CreateAppKeyRequest(String Name, DateTime? ExpireTime);

/// <summary>更新 AppKey 请求</summary>
public record UpdateAppKeyRequest(String? Name, Boolean? Enable, DateTime? ExpireTime);

/// <summary>AppKey 响应（不含完整密钥）</summary>
public record AppKeyResponseDto(Int32 Id, String Name, String SecretMask, Boolean Enable,
    DateTime? ExpireTime, Int64 Calls, Int64 TotalTokens, DateTime LastCallTime, DateTime CreateTime);

/// <summary>AppKey 创建响应（含完整密钥，仅此一次）</summary>
public record AppKeyCreateResponseDto(Int32 Id, String Name, String Secret, DateTime CreateTime);
#endregion
