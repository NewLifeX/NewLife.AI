#nullable enable
using System;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.ChatAI.Controllers;
using Xunit;

namespace XUnitTest.Services;

/// <summary>PresetsController DTO 和请求模型测试</summary>
public class PresetsControllerTests
{
    #region PresetDto

    [Fact]
    [DisplayName("PresetDto_Record构造正确")]
    public void PresetDto_Constructor_SetsAllProperties()
    {
        var dto = new PresetDto(1, "代码审查", 10, "GPT-4o", "review", "你是代码审查员", null, 1, true, 100);

        Assert.Equal(1, dto.Id);
        Assert.Equal("代码审查", dto.Name);
        Assert.Equal(10, dto.ModelId);
        Assert.Equal("GPT-4o", dto.ModelName);
        Assert.Equal("review", dto.SkillCode);
        Assert.Equal("你是代码审查员", dto.SystemPrompt);
        Assert.Null(dto.Prompt);
        Assert.Equal(1, dto.ThinkingMode);
        Assert.True(dto.IsDefault);
        Assert.Equal(100, dto.Sort);
    }

    [Fact]
    [DisplayName("PresetDto_可空字段允许null")]
    public void PresetDto_NullableFields_AcceptNull()
    {
        var dto = new PresetDto(2, "简单预设", 0, null, null, null, null, 0, false, 0);

        Assert.Null(dto.ModelName);
        Assert.Null(dto.SkillCode);
        Assert.Null(dto.SystemPrompt);
        Assert.Null(dto.Prompt);
    }

    [Fact]
    [DisplayName("PresetDto_Record相等性")]
    public void PresetDto_RecordEquality()
    {
        var dto1 = new PresetDto(1, "测试", 0, null, null, null, null, 0, false, 0);
        var dto2 = new PresetDto(1, "测试", 0, null, null, null, null, 0, false, 0);

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    [DisplayName("PresetDto_ThinkingMode映射到枚举值")]
    public void PresetDto_ThinkingMode_MapsCorrectly()
    {
        // Auto=0, Think=1, Fast=2
        var auto = new PresetDto(1, "自动", 0, null, null, null, null, (Int32)ThinkingMode.Auto, false, 0);
        var think = new PresetDto(2, "思考", 0, null, null, null, null, (Int32)ThinkingMode.Think, false, 0);
        var fast = new PresetDto(3, "快速", 0, null, null, null, null, (Int32)ThinkingMode.Fast, false, 0);

        Assert.Equal(0, auto.ThinkingMode);
        Assert.Equal(1, think.ThinkingMode);
        Assert.Equal(2, fast.ThinkingMode);
    }

    #endregion

    #region PresetRequest

    [Fact]
    [DisplayName("PresetRequest_默认值正确")]
    public void PresetRequest_Defaults()
    {
        var request = new PresetRequest();

        Assert.Equal("", request.Name);
        Assert.Equal(0, request.ModelId);
        Assert.Null(request.SkillCode);
        Assert.Null(request.SystemPrompt);
        Assert.Equal(0, request.ThinkingMode);
        Assert.False(request.IsDefault);
        Assert.Equal(0, request.Sort);
    }

    [Fact]
    [DisplayName("PresetRequest_设置全部字段")]
    public void PresetRequest_SetAllFields()
    {
        var request = new PresetRequest
        {
            Name = "翻译助手",
            ModelId = 5,
            SkillCode = "translate",
            SystemPrompt = "你是一个翻译",
            ThinkingMode = 2,
            IsDefault = true,
            Sort = 50,
        };

        Assert.Equal("翻译助手", request.Name);
        Assert.Equal(5, request.ModelId);
        Assert.Equal("translate", request.SkillCode);
        Assert.Equal("你是一个翻译", request.SystemPrompt);
        Assert.Equal(2, request.ThinkingMode);
        Assert.True(request.IsDefault);
        Assert.Equal(50, request.Sort);
    }

    #endregion

    #region ThinkingMode 枚举转换

    [Fact]
    [DisplayName("ThinkingMode枚举_Int32转换往返一致")]
    public void ThinkingMode_Int32Roundtrip()
    {
        foreach (ThinkingMode mode in Enum.GetValues(typeof(ThinkingMode)))
        {
            var intVal = (Int32)mode;
            var backToEnum = (ThinkingMode)intVal;
            Assert.Equal(mode, backToEnum);
        }
    }

    #endregion
}
