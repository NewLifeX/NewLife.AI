namespace NewLife.AI.Clients;

/// <summary>声明 AI 客户端的默认模型，配合 <see cref="AiClientAttribute"/> 使用</summary>
/// <remarks>
/// 同一类上可标注多个此特性。若类上有多个 <see cref="AiClientAttribute"/>，
/// 须通过 <see cref="Code"/> 属性指明该模型归属哪个服务商。
/// </remarks>
/// <param name="model">模型标识</param>
/// <param name="displayName">显示名称</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AiClientModelAttribute(String model, String displayName) : Attribute
{
    /// <summary>模型标识（API 请求中 model 字段的值）</summary>
    public String Model { get; } = model;

    /// <summary>模型显示名称</summary>
    public String DisplayName { get; } = displayName;

    /// <summary>所属服务商编码。类上仅有单个 AiClientAttribute 时可省略；多个时必须显式指定</summary>
    public String? Code { get; set; }

    /// <summary>是否支持思考/推理模式（如 DeepSeek-R1、Claude Thinking）</summary>
    public Boolean Thinking { get; set; }

    /// <summary>是否支持 Function Calling / Tool Use。默认 true</summary>
    public Boolean FunctionCalling { get; set; } = true;

    /// <summary>是否支持图片输入（视觉多模态）</summary>
    public Boolean Vision { get; set; }

    /// <summary>是否支持音频输入输出（如 GPT-4o-audio、Qwen-Omni）</summary>
    public Boolean Audio { get; set; }

    /// <summary>是否支持文生图</summary>
    public Boolean ImageGeneration { get; set; }

    /// <summary>是否支持文生视频（如 Sora、Wan2）</summary>
    public Boolean VideoGeneration { get; set; }

    /// <summary>上下文窗口大小（Token 数）。0 表示未知</summary>
    public Int32 ContextLength { get; set; }
}
