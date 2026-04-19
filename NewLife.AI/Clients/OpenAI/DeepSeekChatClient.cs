using NewLife.AI.Models;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>DeepSeek（深度求索）对话客户端。兼容 OpenAI Chat Completions 协议，处理 DeepSeek 专属参数差异</summary>
/// <remarks>
/// 与标准 OpenAI 协议的主要差异：
/// <list type="bullet">
/// <item>思考控制：<see cref="IChatRequest.EnableThinking"/> 映射为 <c>thinking: {type: "enabled"|"disabled"}</c>，而非 OpenAI 不支持的 <c>enable_thinking</c></item>
/// <item>deepseek-reasoner 始终输出 <c>reasoning_content</c> 思维链，不支持 temperature/top_p/presence_penalty/frequency_penalty 及 Function Calling</item>
/// <item>reasoning_content 字段由基类 <see cref="OpenAIChatClient.ParseChatMessage"/> 负责解析，无需额外处理</item>
/// </list>
/// </remarks>
[AiClient("DeepSeek", "深度求索", "https://api.deepseek.com", Description = "DeepSeek 系列推理和对话模型", Order = 2)]
[AiClientModel("deepseek-chat", "DeepSeek Chat", Code = "DeepSeek", FunctionCalling = true)]
[AiClientModel("deepseek-reasoner", "DeepSeek Reasoner", Code = "DeepSeek", Thinking = true)]
public class DeepSeekChatClient : OpenAIChatClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "深度求索";
    #endregion

    #region 构造
    /// <summary>用连接选项初始化 DeepSeek 客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public DeepSeekChatClient(AiClientOptions options) : base(options) { }

    /// <summary>以 API 密钥和可选模型快速创建 DeepSeek 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public DeepSeekChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 辅助
    /// <summary>构建 DeepSeek 专属请求体</summary>
    /// <remarks>
    /// 在标准 OpenAI 请求基础上做两项调整：
    /// <list type="number">
    /// <item>将 <c>enable_thinking</c> 替换为 DeepSeek 协议的 <c>thinking: {type: "enabled"|"disabled"}</c></item>
    /// <item>deepseek-reasoner 不支持采样调参与工具调用，相关字段一律移除，避免无谓噪音</item>
    /// </list>
    /// </remarks>
    /// <param name="request">统一请求接口</param>
    /// <returns>可直接序列化为 JSON 的请求字典</returns>
    protected override Object BuildRequest(IChatRequest request)
    {
        // 用 BuildBody 返回字典，便于精细增删字段
        var dic = ChatCompletionRequest.BuildBody(request);

        // DeepSeek 不识别 enable_thinking，替换为 thinking: {type: ...}
        dic.Remove("enable_thinking");
        if (request.EnableThinking != null)
        {
            var thinkingType = request.EnableThinking.Value ? "enabled" : "disabled";
            dic["thinking"] = new Dictionary<String, Object> { ["type"] = thinkingType };
        }

        // deepseek-reasoner 始终推理，temperature/top_p 等传入不报错但无效；
        // tools/tool_choice 不支持且可能导致 API 报错，一并移除
        var model = request.Model ?? _options.Model ?? "";
        if (model.Contains("reasoner", StringComparison.OrdinalIgnoreCase))
        {
            dic.Remove("temperature");
            dic.Remove("top_p");
            dic.Remove("presence_penalty");
            dic.Remove("frequency_penalty");
            dic.Remove("tools");
            dic.Remove("tool_choice");
            dic.Remove("parallel_tool_calls");
        }

        return dic;
    }
    #endregion

    #region 不支持能力（覆盖以明确告知调用者）
    /// <summary>DeepSeek 不支持文生图，始终抛出异常</summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<ImageGenerationResponse?> TextToImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持文生图，请改用 OpenAI / DashScope / Gemini");

    /// <summary>DeepSeek 不支持图像编辑，始终抛出异常</summary>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<ImageGenerationResponse?> EditImageAsync(ImageEditsRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持图像编辑，请改用 OpenAI / DashScope");

    /// <summary>DeepSeek 不支持语音合成（TTS），始终抛出异常</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持语音合成（TTS），请改用 OpenAI / DashScope");

    /// <summary>DeepSeek 不支持语音转文字（STT），始终抛出异常</summary>
    /// <param name="request">语音转录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持语音转文字（STT），请改用 OpenAI / DashScope");

    /// <summary>DeepSeek 不支持视频生成，始终抛出异常</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<VideoTaskSubmitResponse> SubmitVideoGenerationAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持视频生成，请改用 DashScope / OpenAI Sora");

    /// <summary>DeepSeek 不支持视频任务查询，始终抛出异常</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task<VideoTaskStatusResponse> GetVideoTaskAsync(String taskId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeepSeek 不支持视频生成，请改用 DashScope / OpenAI Sora");
    #endregion
}
