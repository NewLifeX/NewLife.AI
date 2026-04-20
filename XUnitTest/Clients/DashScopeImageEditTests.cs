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

/// <summary>DashScope 图像编辑路径测试。验证原生多模态路径与兼容模式路径的正确路由</summary>
[DisplayName("DashScope 图像编辑路径测试")]
public class DashScopeImageEditTests
{
    [Fact]
    [DisplayName("千问文生图旧款模型_图像编辑走兼容模式端点")]
    public async Task EditImageAsync_QwenImageMax_UsesCompatibleEndpoint()
    {
        var handler = new CaptureHandler(useNativeFormat: false);
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        using var imageStream = new MemoryStream([1, 2, 3]);
        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-max",
            Prompt = "旁边增加一条狗",
            ImageStream = imageStream,
            ImageFileName = "image.png",
        });

        Assert.NotNull(response);
        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/images/edits", handler.LastRequestUri?.ToString());
    }

    [Fact]
    [DisplayName("qwen-image-2.0-pro_URL传图_走原生多模态端点")]
    public async Task EditImageAsync_QwenImage20Pro_WithUrl_UsesNativeEndpoint()
    {
        var handler = new CaptureHandler(useNativeFormat: true);
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-2.0-pro",
            Prompt = "在画面右下角题写诗句",
            ImageUrl = "https://example.com/input.webp",
            Size = "2048*2048",
        });

        Assert.NotNull(response);
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation", handler.LastRequestUri?.ToString());
    }

    [Fact]
    [DisplayName("qwen-image-2.0_Size含x分隔符_自动转星号")]
    public async Task EditImageAsync_QwenImage20_SizeWithX_NormalizesToAsterisk()
    {
        var capturedBody = "";
        var handler = new CaptureHandler(useNativeFormat: true, bodyCapture: b => capturedBody = b);
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-2.0",
            Prompt = "修改背景颜色",
            ImageUrl = "https://example.com/input.png",
            Size = "1024x1024",
        });

        Assert.NotNull(response);
        Assert.Contains("1024*1024", capturedBody);
        Assert.DoesNotContain("1024x1024", capturedBody);
    }

    [Fact]
    [DisplayName("qwen-image-edit_Stream传图_走原生端点并包含base64")]
    public async Task EditImageAsync_QwenImageEdit_WithStream_UsesNativeEndpointWithBase64()
    {
        var capturedBody = "";
        var handler = new CaptureHandler(useNativeFormat: true, bodyCapture: b => capturedBody = b);
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        using var imageStream = new MemoryStream("fake-image"u8.ToArray());
        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-edit",
            Prompt = "增加文字水印",
            ImageStream = imageStream,
        });

        Assert.NotNull(response);
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation", handler.LastRequestUri?.ToString());
        Assert.Contains("data:image/png;base64,", capturedBody);
    }

    [Fact]
    [DisplayName("qwen-image-edit-max_携带N和NegativePrompt_参数正确传入")]
    public async Task EditImageAsync_QwenImageEditMax_WithNAndNegativePrompt()
    {
        var capturedBody = "";
        var handler = new CaptureHandler(useNativeFormat: true, bodyCapture: b => capturedBody = b);
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" })
        {
            HttpClient = new HttpClient(handler)
        };

        var response = await client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-edit-max",
            Prompt = "改变人物发色",
            ImageUrl = "https://example.com/portrait.png",
            N = 3,
            NegativePrompt = "低分辨率、噪点",
        });

        Assert.NotNull(response);
        Assert.Contains("\"n\":3", capturedBody.Replace(" ", ""));
        Assert.Contains("低分辨率、噪点", capturedBody);
    }

    [Fact]
    [DisplayName("默认原生协议_自定义编辑模型自动切换到兼容模式端点")]
    public async Task EditImageAsync_DefaultNativeProtocol_UsesCompatibleEndpoint()
    {
        var handler = new CaptureHandler(useNativeFormat: false);
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
        var handler = new CaptureHandler(useNativeFormat: false);
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

    [Fact]
    [DisplayName("ImageUrl与ImageStream均为空_抛出ArgumentException")]
    public async Task EditImageAsync_NoImageSource_ThrowsArgumentException()
    {
        using var client = new DashScopeChatClient(new AiClientOptions { ApiKey = "sk-test" });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.EditImageAsync(new ImageEditsRequest
        {
            Model = "qwen-image-2.0-pro",
            Prompt = "修改内容",
        }));

        Assert.Contains("ImageUrl", ex.Message);
        Assert.Contains("ImageStream", ex.Message);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Boolean _useNativeFormat;
        private readonly Action<String>? _bodyCapture;

        public CaptureHandler(Boolean useNativeFormat, Action<String>? bodyCapture = null)
        {
            _useNativeFormat = useNativeFormat;
            _bodyCapture = bodyCapture;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (_bodyCapture != null && request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                _bodyCapture(body);
            }

            String responseBody;
            if (_useNativeFormat)
            {
                // DashScope 原生多模态响应格式
                responseBody = """
                {
                  "output": {
                    "choices": [
                      {
                        "finish_reason": "stop",
                        "message": {
                          "role": "assistant",
                          "content": [
                            {
                              "image": "https://dashscope-result-sz.oss-cn-shenzhen.aliyuncs.com/result.png"
                            }
                          ]
                        }
                      }
                    ]
                  },
                  "usage": { "height": 1024, "image_count": 1, "width": 1024 },
                  "request_id": "test-id"
                }
                """;
            }
            else
            {
                // OpenAI 兼容格式响应
                responseBody = """
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
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}