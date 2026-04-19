#nullable enable
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTest.Gateway;

/// <summary>API 网关集成测试。覆盖 GatewayController 全部公开端点</summary>
/// <remarks>
/// 通过 ChatAIWebAppFactory 在进程内启动 ChatAI，数据库中已配置密钥 sk-NewLifeAI2026。
/// </remarks>
public class GatewayIntegrationTests : IDisposable, IClassFixture<ChatAIWebAppFactory>
{
    private const String ApiKey = "sk-NewLifeAI2026";
    private const String TestModel = "qwen3.5-flash";

    private readonly HttpClient _http;
    private readonly HttpClient _httpBadKey;

    public GatewayIntegrationTests(ChatAIWebAppFactory factory)
    {
        _http = factory.CreateDefaultClient();
        _http.Timeout = TimeSpan.FromSeconds(60);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);

        _httpBadKey = factory.CreateDefaultClient();
        _httpBadKey.Timeout = TimeSpan.FromSeconds(15);
        _httpBadKey.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "sk-invalid-key-xyz-000");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
        _httpBadKey.Dispose();
    }

    /// <summary>构建 application/json 请求体</summary>
    private static StringContent JsonBody(Object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    #region GET /v1/models

    [Fact]
    [DisplayName("GET /v1/models 返回模型列表，object=list")]
    public async Task ListModels_Returns_ModelList()
    {
        var resp = await _http.GetAsync("v1/models");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("application/json", resp.Content.Headers.ContentType?.MediaType ?? "");

        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(body);
        Assert.NotNull(doc);
        Assert.Equal("list", doc["object"]?.GetValue<String>());
        Assert.NotNull(doc["data"]);
        Assert.True(doc["data"]!.AsArray().Count > 0, "模型列表不应为空");
    }

    [Fact]
    [DisplayName("GET /v1/models 每个模型对象包含上下文长度和6个能力字段")]
    public async Task ListModels_Returns_ContextLength_And_Capabilities()
    {
        var resp = await _http.GetAsync("v1/models");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(body);
        Assert.NotNull(doc);

        var data = doc!["data"]!.AsArray();
        Assert.True(data.Count > 0, "模型列表不应为空");

        // 验证第一个模型对象包含所有扩展字段
        var first = data[0]!;
        Assert.True(first["context_length"] != null, "缺少 context_length 字段");
        Assert.True(first["support_thinking"] != null, "缺少 support_thinking 字段");
        Assert.True(first["support_function_calling"] != null, "缺少 support_function_calling 字段");
        Assert.True(first["support_vision"] != null, "缺少 support_vision 字段");
        Assert.True(first["support_audio"] != null, "缺少 support_audio 字段");
        Assert.True(first["support_image_generation"] != null, "缺少 support_image_generation 字段");
        Assert.True(first["support_video_generation"] != null, "缺少 support_video_generation 字段");
    }

    [Fact]
    [DisplayName("GET /v1/models 无效密钥返回 401 + INVALID_API_KEY")]
    public async Task ListModels_InvalidKey_Returns_401()
    {
        var resp = await _httpBadKey.GetAsync("v1/models");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_API_KEY", doc?["code"]?.GetValue<String>());
    }

    #endregion

    #region POST /v1/chat/completions

    [Fact]
    [DisplayName("POST /v1/chat/completions 非流式：model/choices/usage 均正确")]
    public async Task ChatCompletions_NonStream_Returns_ValidResponse()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "用一句话打个招呼" } },
            max_tokens = 50,
            stream = false,
        });

        var resp = await _http.PostAsync("v1/chat/completions", body);

        // 502 表示后端提供商暂不可用，不是本地网关的问题，跳过后续验证
        if ((Int32)resp.StatusCode == 502) return;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.NotEmpty(json);

        var doc = JsonNode.Parse(json);
        Assert.NotNull(doc);
        // 验证响应字段为 snake_case（OpenAI 标准）
        Assert.Equal("chat.completion", doc["object"]?.GetValue<String>());
        Assert.NotNull(doc["choices"]);
        Assert.True(doc["choices"]!.AsArray().Count > 0);

        var content = doc["choices"]![0]!["message"]?["content"]?.GetValue<String>();
        Assert.False(String.IsNullOrEmpty(content));

        // 验证 finish_reason 为 snake_case（非 FinishReason PascalCase）
        var finishReason = doc["choices"]![0]!["finish_reason"]?.GetValue<String>();
        Assert.False(String.IsNullOrEmpty(finishReason));

        // 验证 usage.total_tokens 为 snake_case
        var totalTokens = doc["usage"]?["total_tokens"]?.GetValue<Int32>();
        Assert.True(totalTokens > 0, "total_tokens 应大于 0");
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 流式：返回 SSE 格式，包含 data: chunk 和 [DONE]")]
    public async Task ChatCompletions_Stream_Returns_SseChunks()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "1+1=" } },
            max_tokens = 20,
            stream = true,
            stream_options = new { include_usage = true },
        });

        var resp = await _http.PostAsync("v1/chat/completions", body);

        // 502 表示后端提供商暂不可用，不是本地网关的问题，跳过后续验证
        if ((Int32)resp.StatusCode == 502) return;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);

        var rawBody = await resp.Content.ReadAsStringAsync();
        Assert.Contains("data: ", rawBody);
        Assert.Contains("[DONE]", rawBody);

        // 验证每个 chunk 是合法 JSON 且 object=chat.completion.chunk
        var chunkCount = 0;
        foreach (var line in rawBody.Split('\n'))
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..].Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            var chunk = JsonNode.Parse(data);
            Assert.NotNull(chunk);
            Assert.Equal("chat.completion.chunk", chunk["object"]?.GetValue<String>());
            chunkCount++;
        }
        Assert.True(chunkCount > 0, "流式响应应至少包含一个数据块");
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 携带 stream_options 不干扰模型路由（原 Bug 复现验证）")]
    public async Task ChatCompletions_StreamOptions_Does_Not_Break_Routing()
    {
        // 原 Bug：SystemJson IExtend 转换器导致 model 字段丢失，返回 404 "未找到模型 ''"
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "解释量子计算" } },
            stream = true,
            stream_options = new { include_usage = true },
            max_tokens = 30,
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_current_time",
                        description = "获取当前日期和时间信息",
                        parameters = new { type = "object", properties = new { } },
                    }
                },
            },
        });

        var resp = await _http.PostAsync("v1/chat/completions", body);

        // 原 Bug：IExtend 导致 model 字段丢失，返回 404 "未找到模型 ''"
        // 修复后：model 正确提取，gateway 正常路由到提供商
        // → 不应返回 404（如返回 504/502 则说明 gateway 正常但后端暂不可用，亦可接受）
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);

        // 若后端正常，应得到 200 流式响应
        if ((Int32)resp.StatusCode == 200)
        {
            Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 携带 tools 参数正常响应")]
    public async Task ChatCompletions_WithTools_Returns_ValidResponse()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "现在几点了？" } },
            max_tokens = 100,
            stream = false,
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_current_time",
                        description = "获取当前时间",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                timezone = new { type = "string", description = "时区名称" }
                            }
                        },
                    }
                }
            },
            tool_choice = "auto",
        });

        var resp = await _http.PostAsync("v1/chat/completions", body);

        // 502 表示后端提供商暂不可用，不是本地网关的问题，跳过后续验证
        if ((Int32)resp.StatusCode == 502) return;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.NotNull(doc);
        Assert.NotNull(doc["choices"]);
        Assert.True(doc["choices"]!.AsArray().Count > 0, "choices 不应为空");
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 无效密钥返回 401 + INVALID_API_KEY")]
    public async Task ChatCompletions_InvalidApiKey_Returns_401()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "hello" } },
        });

        var resp = await _httpBadKey.PostAsync("v1/chat/completions", body);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_API_KEY", doc?["code"]?.GetValue<String>());
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 未知模型返回 404 + MODEL_NOT_FOUND，错误消息含模型名")]
    public async Task ChatCompletions_UnknownModel_Returns_404_With_ModelName()
    {
        var unknownModel = "non-existent-model-xyz-99999";
        var body = JsonBody(new
        {
            model = unknownModel,
            messages = new[] { new { role = "user", content = "hello" } },
        });

        var resp = await _http.PostAsync("v1/chat/completions", body);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("MODEL_NOT_FOUND", doc?["code"]?.GetValue<String>());
        Assert.Contains(unknownModel, doc?["message"]?.GetValue<String>() ?? "");
    }

    [Fact]
    [DisplayName("POST /v1/chat/completions 请求体格式错误返回 400 + INVALID_REQUEST")]
    public async Task ChatCompletions_MalformedBody_Returns_400()
    {
        var badBody = new StringContent("{ not valid json !!!}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("v1/chat/completions", badBody);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_REQUEST", doc?["code"]?.GetValue<String>());
    }

    #endregion

    #region POST /v1/responses

    [Fact]
    [DisplayName("POST /v1/responses 等价于 /v1/chat/completions，正常返回响应")]
    public async Task Responses_Endpoint_Works_Like_Chat()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "你好" } },
            max_tokens = 30,
            stream = false,
        });

        var resp = await _http.PostAsync("v1/responses", body);

        // 502 表示后端提供商暂不可用，不是本地网关的问题，跳过后续验证
        if ((Int32)resp.StatusCode == 502) return;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.NotNull(doc?["choices"]);
    }

    #endregion

    #region POST /v1/messages (Anthropic 兼容)

    [Fact]
    [DisplayName("POST /v1/messages Anthropic 兼容端点正常响应")]
    public async Task Messages_Anthropic_Endpoint_Works()
    {
        var body = JsonBody(new
        {
            model = TestModel,
            messages = new[] { new { role = "user", content = "你好" } },
            max_tokens = 30,
            stream = false,
        });

        var resp = await _http.PostAsync("v1/messages", body);

        // 502 表示后端提供商暂不可用，不是本地网关的问题，跳过后续验证
        if ((Int32)resp.StatusCode == 502) return;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.NotNull(doc);
        // /v1/messages 返回 Anthropic 协议格式（type/content），不是 OpenAI 的 choices 格式
        Assert.Equal("message", doc["type"]?.GetValue<String>());
        Assert.NotNull(doc["content"]);
        Assert.True(doc["content"]!.AsArray().Count > 0, "Anthropic content 不应为空");
        var text = doc["content"]![0]?["text"]?.GetValue<String>();
        Assert.False(String.IsNullOrEmpty(text));
    }

    #endregion
}
