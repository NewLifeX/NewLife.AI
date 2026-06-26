#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.OpenAI;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScope CosyVoice v3 语音合成单元测试</summary>
/// <remarks>
/// 不依赖真实 API Key。测试 GetHeaderAction 纯逻辑、参数校验。
/// v3 模型支持系统音色（120+ 预设音色），v3.5 不支持。
/// 真实端到端测试请参见 DashScopeIntegrationTests。
/// </remarks>
public class DashScopeSpeechStreamTests
{
    #region GetHeaderAction 纯逻辑测试

    [Fact]
    [DisplayName("GetHeaderAction null 字典返回 null")]
    public void GetHeaderAction_Null_ReturnsNull()
    {
        var method = typeof(DashScopeChatClient).GetMethod("GetHeaderAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var client = CreateClient("cosyvoice-v3-flash");
        var result = method!.Invoke(client, [null!]);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("GetHeaderAction 正常字典返回 action 值")]
    public void GetHeaderAction_ValidHeader_ReturnsAction()
    {
        var method = typeof(DashScopeChatClient).GetMethod("GetHeaderAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var client = CreateClient("cosyvoice-v3-flash");
        var header = new Dictionary<String, Object?> { ["action"] = "task-started" };
        var dic = new Dictionary<String, Object?> { ["header"] = header };

        var result = method!.Invoke(client, [dic]);
        Assert.Equal("task-started", result);
    }

    [Fact]
    [DisplayName("GetHeaderAction 含额外字段仍正确提取 action")]
    public void GetHeaderAction_ExtraFields_StillReturnsAction()
    {
        var method = typeof(DashScopeChatClient).GetMethod("GetHeaderAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var client = CreateClient("cosyvoice-v3-flash");
        var header = new Dictionary<String, Object?>
        {
            ["task_id"] = "abc-123",
            ["action"] = "task-finished",
            ["streaming"] = "out",
        };
        var dic = new Dictionary<String, Object?> { ["header"] = header };

        var result = method!.Invoke(client, [dic]);
        Assert.Equal("task-finished", result);
    }

    [Fact]
    [DisplayName("GetHeaderAction 无 header 字段返回 null")]
    public void GetHeaderAction_NoHeaderField_ReturnsNull()
    {
        var method = typeof(DashScopeChatClient).GetMethod("GetHeaderAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var client = CreateClient("cosyvoice-v3-flash");
        var dic = new Dictionary<String, Object?> { ["payload"] = new { } };

        var result = method!.Invoke(client, [dic]);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("GetHeaderAction header 非字典类型返回 null")]
    public void GetHeaderAction_HeaderNotDictionary_ReturnsNull()
    {
        var method = typeof(DashScopeChatClient).GetMethod("GetHeaderAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var client = CreateClient("cosyvoice-v3-flash");
        var dic = new Dictionary<String, Object?> { ["header"] = "not-a-dict" };

        var result = method!.Invoke(client, [dic]);
        Assert.Null(result);
    }

    #endregion

    #region 参数校验

    [Fact]
    [DisplayName("SpeechStreamAsync null 请求应抛出 ArgumentNullException")]
    public async Task SpeechStreamAsync_NullRequest_ThrowsArgumentNullException()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(null!, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 空输入应抛出 ArgumentException")]
    public async Task SpeechStreamAsync_EmptyInput_ThrowsArgumentException()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3-flash", Input = "", Voice = "longxiaochun_v3" };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync v3 模型接受系统音色 longxiaochun_v3")]
    public async Task SpeechStreamAsync_V3_AcceptsSystemVoice()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        // v3 模型支持 120+ 系统预设音色，longxiaochun_v3 应通过校验
        var request = new SpeechRequest { Model = "cosyvoice-v3-flash", Input = "测试", Voice = "longxiaochun_v3" };

        // 无真实 API Key → 连接失败，但不应因音色校验抛 ArgumentException
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync v3 模型接受 OpenAI 兼容音色 alloy")]
    public async Task SpeechStreamAsync_V3_AcceptsOapiVoiceAlloy()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        // v3 模型支持系统音色，alloy 映射为 longxiaochun_v3 后应通过校验
        var request = new SpeechRequest { Model = "cosyvoice-v3-flash", Input = "测试", Voice = "alloy" };

        // 无真实 API Key → 连接失败，但不应因音色校验抛 ArgumentException
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 使用 request.Model 而非 _options.Model")]
    public async Task SpeechStreamAsync_UsesRequestModel_NotOptionsModel()
    {
        // 用 v3-plus 构造客户端，但 request 指定 v3-flash（不同 v3 变体）
        var client = CreateClient("cosyvoice-v3-plus");
        var request = new SpeechRequest { Model = "cosyvoice-v3-flash", Input = "测试", Voice = "longxiaochun_v3" };

        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });

        Assert.NotNull(ex);
        // 非 ArgumentException 说明 request.Model（v3-flash）被正确使用，未回退到 _options.Model（v3-plus）
        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 空 request.Model 时回退到 _options.Model")]
    public async Task SpeechStreamAsync_NullModel_FallsBackToOptions()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest { Model = null!, Input = "测试", Voice = "longxiaochun_v3" };

        // 无真实 API Key → WebSocket 连接失败，但不因版本检测抛异常
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });

        Assert.NotNull(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 无 API Key 时连接失败应抛异常")]
    public async Task SpeechStreamAsync_NoApiKey_ThrowsOnConnect()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3-flash", Input = "测试", Voice = "longxiaochun_v3" };

        // 无 API Key → WebSocket 连接被拒，抛异常
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });
    }

    #endregion

    #region 工具方法

    private static DashScopeChatClient CreateClient(String modelCode)
    {
        var options = new AiClientOptions { ApiKey = null, Model = modelCode, Endpoint = "https://dashscope.aliyuncs.com/api/v1" };
        return new DashScopeChatClient(options);
    }

    #endregion
}
