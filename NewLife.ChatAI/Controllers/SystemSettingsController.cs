using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.ChatData.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>系统设置控制器。仅 IsSystem 系统角色管理员可访问</summary>
[Route("api/system/settings")]
public class SystemSettingsController : ChatApiControllerBase
{
    /// <summary>获取系统设置</summary>
    /// <returns>当前 ChatSetting 配置，含可用模型列表</returns>
    [HttpGet]
    public ActionResult<SystemSettingsDto> Get()
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可访问系统设置" });

        var s = ChatSetting.Current;

        // 构建可用模型下拉列表
        var models = new List<ModelOptionDto> { new(0, "默认（第一个可用模型）") };
        foreach (var m in ModelConfig.FindAllEnabled())
            models.Add(new ModelOptionDto(m.Id, m.Name));

        return Ok(new SystemSettingsDto
        {
            // 基本配置
            Name = s.Name,
            SiteTitle = s.SiteTitle,
            LogoUrl = s.LogoUrl,
            AutoGenerateTitle = s.AutoGenerateTitle,
            // 对话默认
            DefaultModel = s.DefaultModel,
            DefaultThinkingMode = (Int32)s.DefaultThinkingMode,
            DefaultContextRounds = s.DefaultContextRounds,
            // 上传与分享
            MaxAttachmentSize = s.MaxAttachmentSize,
            MaxAttachmentCount = s.MaxAttachmentCount,
            AllowedExtensions = s.AllowedExtensions,
            DefaultImageSize = s.DefaultImageSize,
            ShareExpireDays = s.ShareExpireDays,
            // API 网关
            EnableGateway = s.EnableGateway,
            EnableGatewayPipeline = s.EnableGatewayPipeline,
            GatewayRateLimit = s.GatewayRateLimit,
            UpstreamRetryCount = s.UpstreamRetryCount,
            EnableGatewayRecording = s.EnableGatewayRecording,
            // 工具与能力
            EnableFunctionCalling = s.EnableFunctionCalling,
            EnableMcp = s.EnableMcp,
            EnableSuggestedQuestionCache = s.EnableSuggestedQuestionCache,
            StreamingSpeed = s.StreamingSpeed,
            ToolAdvertiseThreshold = s.ToolAdvertiseThreshold,
            // 功能开关
            EnableUsageStats = s.EnableUsageStats,
            BackgroundGeneration = s.BackgroundGeneration,
            MaxMessagesPerMinute = s.MaxMessagesPerMinute,
            // 自学习
            EnableAutoLearning = s.EnableAutoLearning,
            LearningModel = s.LearningModel,
            MinLearningContentLength = s.MinLearningContentLength,
            // 可用模型列表
            Models = [.. models],
        });
    }

    /// <summary>保存系统设置</summary>
    /// <param name="dto">要更新的设置字段（null 字段保持原值）</param>
    [HttpPut]
    public ActionResult Save([FromBody] SystemSettingsUpdateDto dto)
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可修改系统设置" });

        var s = ChatSetting.Current;

        // 基本配置
        if (dto.Name != null) s.Name = dto.Name;
        if (dto.SiteTitle != null) s.SiteTitle = dto.SiteTitle;
        if (dto.LogoUrl != null) s.LogoUrl = dto.LogoUrl;
        if (dto.AutoGenerateTitle.HasValue) s.AutoGenerateTitle = dto.AutoGenerateTitle.Value;
        // 对话默认
        if (dto.DefaultModel.HasValue) s.DefaultModel = dto.DefaultModel.Value;
        if (dto.DefaultThinkingMode.HasValue) s.DefaultThinkingMode = (ThinkingMode)dto.DefaultThinkingMode.Value;
        if (dto.DefaultContextRounds.HasValue) s.DefaultContextRounds = dto.DefaultContextRounds.Value;
        // 上传与分享
        if (dto.MaxAttachmentSize.HasValue) s.MaxAttachmentSize = dto.MaxAttachmentSize.Value;
        if (dto.MaxAttachmentCount.HasValue) s.MaxAttachmentCount = dto.MaxAttachmentCount.Value;
        if (dto.AllowedExtensions != null) s.AllowedExtensions = dto.AllowedExtensions;
        if (dto.DefaultImageSize != null) s.DefaultImageSize = dto.DefaultImageSize;
        if (dto.ShareExpireDays.HasValue) s.ShareExpireDays = dto.ShareExpireDays.Value;
        // API 网关
        if (dto.EnableGateway.HasValue) s.EnableGateway = dto.EnableGateway.Value;
        if (dto.EnableGatewayPipeline.HasValue) s.EnableGatewayPipeline = dto.EnableGatewayPipeline.Value;
        if (dto.GatewayRateLimit.HasValue) s.GatewayRateLimit = dto.GatewayRateLimit.Value;
        if (dto.UpstreamRetryCount.HasValue) s.UpstreamRetryCount = dto.UpstreamRetryCount.Value;
        if (dto.EnableGatewayRecording.HasValue) s.EnableGatewayRecording = dto.EnableGatewayRecording.Value;
        // 工具与能力
        if (dto.EnableFunctionCalling.HasValue) s.EnableFunctionCalling = dto.EnableFunctionCalling.Value;
        if (dto.EnableMcp.HasValue) s.EnableMcp = dto.EnableMcp.Value;
        if (dto.EnableSuggestedQuestionCache.HasValue) s.EnableSuggestedQuestionCache = dto.EnableSuggestedQuestionCache.Value;
        if (dto.StreamingSpeed.HasValue) s.StreamingSpeed = dto.StreamingSpeed.Value;
        if (dto.ToolAdvertiseThreshold.HasValue) s.ToolAdvertiseThreshold = dto.ToolAdvertiseThreshold.Value;
        // 功能开关
        if (dto.EnableUsageStats.HasValue) s.EnableUsageStats = dto.EnableUsageStats.Value;
        if (dto.BackgroundGeneration.HasValue) s.BackgroundGeneration = dto.BackgroundGeneration.Value;
        if (dto.MaxMessagesPerMinute.HasValue) s.MaxMessagesPerMinute = dto.MaxMessagesPerMinute.Value;
        // 自学习
        if (dto.EnableAutoLearning.HasValue) s.EnableAutoLearning = dto.EnableAutoLearning.Value;
        if (dto.LearningModel != null) s.LearningModel = dto.LearningModel;
        if (dto.MinLearningContentLength.HasValue) s.MinLearningContentLength = dto.MinLearningContentLength.Value;

        s.Save();

        return NoContent();
    }
}
