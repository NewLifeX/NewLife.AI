using Microsoft.Extensions.FileProviders;

namespace NewLife.ChatAI.Services;

/// <summary>聊天AI静态文件服务，从嵌入资源提供wwwroot文件</summary>
public class ChatAiStaticFilesService
{
    /// <summary>获取嵌入的文件提供程序</summary>
    public static IFileProvider GetEmbeddedFileProvider()
    {
        var assembly = typeof(ChatAiStaticFilesService).Assembly;
        return new EmbeddedFileProvider(assembly, "NewLife.ChatAI.wwwroot");
    }

    /// <summary>获取资源流</summary>
    /// <param name="filePath">相对于wwwroot的文件路径，例如 "chat.html"</param>
    /// <returns>资源流，如果文件不存在则返回null</returns>
    public static Stream? GetResourceStream(String filePath)
    {
        var assembly = typeof(ChatAiStaticFilesService).Assembly;
        var resourceName = $"NewLife.ChatAI.wwwroot.{filePath}".Replace('/', '.');
        
        try
        {
            var stream = assembly.GetManifestResourceStream(resourceName);
            return stream;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取资源文本内容</summary>
    /// <param name="filePath">相对于wwwroot的文件路径</param>
    /// <returns>文件内容</returns>
    public static async Task<String?> GetResourceTextAsync(String filePath)
    {
        using var stream = GetResourceStream(filePath);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>获取资源字节内容</summary>
    /// <param name="filePath">相对于wwwroot的文件路径</param>
    /// <returns>文件内容</returns>
    public static async Task<Byte[]?> GetResourceBytesAsync(String filePath)
    {
        using var stream = GetResourceStream(filePath);
        if (stream == null) return null;

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
