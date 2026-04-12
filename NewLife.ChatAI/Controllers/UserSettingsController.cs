using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.ChatAI.Models;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>用户设置控制器</summary>
[Route("api/user")]
public class UserSettingsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>获取当前用户资料</summary>
    [HttpGet("profile")]
    public ActionResult<UserProfileDto> GetProfile()
    {
        var user = XCode.Membership.ManageProvider.User;
        var nickname = user?.DisplayName ?? user?.Name ?? "用户";
        var account = user?.Name ?? "";
        var avatar = user?.GetAvatarUrl();

        // 角色：取主角色或所有角色名拼接
        String? role = null;
        if (user != null)
        {
            var roleIds = user.RoleIds?.SplitAsInt();
            if (roleIds?.Length > 0)
            {
                var roleNames = roleIds
                    .Select(id => Role.FindByID(id)?.Name)
                    .Where(n => !n.IsNullOrEmpty())
                    .Join("、");
                if (!roleNames.IsNullOrEmpty()) role = roleNames;
            }
            else if (user.RoleID > 0)
            {
                role = Role.FindByID(user.RoleID)?.Name;
            }
        }

        // 部门
        String? department = null;
        if (user?.DepartmentID > 0)
            department = Department.FindByID(user.DepartmentID)?.Name;

        return Ok(new UserProfileDto(
            nickname,
            account,
            avatar,
            Role: role,
            Department: department,
            Email: user?.Mail.IsNullOrEmpty() == false ? user.Mail : null,
            Mobile: user?.Mobile.IsNullOrEmpty() == false ? user.Mobile : null,
            Remark: user?.Remark.IsNullOrEmpty() == false ? user.Remark : null
        ));
    }

    /// <summary>获取用户设置</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<UserSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await chatService.GetUserSettingsAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>保存用户设置</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<UserSettingsDto>> SaveSettingsAsync([FromBody] UserSettingsDto request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateUserSettingsAsync(request, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>导出用户数据</summary>
    [HttpGet("data/export")]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var stream = await chatService.ExportUserDataAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return File(stream, "application/json", "chat-export.json");
    }

    /// <summary>清除用户数据</summary>
    [HttpDelete("data/clear")]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await chatService.ClearUserConversationsAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>导入用户数据。接受与导出格式一致的 JSON 文件</summary>
    /// <param name="file">JSON 文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("data/import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> ImportAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("请上传文件");
        if (!file.ContentType.Contains("json") && !file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest("仅支持 JSON 格式");

        using var stream = file.OpenReadStream();
        var count = await chatService.ImportUserDataAsync(GetCurrentUserId(), stream, cancellationToken).ConfigureAwait(false);
        return Ok(new { imported = count });
    }
}