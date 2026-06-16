using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.AI.Tools;
using NewLife.Collections;

namespace NewLife.AI.Coding;

/// <summary>编码专用工具集。提供文件读写、目录浏览、代码搜索、命令执行和用户交互等 8 个工具，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>
/// 支持工作区沙箱：设置 WorkspacePath 后，所有文件操作限制在工作区范围内；超出范围的操作需通过 ApprovalProvider 审批。
/// </remarks>
/// <remarks>初始化编码工具集并指定工作区路径</remarks>
/// <param name="workspacePath">工作区根路径</param>
public class CodingTools(String? workspacePath = null)
{
    #region 属性
    /// <summary>工作区根路径。设置后所有文件操作限制在此范围内；为 null 时不限制</summary>
    public String? WorkspacePath { get; set; } = workspacePath;

    /// <summary>工具审批提供者。工作区外的文件操作需经此审批；为 null 时直接拒绝</summary>
    public IToolApprovalProvider? ApprovalProvider { get; set; }
    #endregion

    #region 文件读写

    /// <summary>读取指定文件的内容，支持行范围截取</summary>
    /// <param name="path">相对于工作区的文件路径</param>
    /// <param name="startLine">起始行号（1-based），不指定则从第一行开始</param>
    /// <param name="endLine">结束行号（含），不指定则读到末尾</param>
    /// <returns>文件内容，含行号前缀</returns>
    [ToolDescription("read_file")]
    [DisplayName("读取文件")]
    [Description("读取指定文件的内容，支持行范围截取。返回带行号前缀的文本")]
    public Task<String> ReadFileAsync(
        [Description("相对于工作区的文件路径")] String path,
        [Description("起始行号（1-based），不指定则从第一行开始")] Int32? startLine = null,
        [Description("结束行号（含），不指定则读到末尾")] Int32? endLine = null)
    {
        var fullPath = ResolvePath(path);
        if (fullPath == null) return Task.FromResult("Error: 路径超出工作区范围，访问被拒绝");

        if (!File.Exists(fullPath))
            return Task.FromResult($"Error: 文件不存在 - {path}");

        try
        {
            var lines = File.ReadAllLines(fullPath);
            var totalLines = lines.Length;

            var from = startLine ?? 1;
            var to = endLine ?? totalLines;

            // 边界保护
            if (from < 1) from = 1;
            if (to > totalLines) to = totalLines;
            if (from > to) return Task.FromResult($"Error: 起始行 {from} 大于结束行 {to}");

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine($"// {path} (lines {from}-{to} of {totalLines})");
            for (var i = from - 1; i < to; i++)
            {
                sb.AppendLine($"{i + 1,6}: {lines[i]}");
            }

            if (to < totalLines)
                sb.AppendLine($"// ... ({totalLines - to} more lines omitted)");

            return Task.FromResult(sb.Return(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: 读取文件失败 - {ex.Message}");
        }
    }

    /// <summary>创建或覆盖文件内容</summary>
    /// <param name="path">相对于工作区的文件路径</param>
    /// <param name="content">要写入的文件内容</param>
    /// <returns>写入结果</returns>
    [ToolDescription("write_file")]
    [DisplayName("写入文件")]
    [Description("创建或覆盖文件内容。工作区外的写入需要审批")]
    public async Task<String> WriteFileAsync(
        [Description("相对于工作区的文件路径")] String path,
        [Description("要写入的文件内容")] String content)
    {
        var fullPath = ResolvePath(path);
        if (fullPath == null)
        {
            // 工作区外路径，尝试审批
            var approved = await TryApproveAsync("write_file", path);
            if (!approved) return "Error: 路径超出工作区范围，审批未通过";
            fullPath = Path.GetFullPath(path);
        }

        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var existed = File.Exists(fullPath);
            File.WriteAllText(fullPath, content);

            var size = content.Length;
            return existed
                ? $"已覆盖文件: {path} ({size} 字符)"
                : $"已创建文件: {path} ({size} 字符)";
        }
        catch (Exception ex)
        {
            return $"Error: 写入文件失败 - {ex.Message}";
        }
    }

    #endregion

    #region 目录浏览

    /// <summary>列出目录结构，以树形文本展示</summary>
    /// <param name="path">相对于工作区的目录路径，不指定则为工作区根目录</param>
    /// <param name="maxDepth">最大递归深度，默认 2</param>
    /// <returns>树形目录结构文本</returns>
    [ToolDescription("list_dir")]
    [DisplayName("列出目录")]
    [Description("列出目录结构，以树形文本展示。支持限制递归深度")]
    public Task<String> ListDirAsync(
        [Description("相对于工作区的目录路径，默认工作区根")] String? path = null,
        [Description("最大递归深度，默认 2")] Int32? maxDepth = 2)
    {
        var targetPath = String.IsNullOrEmpty(path) ? WorkspacePath : ResolvePath(path);
        if (targetPath == null) return Task.FromResult("Error: 路径超出工作区范围");

        if (!Directory.Exists(targetPath))
            return Task.FromResult($"Error: 目录不存在 - {path ?? WorkspacePath}");

        try
        {
            var depth = maxDepth ?? 2;
            if (depth < 1) depth = 1;
            if (depth > 5) depth = 5;

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine(targetPath);
            ListDirRecursive(targetPath, "", depth, sb);
            return Task.FromResult(sb.Return(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: 列出目录失败 - {ex.Message}");
        }
    }

    private void ListDirRecursive(String dir, String indent, Int32 remainingDepth, StringBuilder sb)
    {
        if (remainingDepth <= 0) return;

        try
        {
            var entries = Directory.GetFileSystemEntries(dir)
                .OrderBy(e => (File.GetAttributes(e) & FileAttributes.Directory) == 0)
                .ThenBy(e => Path.GetFileName(e))
                .Take(200) // 限制单层数量
                .ToList();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var isLast = i == entries.Count - 1;
                var prefix = isLast ? "└── " : "├── ";
                var childIndent = isLast ? "    " : "│   ";
                var name = Path.GetFileName(entry);

                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                {
                    sb.AppendLine($"{indent}{prefix}{name}/");
                    ListDirRecursive(entry, indent + childIndent, remainingDepth - 1, sb);
                }
                else
                {
                    sb.AppendLine($"{indent}{prefix}{name}");
                }
            }
        }
        catch
        {
            // 权限不足时静默跳过
            sb.AppendLine($"{indent}... (无法访问)");
        }
    }

    #endregion

    #region 代码搜索

    /// <summary>按文件名模式搜索文件</summary>
    /// <param name="pattern">glob 模式，如 **/*.cs、*.json、src/**/*.ts</param>
    /// <param name="path">搜索起始目录，默认工作区根</param>
    /// <returns>匹配的文件路径列表</returns>
    [ToolDescription("glob_search")]
    [DisplayName("文件搜索")]
    [Description("按文件名模式搜索文件，支持 ** 递归匹配")]
    public Task<String> GlobSearchAsync(
        [Description("glob 模式，如 **/*.cs")] String pattern,
        [Description("搜索起始目录，默认工作区根")] String? path = null)
    {
        var searchRoot = String.IsNullOrEmpty(path) ? WorkspacePath : ResolvePath(path);
        if (searchRoot == null) return Task.FromResult("Error: 搜索路径超出工作区范围");
        if (!Directory.Exists(searchRoot))
            return Task.FromResult($"Error: 目录不存在 - {path ?? WorkspacePath}");

        try
        {
            var results = GlobSearch(searchRoot, pattern, 200);
            if (results.Count == 0)
                return Task.FromResult($"未找到匹配 '{pattern}' 的文件");

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine($"找到 {results.Count} 个匹配 '{pattern}' 的文件:");
            foreach (var file in results)
            {
                var relativePath = WorkspacePath != null && file.StartsWith(WorkspacePath, StringComparison.OrdinalIgnoreCase)
                    ? file[(WorkspacePath.Length + 1)..]
                    : file;
                sb.AppendLine($"  {relativePath}");
            }

            return Task.FromResult(sb.Return(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: 文件搜索失败 - {ex.Message}");
        }
    }

    private static List<String> GlobSearch(String root, String pattern, Int32 maxResults)
    {
        var results = new List<String>();

        // 简单 glob 解析：将 ** 替换为递归搜索，* 替换为任意文件名
        var parts = pattern.Replace('\\', '/').Split('/');
        var searchOption = pattern.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var fileNamePattern = parts[^1];
        var searchDir = parts.Length > 1 && parts[0] != "**"
            ? Path.Combine(root, String.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(parts.Length - 1)))
            : root;

        if (!Directory.Exists(searchDir)) return results;

        try
        {
            var files = Directory.GetFiles(searchDir, fileNamePattern, searchOption);
            foreach (var file in files)
            {
                results.Add(file);
                if (results.Count >= maxResults) break;
            }
        }
        catch
        {
            // 权限不足跳过
        }

        return results;
    }

    /// <summary>在文件内容中搜索匹配的文本模式</summary>
    /// <param name="pattern">正则表达式模式</param>
    /// <param name="includePattern">限定搜索的文件 glob 模式，如 **/*.cs</param>
    /// <param name="path">搜索起始目录，默认工作区根</param>
    /// <param name="maxResults">最大结果数，默认 50</param>
    /// <returns>匹配结果，每行格式：文件路径:行号: 匹配内容</returns>
    [ToolDescription("grep_search")]
    [DisplayName("内容搜索")]
    [Description("在文件内容中搜索匹配正则表达式的文本，支持文件类型过滤和结果数限制")]
    public Task<String> GrepSearchAsync(
        [Description("正则表达式模式")] String pattern,
        [Description("限定搜索的文件 glob 模式，如 **/*.cs")] String? includePattern = null,
        [Description("搜索起始目录，默认工作区根")] String? path = null,
        [Description("最大结果数，默认 50")] Int32? maxResults = 50)
    {
        var searchRoot = String.IsNullOrEmpty(path) ? WorkspacePath : ResolvePath(path);
        if (searchRoot == null) return Task.FromResult("Error: 搜索路径超出工作区范围");
        if (!Directory.Exists(searchRoot))
            return Task.FromResult($"Error: 目录不存在 - {path ?? WorkspacePath}");

        var max = maxResults ?? 50;
        if (max < 1) max = 1;
        if (max > 200) max = 200;

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var filePattern = includePattern ?? "*";
            var results = new List<String>();
            var cancelled = false;

            var files = Directory.GetFiles(searchRoot, filePattern, SearchOption.AllDirectories);

            // 跳过常见非文本目录
            var skipDirs = new[] { "\\obj\\", "\\bin\\", "\\node_modules\\", "\\.git\\", "\\dist\\", "\\out\\", "\\TestResults\\" };

            foreach (var file in files)
            {
                if (skipDirs.Any(d => file.Contains(d))) continue;
                if (results.Count >= max) { cancelled = true; break; }

                try
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (results.Count >= max) { cancelled = true; break; }
                        if (regex.IsMatch(lines[i]))
                        {
                            var relativePath = WorkspacePath != null && file.StartsWith(WorkspacePath, StringComparison.OrdinalIgnoreCase)
                                ? file[(WorkspacePath.Length + 1)..]
                                : file;
                            results.Add($"{relativePath}:{i + 1}: {lines[i].Trim()}");
                        }
                    }
                }
                catch
                {
                    // 跳过无法读取的文件
                }
            }

            if (results.Count == 0)
                return Task.FromResult($"未找到匹配 '{pattern}' 的内容");

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine($"找到 {results.Count} 条匹配{(cancelled ? $"（已达上限 {max}）" : "")}:");
            foreach (var line in results)
            {
                sb.AppendLine($"  {line}");
            }

            return Task.FromResult(sb.Return(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: 内容搜索失败 - {ex.Message}");
        }
    }

    #endregion

    #region 命令执行

    /// <summary>执行 shell 命令并返回输出</summary>
    /// <param name="command">要执行的命令，如 dotnet build</param>
    /// <param name="workDir">工作目录，默认工作区根</param>
    /// <param name="timeout">超时秒数，默认 120 秒</param>
    /// <returns>命令的标准输出和标准错误</returns>
    [ToolDescription("run_command")]
    [DisplayName("执行命令")]
    [Description("执行 shell 命令并返回标准输出和标准错误。工作区外的执行需要审批")]
    public async Task<String> RunCommandAsync(
        [Description("要执行的命令，如 dotnet build")] String command,
        [Description("工作目录，默认工作区根")] String? workDir = null,
        [Description("超时秒数，默认 120")] Int32? timeout = 120)
    {
        if (String.IsNullOrWhiteSpace(command))
            return "Error: 命令不能为空";

        var workingDir = String.IsNullOrEmpty(workDir) ? WorkspacePath : ResolvePath(workDir);
        if (workingDir == null)
        {
            var approved = await TryApproveAsync("run_command", $"{command} (workDir: {workDir})");
            if (!approved) return "Error: 工作目录超出工作区范围，审批未通过";
            workingDir = String.IsNullOrEmpty(workDir) ? Environment.CurrentDirectory : Path.GetFullPath(workDir);
        }

        var timeoutMs = (timeout ?? 120) * 1000;
        if (timeoutMs < 1000) timeoutMs = 1000;
        if (timeoutMs > 600_000) timeoutMs = 600_000; // 最大 10 分钟

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var output = Pool.StringBuilder.Get();
            var error = Pool.StringBuilder.Get();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return $"Error: 命令执行超时（{timeoutMs / 1000}秒）\n已捕获输出:\n{output}";
            }

            process.WaitForExit();

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine($"[exit code: {process.ExitCode}]");
            sb.AppendLine("[stdout]:");
            sb.Append(output.Return(true));
            if (error.Length > 0)
            {
                sb.AppendLine("[stderr]:");
                sb.Append(error.Return(true));
            }

            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return $"Error: 命令执行失败 - {ex.Message}";
        }
    }

    /// <summary>获取项目的编译错误信息</summary>
    /// <param name="projectPath">项目文件路径或目录，默认工作区根</param>
    /// <returns>编译错误列表</returns>
    [ToolDescription("get_errors")]
    [DisplayName("获取编译错误")]
    [Description("运行 dotnet build 并解析返回 MSBuild 格式的错误信息")]
    public async Task<String> GetErrorsAsync(
        [Description("项目文件路径或目录，默认工作区根")] String? projectPath = null)
    {
        var targetPath = String.IsNullOrEmpty(projectPath) ? WorkspacePath : ResolvePath(projectPath);
        if (targetPath == null) return "Error: 项目路径超出工作区范围";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --no-restore",
                    WorkingDirectory = targetPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var output = Pool.StringBuilder.Get();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(120_000))
            {
                try { process.Kill(); } catch { }
                return "Error: dotnet build 执行超时";
            }

            process.WaitForExit();

            var raw = output.Return(true);

            // 解析 MSBuild 错误格式：path(line,col): error CODE: message
            var errorLines = new List<String>();
            var errorRegex = new Regex(@"^(.+?)\((\d+),(\d+)\):\s*(error|warning)\s+(\w+):\s*(.+)$", RegexOptions.Multiline);
            var matches = errorRegex.Matches(raw);

            if (matches.Count == 0)
            {
                if (process.ExitCode == 0)
                    return "编译成功，无错误";

                return $"编译失败 (exit code: {process.ExitCode})\n{raw}";
            }

            var sb = Pool.StringBuilder.Get();
            sb.AppendLine($"编译失败 (exit code: {process.ExitCode})，发现 {matches.Count} 条错误/警告:");
            foreach (Match match in matches)
            {
                var file = match.Groups[1].Value;
                var line = match.Groups[2].Value;
                var severity = match.Groups[4].Value.ToUpperInvariant();
                var code = match.Groups[5].Value;
                var message = match.Groups[6].Value;

                var relativePath = WorkspacePath != null && file.StartsWith(WorkspacePath, StringComparison.OrdinalIgnoreCase)
                    ? file[(WorkspacePath.Length + 1)..]
                    : file;

                sb.AppendLine($"  [{severity}] {relativePath}({line}): {code}: {message}");
            }

            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return $"Error: 获取编译错误失败 - {ex.Message}";
        }
    }

    #endregion

    #region 用户交互

    /// <summary>向用户提问并获取回答。当 Agent 遇到不确定的情况时使用此工具暂停并请求用户输入</summary>
    /// <param name="question">向用户提出的问题</param>
    /// <returns>用户的回答</returns>
    [ToolDescription("ask_user")]
    [DisplayName("询问用户")]
    [Description("当 Agent 遇到不确定的情况时，暂停并向用户提问。仅在控制台环境下有效")]
    public Task<String> AskUserAsync(
        [Description("向用户提出的问题")] String question)
    {
        Console.WriteLine();
        Console.WriteLine($"🤖 [Agent 提问] {question}");
        Console.Write("👤 [你的回答] > ");
        var answer = Console.ReadLine();
        return Task.FromResult(answer ?? "");
    }

    #endregion

    #region 辅助

    /// <summary>解析并验证路径。在工作区内返回全路径，超出范围返回 null</summary>
    private String? ResolvePath(String path)
    {
        if (String.IsNullOrWhiteSpace(path)) return WorkspacePath;

        try
        {
            // 相对路径：先与工作区根组合，避免 Path.GetFullPath 基于当前目录解析
            if (!Path.IsPathRooted(path) && !String.IsNullOrEmpty(WorkspacePath))
                path = Path.Combine(WorkspacePath, path);

            var fullPath = Path.GetFullPath(path);

            // 未设置工作区时不限制
            if (String.IsNullOrEmpty(WorkspacePath)) return fullPath;

            var normalizedWorkspace = Path.GetFullPath(WorkspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedPath.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>尝试审批工作区外的操作</summary>
    private async Task<Boolean> TryApproveAsync(String toolName, String details)
    {
        if (ApprovalProvider == null) return false;

        try
        {
            var result = await ApprovalProvider.RequestApprovalAsync(
                toolName,
                $"工作区外操作: {details}",
                CancellationToken.None);
            return result.Approved;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
