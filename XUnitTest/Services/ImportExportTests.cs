#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.ChatAI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Services;

/// <summary>导入/导出功能测试</summary>
public class ImportExportTests
{
    #region 导入 JSON 解析测试

    [Fact]
    [DisplayName("导入JSON_空内容返回空列表")]
    public void ImportJson_Empty_ReturnsNull()
    {
        var items = "".ToJsonEntity<List<ImportConversationDummy>>();
        Assert.Null(items);
    }

    [Fact]
    [DisplayName("导入JSON_空数组返回空列表")]
    public void ImportJson_EmptyArray_ReturnsEmptyList()
    {
        var items = "[]".ToJsonEntity<List<ImportConversationDummy>>();
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    [DisplayName("导入JSON_单会话解析正确")]
    public void ImportJson_SingleConversation_Parsed()
    {
        var json = """
        [
          {
            "title": "测试对话",
            "modelId": 5,
            "isPinned": true,
            "createTime": "2025-01-15T10:30:00",
            "lastMessageTime": "2025-01-15T11:00:00",
            "messages": [
              {
                "role": "user",
                "content": "你好",
                "thinkingMode": 0,
                "createTime": "2025-01-15T10:30:00"
              },
              {
                "role": "assistant",
                "content": "你好！有什么可以帮助你的吗？",
                "thinkingContent": "用户在打招呼",
                "thinkingMode": 1,
                "createTime": "2025-01-15T10:30:05"
              }
            ]
          }
        ]
        """;

        var items = json.ToJsonEntity<List<ImportConversationDummy>>();
        Assert.NotNull(items);
        Assert.Single(items);

        var conv = items[0];
        Assert.Equal("测试对话", conv.Title);
        Assert.Equal(5, conv.ModelId);
        Assert.True(conv.IsPinned);
        Assert.NotNull(conv.Messages);
        Assert.Equal(2, conv.Messages!.Count);

        Assert.Equal("user", conv.Messages[0].Role);
        Assert.Equal("你好", conv.Messages[0].Content);
        Assert.Equal(0, conv.Messages[0].ThinkingMode);

        Assert.Equal("assistant", conv.Messages[1].Role);
        Assert.Equal("你好！有什么可以帮助你的吗？", conv.Messages[1].Content);
        Assert.Equal("用户在打招呼", conv.Messages[1].ThinkingContent);
        Assert.Equal(1, conv.Messages[1].ThinkingMode);
    }

    [Fact]
    [DisplayName("导入JSON_多会话解析正确")]
    public void ImportJson_MultipleConversations_Parsed()
    {
        var json = """
        [
          {"title": "会话1", "modelId": 1, "isPinned": false, "createTime": "2025-01-01T00:00:00", "lastMessageTime": "2025-01-01T00:00:00"},
          {"title": "会话2", "modelId": 2, "isPinned": true, "createTime": "2025-01-02T00:00:00", "lastMessageTime": "2025-01-02T00:00:00"},
          {"title": "会话3", "modelId": 3, "isPinned": false, "createTime": "2025-01-03T00:00:00", "lastMessageTime": "2025-01-03T00:00:00"}
        ]
        """;

        var items = json.ToJsonEntity<List<ImportConversationDummy>>();
        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);
        Assert.Equal("会话1", items[0].Title);
        Assert.Equal("会话3", items[2].Title);
    }

    [Fact]
    [DisplayName("导入JSON_无消息字段会话仍可解析")]
    public void ImportJson_NoMessages_Parsed()
    {
        var json = """
        [{"title": "空会话", "modelId": 0, "isPinned": false, "createTime": "2025-01-01T00:00:00", "lastMessageTime": "2025-01-01T00:00:00"}]
        """;

        var items = json.ToJsonEntity<List<ImportConversationDummy>>();
        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Null(items![0].Messages);
    }

    [Fact]
    [DisplayName("导入JSON_ThinkingMode映射正确")]
    public void ImportJson_ThinkingMode_MapsCorrectly()
    {
        var json = """
        [
          {
            "title": "思考测试",
            "modelId": 0,
            "isPinned": false,
            "createTime": "2025-01-01T00:00:00",
            "lastMessageTime": "2025-01-01T00:00:00",
            "messages": [
              {"role": "user", "content": "test", "thinkingMode": 0, "createTime": "2025-01-01T00:00:00"},
              {"role": "assistant", "content": "think", "thinkingMode": 1, "createTime": "2025-01-01T00:00:01"},
              {"role": "assistant", "content": "fast", "thinkingMode": 2, "createTime": "2025-01-01T00:00:02"}
            ]
          }
        ]
        """;

        var items = json.ToJsonEntity<List<ImportConversationDummy>>();
        var msgs = items![0].Messages!;

        Assert.Equal((Int32)ThinkingMode.Auto, msgs[0].ThinkingMode);
        Assert.Equal((Int32)ThinkingMode.Think, msgs[1].ThinkingMode);
        Assert.Equal((Int32)ThinkingMode.Fast, msgs[2].ThinkingMode);
    }

    #endregion

    #region 导出格式验证

    [Fact]
    [DisplayName("UserSettingsDto_Record构造正确")]
    public void UserSettingsDto_Constructor()
    {
        var dto = new UserSettingsDto("zh-CN", "system", 16, "Enter", 0, ThinkingMode.Auto, 10, "", "", ResponseStyle.Balanced, "", false);

        Assert.Equal("zh-CN", dto.Language);
        Assert.Equal("system", dto.Theme);
        Assert.Equal(16, dto.FontSize);
        Assert.Equal(ThinkingMode.Auto, dto.DefaultThinkingMode);
    }

    #endregion

    #region 辅助类型（镜像 ImportUserDataAsync 的内部类结构，用于测试 JSON 反序列化）

    /// <summary>导入会话数据</summary>
    public class ImportConversationDummy
    {
        public String? Title { get; set; }
        public Int32 ModelId { get; set; }
        public Boolean IsPinned { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime LastMessageTime { get; set; }
        public List<ImportMessageDummy>? Messages { get; set; }
    }

    /// <summary>导入消息数据</summary>
    public class ImportMessageDummy
    {
        public String? Role { get; set; }
        public String? Content { get; set; }
        public String? ThinkingContent { get; set; }
        public Int32 ThinkingMode { get; set; }
        public String? Attachments { get; set; }
        public DateTime CreateTime { get; set; }
    }

    #endregion
}
