namespace NewLife.AI.Models;

/// <summary>多模态内容辅助方法。各协议请求构建共享的 data URI 解析与格式提取逻辑</summary>
internal static class AIContentHelper
{
    /// <summary>解析 data URI 为 base64 字符串。如 data:image/jpeg;base64,xxxx → xxxx；非 data URI 返回 null</summary>
    /// <param name="uri">资源地址</param>
    /// <returns>base64 内容，无法解析返回 null</returns>
    public static String? ParseDataUri(String? uri)
    {
        if (String.IsNullOrEmpty(uri) || !uri!.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

        var idx = uri.IndexOf(',');
        if (idx <= 0) return null;
        return uri[(idx + 1)..];
    }

    /// <summary>从媒体类型提取格式标识。如 image/jpeg → jpeg；无斜杠时原样返回；为空返回 png</summary>
    /// <param name="mediaType">媒体类型</param>
    /// <returns>格式标识</returns>
    public static String GetFormat(String? mediaType)
    {
        if (String.IsNullOrEmpty(mediaType)) return "png";
        var format = mediaType!;
        if (format.Contains('/')) format = format.Split('/')[^1];
        return format;
    }
}
