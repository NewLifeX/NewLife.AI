using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.ChatData.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>系统设置控制器。仅 IsSystem 系统角色管理员可访问</summary>
[Route("api/system/settings")]
public class SystemSettingsController(ChatSetting chatSetting) : ChatApiControllerBase
{
    /// <summary>获取系统设置</summary>
    /// <returns>当前 ChatSetting 配置，含可用模型列表</returns>
    [HttpGet]
    public ActionResult<SystemSettingsDto> Get()
    {
        if (!IsCurrentUserSystem())
            return StatusCode(403, new { code = "FORBIDDEN", message = "仅系统管理员可访问系统设置" });


        // 构建可用模型下拉列表
        var models = new List<ModelOptionDto> { new(0, "默认（第一个可用模型）") };
        foreach (var m in ModelConfig.FindAllEnabled())
            models.Add(new ModelOptionDto(m.Id, m.Name));

        return Ok(new SystemSettingsDto
        {
            // 基本配置
            Name = chatSetting.Name,
            SiteTitle = chatSetting.SiteTitle,
            LogoUrl = chatSetting.LogoUrl,
            AutoGenerateTitle = chatSetting.AutoGenerateTitle,
            // 对话默认
            DefaultModel = chatSetting.DefaultModel,
            DefaultThinkingMode = (Int32)chatSetting.DefaultThinkingMode,
            DefaultContextRounds = chatSetting.DefaultContextRounds,
            // 上传与分享
            MaxAttachmentSize = chatSetting.MaxAttachmentSize,
            MaxAttachmentCount = chatSetting.MaxAttachmentCount,
            AllowedExtensions = chatSetting.AllowedExtensions,
            DefaultImageSize = chatSetting.DefaultImageSize,
            ShareExpireDays = chatSetting.ShareExpireDays,
            // API 网关
            EnableGateway = chatSetting.EnableGateway,
            EnableGatewayPipeline = chatSetting.EnableGatewayPipeline,
            GatewayRateLimit = chatSetting.GatewayRateLimit,
            UpstreamRetryCount = chatSetting.UpstreamRetryCount,
            EnableGatewayRecording = chatSetting.EnableGatewayRecording,
            // 工具与能力
            EnableFunctionCalling = chatSetting.EnableFunctionCalling,
            EnableMcp = chatSetting.EnableMcp,
            EnableSuggestedQuestionCache = chatSetting.EnableSuggestedQuestionCache,
            StreamingSpeed = chatSetting.StreamingSpeed,
            ToolAdvertiseThreshold = chatSetting.ToolAdvertiseThreshold,
            // 功能开关
            EnableUsageStats = chatSetting.EnableUsageStats,
            BackgroundGeneration = chatSetting.BackgroundGeneration,
            MaxMessagesPerMinute = chatSetting.MaxMessagesPerMinute,
            // 自学习
            EnableAutoLearning = chatSetting.EnableAutoLearning,
            LearningModel = chatSetting.LearningModel,
            MinLearningContentLength = chatSetting.MinLearningContentLength,
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


        // 基本配置
        if (dto.Name != null) chatSetting.Name = dto.Name;
        if (dto.SiteTitle != null) chatSetting.SiteTitle = dto.SiteTitle;
        if (dto.LogoUrl != null) chatSetting.LogoUrl = dto.LogoUrl;
        if (dto.AutoGenerateTitle.HasValue) chatSetting.AutoGenerateTitle = dto.AutoGenerateTitle.Value;
        // 对话默认
        if (dto.DefaultModel.HasValue) chatSetting.DefaultModel = dto.DefaultModel.Value;
        if (dto.DefaultThinkingMode.HasValue) chatSetting.DefaultThinkingMode = (ThinkingMode)dto.DefaultThinkingMode.Value;
        if (dto.DefaultContextRounds.HasValue) chatSetting.DefaultContextRounds = dto.DefaultContextRounds.Value;
        // 上传与分享
        if (dto.MaxAttachmentSize.HasValue) chatSetting.MaxAttachmentSize = dto.MaxAttachmentSize.Value;
        if (dto.MaxAttachmentCount.HasValue) chatSetting.MaxAttachmentCount = dto.MaxAttachmentCount.Value;
        if (dto.AllowedExtensions != null) chatSetting.AllowedExtensions = dto.AllowedExtensions;
        if (dto.DefaultImageSize != null) chatSetting.DefaultImageSize = dto.DefaultImageSize;
        if (dto.ShareExpireDays.HasValue) chatSetting.ShareExpireDays = dto.ShareExpireDays.Value;
        // API 网关
        if (dto.EnableGateway.HasValue) chatSetting.EnableGateway = dto.EnableGateway.Value;
        if (dto.EnableGatewayPipeline.HasValue) chatSetting.EnableGatewayPipeline = dto.EnableGatewayPipeline.Value;
        if (dto.GatewayRateLimit.HasValue) chatSetting.GatewayRateLimit = dto.GatewayRateLimit.Value;
        if (dto.UpstreamRetryCount.HasValue) chatSetting.UpstreamRetryCount = dto.UpstreamRetryCount.Value;
        if (dto.EnableGatewayRecording.HasValue) chatSetting.EnableGatewayRecording = dto.EnableGatewayRecording.Value;
        // 工具与能力
        if (dto.EnableFunctionCalling.HasValue) chatSetting.EnableFunctionCalling = dto.EnableFunctionCalling.Value;
        if (dto.EnableMcp.HasValue) chatSetting.EnableMcp = dto.EnableMcp.Value;
        if (dto.EnableSuggestedQuestionCache.HasValue) chatSetting.EnableSuggestedQuestionCache = dto.EnableSuggestedQuestionCache.Value;
        if (dto.StreamingSpeed.HasValue) chatSetting.StreamingSpeed = dto.StreamingSpeed.Value;
        if (dto.ToolAdvertiseThreshold.HasValue) chatSetting.ToolAdvertiseThreshold = dto.ToolAdvertiseThreshold.Value;
        // 功能开关
        if (dto.EnableUsageStats.HasValue) chatSetting.EnableUsageStats = dto.EnableUsageStats.Value;
        if (dto.BackgroundGeneration.HasValue) chatSetting.BackgroundGeneration = dto.BackgroundGeneration.Value;
        if (dto.MaxMessagesPerMinute.HasValue) chatSetting.MaxMessagesPerMinute = dto.MaxMessagesPerMinute.Value;
        // 自学习
        if (dto.EnableAutoLearning.HasValue) chatSetting.EnableAutoLearning = dto.EnableAutoLearning.Value;
        if (dto.LearningModel != null) chatSetting.LearningModel = dto.LearningModel;
        if (dto.MinLearningContentLength.HasValue) chatSetting.MinLearningContentLength = dto.MinLearningContentLength.Value;

        chatSetting.Save();

        return NoContent();
    }
}
