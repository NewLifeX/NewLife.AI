using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Data;
using Xunit;

namespace XUnitTest.Models;

/// <summary>ChatMessage 模型单元测试。验证 IExtend 实现与索引器空保护</summary>
[DisplayName("ChatMessage 单元测试")]
public class ChatMessageTests
{
    [Fact]
    [DisplayName("实现 IExtend 接口")]
    public void ImplementsIExtend()
    {
        var msg = new ChatMessage { Role = "user", Content = "hi" };

        Assert.IsAssignableFrom<IExtend>(msg);
    }

    [Fact]
    [DisplayName("索引器_读写扩展数据")]
    public void Indexer_SetAndGet()
    {
        var msg = new ChatMessage { Role = "user", Content = "hi" };
        msg["Signature"] = "sig_1";

        Assert.Equal("sig_1", msg["Signature"]);
        Assert.True(msg.Items.ContainsKey("Signature"));
    }

    [Fact]
    [DisplayName("索引器_Items为null时读取返回null不抛异常")]
    public void Indexer_NullItems_GetReturnsNull()
    {
        var msg = new ChatMessage { Role = "user" };
        msg.Items = null!;

        Assert.Null(msg["Signature"]);
    }

    [Fact]
    [DisplayName("索引器_Items为null时写入自动创建")]
    public void Indexer_NullItems_SetCreates()
    {
        var msg = new ChatMessage { Role = "user" };
        msg.Items = null!;

        msg["Signature"] = "sig_2";

        Assert.Equal("sig_2", msg["Signature"]);
        Assert.NotNull(msg.Items);
    }
}
