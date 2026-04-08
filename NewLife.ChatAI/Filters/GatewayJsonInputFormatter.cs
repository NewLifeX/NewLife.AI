using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using NewLife.Serialization;

namespace NewLife.ChatAI.Filters;

/// <summary>网关 JSON 输入格式化器。根据 Action 上的标记属性选择不同的 JsonSerializerOptions 反序列化请求体</summary>
/// <remarks>
/// 通过在端点元数据中查找 <see cref="SnakeCaseBodyAttribute"/> 或 <see cref="CamelCaseBodyAttribute"/>，
/// 自动切换 snake_case / camelCase 的 <see cref="JsonSerializerOptions"/>。
/// 未标记的 Action 使用全局默认配置（camelCase），不影响非网关控制器。
/// </remarks>
public class GatewayJsonInputFormatter : TextInputFormatter
{
    private readonly JsonSerializerOptions _defaultOptions;
    private readonly JsonSerializerOptions _snakeCaseOptions;
    private readonly JsonSerializerOptions _camelCaseOptions;

    /// <summary>实例化网关 JSON 输入格式化器</summary>
    /// <param name="defaultOptions">全局默认 JsonSerializerOptions（由 MVC 框架传入）</param>
    public GatewayJsonInputFormatter(JsonSerializerOptions defaultOptions)
    {
        _defaultOptions = defaultOptions;

        var snake = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(snake, true);
        _snakeCaseOptions = snake;

        var camel = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(camel, true);
        _camelCaseOptions = camel;

        SupportedEncodings.Add(UTF8EncodingWithoutBOM);
        SupportedEncodings.Add(UTF16EncodingLittleEndian);
        SupportedMediaTypes.Add("application/json");
        SupportedMediaTypes.Add("text/json");
        SupportedMediaTypes.Add("application/*+json");
    }

    /// <summary>读取并反序列化请求体。根据端点元数据中的标记属性选择对应的 JsonSerializerOptions</summary>
    /// <param name="context">输入格式化器上下文</param>
    /// <param name="encoding">字符编码</param>
    /// <returns>反序列化结果</returns>
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        var httpContext = context.HttpContext;
        var endpoint = httpContext.GetEndpoint();

        // 根据标记属性选择 JsonSerializerOptions；无标记时使用全局默认
        var options = _defaultOptions;
        if (endpoint?.Metadata.GetMetadata<SnakeCaseBodyAttribute>() != null)
            options = _snakeCaseOptions;
        else if (endpoint?.Metadata.GetMetadata<CamelCaseBodyAttribute>() != null)
            options = _camelCaseOptions;

        try
        {
            var model = await JsonSerializer.DeserializeAsync(httpContext.Request.Body, context.ModelType, options, httpContext.RequestAborted).ConfigureAwait(false);
            if (model == null)
                return InputFormatterResult.NoValue();

            return InputFormatterResult.Success(model);
        }
        catch (JsonException ex)
        {
            context.ModelState.TryAddModelError(context.ModelName, ex.Message);
            return InputFormatterResult.Failure();
        }
    }
}
