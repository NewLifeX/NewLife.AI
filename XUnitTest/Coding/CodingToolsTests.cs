using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using NewLife.AI.Coding;
using Xunit;

namespace XUnitTest.Coding;

/// <summary>编码工具集单元测试</summary>
[DisplayName("编码工具集测试")]
public class CodingToolsTests : IDisposable
{
    private readonly String _tempDir;
    private readonly CodingTools _tools;

    public CodingToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CodingToolsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // 创建测试文件
        File.WriteAllText(Path.Combine(_tempDir, "test.cs"), """
// Test file
using System;

namespace Test;

public class TestClass
{
    public String GetName() => "test";
    
    private Int32 _count;
    
    public void DoSomething()
    {
        _count++;
    }
}
""");

        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "# Test Project\n\nThis is a test project.");

        // 创建子目录
        var subDir = Path.Combine(_tempDir, "SubDir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.cs"), "// nested file");

        _tools = new CodingTools(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    #region read_file

    [Fact]
    [DisplayName("read_file—有效路径返回内容")]
    public async Task ReadFile_ValidPath_ReturnsContent()
    {
        var result = await _tools.ReadFileAsync("test.cs");

        Assert.Contains("public class TestClass", result);
        Assert.Contains("using System;", result);
    }

    [Fact]
    [DisplayName("read_file—行范围截取正确")]
    public async Task ReadFile_LineRange_ReturnsCorrectLines()
    {
        // 使用独立的测试文件避免被其他测试覆盖
        var testFile = Path.Combine(_tempDir, "linetest.cs");
        File.WriteAllText(testFile, """
// Line 1
// Line 2
// Line 3
// Line 4
// Line 5
// Line 6
// Line 7
// Line 8 - Target
// Line 9 - Target
// Line 10 - Target
""");

        var result = await _tools.ReadFileAsync("linetest.cs", startLine: 8, endLine: 10);

        Assert.Contains("lines 8-10", result);
        Assert.Contains("Line 8 - Target", result);
        Assert.Contains("Line 10 - Target", result);
    }

    [Fact]
    [DisplayName("read_file—不存在的文件返回错误")]
    public async Task ReadFile_NonexistentFile_ReturnsError()
    {
        var result = await _tools.ReadFileAsync("nonexistent.cs");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    [DisplayName("read_file—工作区外路径被拒绝")]
    public async Task ReadFile_OutsideWorkspace_Denied()
    {
        var result = await _tools.ReadFileAsync(@"C:\Windows\win.ini");

        Assert.Contains("Error", result);
        Assert.Contains("超出工作区", result);
    }

    #endregion

    #region write_file

    [Fact]
    [DisplayName("write_file—新建文件成功")]
    public async Task WriteFile_NewFile_Succeeds()
    {
        var result = await _tools.WriteFileAsync("newfile.cs", "// new file content");

        Assert.Contains("已创建文件", result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "newfile.cs")));
        Assert.Equal("// new file content", File.ReadAllText(Path.Combine(_tempDir, "newfile.cs")));
    }

    [Fact]
    [DisplayName("write_file—覆盖已有文件")]
    public async Task WriteFile_Overwrite_Succeeds()
    {
        var result = await _tools.WriteFileAsync("test.cs", "// overwritten");

        Assert.Contains("已覆盖文件", result);
        Assert.Equal("// overwritten", File.ReadAllText(Path.Combine(_tempDir, "test.cs")));
    }

    [Fact]
    [DisplayName("write_file—工作区外写入被拒绝")]
    public async Task WriteFile_OutsideWorkspace_Denied()
    {
        var result = await _tools.WriteFileAsync(@"C:\Temp\outside.txt", "test");

        Assert.Contains("Error", result);
        Assert.Contains("超出工作区", result);
    }

    #endregion

    #region list_dir

    [Fact]
    [DisplayName("list_dir—根目录返回树形结构")]
    public async Task ListDir_Root_ReturnsTree()
    {
        var result = await _tools.ListDirAsync();

        Assert.Contains(_tempDir, result);
        Assert.Contains("test.cs", result);
        Assert.Contains("readme.md", result);
    }

    [Fact]
    [DisplayName("list_dir—maxDepth 限制生效")]
    public async Task ListDir_MaxDepth_RespectsLimit()
    {
        // depth=1 不应包含 SubDir 内的文件
        var result = await _tools.ListDirAsync(maxDepth: 1);

        Assert.Contains("SubDir/", result);
        Assert.DoesNotContain("nested.cs", result);
    }

    #endregion

    #region glob_search

    [Fact]
    [DisplayName("glob_search—找到 .cs 文件")]
    public async Task GlobSearch_CsFiles_FindsResults()
    {
        var result = await _tools.GlobSearchAsync("*.cs");

        Assert.Contains("test.cs", result);
        Assert.DoesNotContain("readme.md", result);
    }

    [Fact]
    [DisplayName("glob_search—** 递归匹配")]
    public async Task GlobSearch_Recursive_FindsNested()
    {
        var result = await _tools.GlobSearchAsync("**/*.cs");

        Assert.Contains("test.cs", result);
        Assert.Contains("nested.cs", result);
    }

    #endregion

    #region grep_search

    [Fact]
    [DisplayName("grep_search—已知模式找到结果")]
    public async Task GrepSearch_KnownPattern_FindsMatches()
    {
        var result = await _tools.GrepSearchAsync("TestClass");

        Assert.Contains("test.cs:", result);
        Assert.Contains("TestClass", result);
    }

    [Fact]
    [DisplayName("grep_search—maxResults 截断生效")]
    public async Task GrepSearch_MaxResults_Truncates()
    {
        // 使用宽泛模式，验证结果数限制
        var result = await _tools.GrepSearchAsync(".", maxResults: 1);

        Assert.Contains("找到 1 条匹配", result);
    }

    #endregion

    #region ask_user

    [Fact]
    [DisplayName("ask_user—返回非空回答（模拟需交互）")]
    public async Task AskUser_ReturnsAnswer()
    {
        // 注意：此测试在自动测试环境下无法真正读取控制台输入，
        // 但应能正常返回（即使 answer 为空字符串）
        var result = await _tools.AskUserAsync("测试问题");

        // 在无交互环境下，ReadLine 可能返回 null，此时返回 ""
        Assert.NotNull(result);
    }

    #endregion

    #region 边界

    [Fact]
    [DisplayName("工作区路径为空时不限制访问")]
    public async Task WorkspaceNull_NoRestriction()
    {
        var unboundTools = new CodingTools();
        var tempFile = Path.Combine(_tempDir, "test.cs");

        var result = await unboundTools.ReadFileAsync(tempFile);
        Assert.Contains("TestClass", result);
    }

    #endregion
}
