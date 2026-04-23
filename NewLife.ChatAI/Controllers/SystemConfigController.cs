using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>系统公开配置接口。无需登录即可访问，供前端初始化时读取站点标题等配置</summary>
[Route("api/system")]
public class SystemConfigController(ChatSetting chatSetting) : ChatApiControllerBase
{

    /// <summary>获取系统公开配置</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<SystemConfigDto> GetConfig()
    {

        // 从推荐问题表读取启用的问题，按排序号倒序、编号倒序排列
        var questions = SuggestedQuestion.FindAllCachedEnabled()
            .OrderByDescending(q => q.Sort)
            .ThenByDescending(q => q.Id)
            .Select(q => new SuggestedQuestionDto
            {
                Question = q.Question,
                Icon = q.Icon,
                Color = q.Color,
            })
            .ToArray();

        return Ok(new SystemConfigDto
        {
            AppName = chatSetting.Name,
            SiteTitle = chatSetting.SiteTitle,
            LogoUrl = chatSetting.LogoUrl,
            SuggestedQuestions = questions,
        });
    }
}
