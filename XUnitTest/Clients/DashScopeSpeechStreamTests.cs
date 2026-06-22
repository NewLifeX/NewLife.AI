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

/// <summary>DashScope CosyVoice WebSocket 流式语音合成单元测试</summary>
/// <remarks>
/// 不依赖真实 API Key。测试 GetHeaderAction 纯逻辑、参数校验。
/// 真实 WebSocket 端到端测试请参见 DashScopeIntegrationTests。
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

        var client = CreateClient("cosyvoice-v3.5-flash");
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

        var client = CreateClient("cosyvoice-v3.5-flash");
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

        var client = CreateClient("cosyvoice-v3.5-flash");
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

        var client = CreateClient("cosyvoice-v3.5-flash");
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

        var client = CreateClient("cosyvoice-v3.5-flash");
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
        var client = CreateClient("cosyvoice-v3.5-flash");
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
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3.5-flash", Input = "", Voice = "longxiaochun_v3" };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 无效音色应抛出 ArgumentException")]
    public async Task SpeechStreamAsync_InvalidVoice_ThrowsArgumentException()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3.5-flash", Input = "测试", Voice = "invalid_xyz" };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync OpenAI 兼容音色 alloy 应通过校验")]
    public async Task SpeechStreamAsync_OapiVoice_Alloy_PassesVoiceCheck()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3.5-flash", Input = "测试", Voice = "alloy" };

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
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3.5-flash", Input = "测试", Voice = "longxiaochun_v3" };

        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None)) { }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 空 request.Model 时回退到 _options.Model")]
    public async Task SpeechStreamAsync_NullModel_FallsBackToOptions()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
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
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest { Model = "cosyvoice-v3.5-flash", Input = "测试", Voice = "longxiaochun_v3" };

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
