using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewLife.ChatAI.Services;

/// <summary>Int64 JSON 转换器。将 Int64 序列化为字符串，避免 JavaScript 精度丢失</summary>
/// <remarks>
/// JavaScript Number.MAX_SAFE_INTEGER = 2^53-1 ≈ 9×10^15，
/// 而 Snowflake ID 通常为 18-19 位数字（约 10^18），超出安全整数范围。
/// 将 Int64 值序列化为 JSON 字符串，确保前后端精度一致。
/// </remarks>
public class LongJsonConverter : JsonConverter<Int64>
{
    /// <summary>反序列化：同时支持从字符串和数字读取</summary>
    public override Int64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (Int64.TryParse(str, out var val)) return val;
            return 0;
        }

        return reader.GetInt64();
    }

    /// <summary>序列化：写出为字符串</summary>
    public override void Write(Utf8JsonWriter writer, Int64 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
