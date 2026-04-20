#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.OpenAI;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScope 图像编辑路径测试。验证不会把原生端点错误拼接成 /api/v1/v1/images/edits</summary>
[DisplayName("DashScope 图像编辑路径测试")]
public class DashScopeImageEditTests
{
    [Fact]
    [DisplayName("千问文生图模型_图像编辑直接返回不支持")]
    public async Task EditImageAsync_QwenImageMax_ThrowsNotSupportedException()
    {
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" });

        using var imageStream = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-max",
            Prompt = "旁边增加一条狗",
            ImageStream = imageStream,
            ImageFileName = "image.png",
        }));

        Assert.Contains("仅支持文生图", ex.Message);
    }

    [Fact]
    [DisplayName("默认原生协议_自定义编辑模型自动切换到兼容模式端点")]
    public async Task EditImageAsync_DefaultNativeProtocol_UsesCompatibleEndpoint()
    {
        var handler = new CaptureHandler();
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        using var imageStream = new MemoryStream([1, 2, 3]);
        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "custom-image-edit-model",
            Prompt = "旁边增加一条狗",
            ImageStream = imageStream,
            ImageFileName = "image.png",
        });

        Assert.NotNull(response);
        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/images/edits", handler.LastRequestUri?.ToString());
    }

    [Fact]
    [DisplayName("显式兼容模式v1端点_图像编辑不会重复拼接v1")]
    public async Task EditImageAsync_CompatibleV1Endpoint_DoesNotDuplicateV1()
    {
        var handler = new CaptureHandler();
        using var client = new DashScopeChatClient(new AiClientOptions
        {
            ApiKey = "sk-test",
            Endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1",
        })
        {
            HttpClient = new HttpClient(handler)
        };

        using var imageStream = new MemoryStream([1, 2, 3]);
        using var maskStream = new MemoryStream([4, 5, 6]);
        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "custom-image-edit-model",
            Prompt = "旁边增加一条狗",
            ImageStream = imageStream,
            ImageFileName = "image.png",
            MaskStream = maskStream,
            MaskFileName = "mask.png",
        });

        Assert.NotNull(response);
        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/images/edits", handler.LastRequestUri?.ToString());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var body = """
            {
              "created": 1710000000,
              "data": [
                {
                  "url": "https://example.com/edited-image.png",
                  "revised_prompt": "旁边增加一条狗"
                }
              ]
            }
            """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}