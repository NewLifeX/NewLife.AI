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
/// 不依赖真实 API Key。测试模型版本检测、参数校验、及纯逻辑辅助方法。
/// 真实 WebSocket 端到端测试请参见 DashScopeIntegrationTests。
/// </remarks>
public class DashScopeSpeechStreamTests
{
    #region 模型版本检测

    [Fact]
    [DisplayName("SpeechStreamAsync_cosyvoice_v3_flash 应抛出 NotSupportedException")]
    public async Task SpeechStreamAsync_V3Flash_ThrowsNotSupportedException()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3-flash",
            Input = "测试文本",
            Voice = "longxiaochun_v3",
        };

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });
    }

    [Theory]
    [DisplayName("SpeechStreamAsync 旧版模型应抛出 NotSupportedException")]
    [InlineData("cosyvoice-v3-flash", "longxiaochun_v3")]
    public async Task SpeechStreamAsync_LegacyModels_ThrowsNotSupportedException(String modelCode, String voice)
    {
        var client = CreateClient(modelCode);
        var request = new SpeechRequest
        {
            Model = modelCode,
            Input = "测试文本",
            Voice = voice,
        };

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });
    }

    [Theory]
    [DisplayName("SpeechStreamAsync 新版模型不因版本检测抛出 NotSupportedException")]
    [InlineData("cosyvoice-v3.5-flash")]
    [InlineData("cosyvoice-v4.0")]
    public async Task SpeechStreamAsync_NewerModels_NotThrowByVersionCheck(String modelCode)
    {
        var client = CreateClient(modelCode);
        var request = new SpeechRequest
        {
            Model = modelCode,
            Input = "测试文本",
            Voice = "longxiaochun_v3",
        };

        // 无真实 WebSocket 连接 → 会因连接失败抛异常，但不应是 NotSupportedException
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<NotSupportedException>(ex);
    }

    #endregion

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
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "",
            Voice = "longxiaochun_v3",
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 无效音色应抛出 ArgumentException")]
    public async Task SpeechStreamAsync_InvalidVoice_ThrowsArgumentException()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "测试文本",
            Voice = "invalid_voice_xyz_not_exists",
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync OpenAI 兼容音色 alloy 应通过校验")]
    public async Task SpeechStreamAsync_OapiVoice_Alloy_PassesVoiceCheck()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "测试文本",
            Voice = "alloy",
        };

        // 不应因音色校验失败而抛异常（会因网络连接失败抛其他异常）
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 使用 request.Model 而非 _options.Model")]
    public async Task SpeechStreamAsync_UsesRequestModel_NotOptionsModel()
    {
        var client = CreateClient("cosyvoice-v3-flash"); // options 中设置旧版
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash", // request 中覆盖为新版
            Input = "测试文本",
            Voice = "longxiaochun_v3",
        };

        // request.Model 是 v3.5 → 版本检测通过
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });

        Assert.NotNull(ex);
        Assert.IsNotType<NotSupportedException>(ex);
    }

    [Fact]
    [DisplayName("SpeechStreamAsync 空 request.Model 时回退到 _options.Model")]
    public async Task SpeechStreamAsync_NullModel_FallsBackToOptions()
    {
        var client = CreateClient("cosyvoice-v3-flash");
        var request = new SpeechRequest
        {
            Model = null!,
            Input = "测试文本",
            Voice = "longxiaochun_v3",
        };

        // 回退到 options.Model (v3-flash) → 版本检测不通过
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    [DisplayName("SpeechStreamAsync CancellationToken 取消应能中断")]
    public async Task SpeechStreamAsync_Cancelled_Token_StopsEarly()
    {
        var client = CreateClient("cosyvoice-v3.5-flash");
        var request = new SpeechRequest
        {
            Model = "cosyvoice-v3.5-flash",
            Input = "测试文本",
            Voice = "longxiaochun_v3",
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // 立即取消

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.SpeechStreamAsync(request, cts.Token))
            {
            }
        });
    }

    #endregion

    #region 工具方法

    /// <summary>创建用于测试的 DashScopeChatClient 实例（无 API Key）</summary>
    private static DashScopeChatClient CreateClient(String modelCode)
    {
        var options = new AiClientOptions
        {
            ApiKey = null,
            Model = modelCode,
            Endpoint = "https://dashscope.aliyuncs.com/api/v1",
        };
        return new DashScopeChatClient(options);
    }

    #endregion
}
