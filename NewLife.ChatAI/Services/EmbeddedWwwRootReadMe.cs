/// <summary>
/// wwwroot 嵌入到 DLL 的实现说明
/// </summary>
/// <remarks>
/// 当前实现将 wwwroot 目录中的所有文件嵌入到编译后的 DLL 中。
/// 运行时从 DLL 读取这些资源，而不是从文件系统。
/// 
/// **配置方式：**
/// 1. .csproj 中设置 GenerateEmbeddedFilesManifest=true
/// 2. 在 Program.cs 中使用 ChatAiStaticFilesService.GetEmbeddedFileProvider()
/// 3. 通过 UseStaticFiles() 中间件提供嵌入的静态文件
/// 
/// **优点：**
/// - 无需在输出目录中存放 wwwroot 文件
/// - 无需打包 wwwroot 到 NuGet（仅作为库时）
/// - DLL 完全自洽，便于部署和分发
/// 
/// **使用方式：**
/// 
/// 1. 在 Startup 代码中配置（已在 Program.cs 完成）：
///    var embeddedFileProvider = ChatAiStaticFilesService.GetEmbeddedFileProvider();
///    app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedFileProvider });
/// 
/// 2. 在控制器或服务中读取嵌入资源：
///    // 读取文本文件
///    var html = await ChatAiStaticFilesService.GetResourceTextAsync("chat.html");
///    
///    // 读取二进制文件
///    var bytes = await ChatAiStaticFilesService.GetResourceBytesAsync("assets/index.js");
///    
///    // 直接获取流
///    using var stream = ChatAiStaticFilesService.GetResourceStream("chat.html");
/// 
/// 3. 浏览器请求 /chat.html 会自动从嵌入资源加载
/// 
/// **开发时注意事项：**
/// - 需要编译项目后，wwwroot 文件才会嵌入到 DLL
/// - 开发环境可在 appsettings.Development.json 中配置，切换为文件系统提供程序以获得热重载
/// - 修改 wwwroot 文件后需要重新编译
/// 
/// **生产部署：**
/// - 只需部署编译后的 DLL（包含所有 wwwroot 文件）
/// - 不需要单独部署 wwwroot 目录
/// </remarks>
public class EmbeddedWwwRootReadMe { }
