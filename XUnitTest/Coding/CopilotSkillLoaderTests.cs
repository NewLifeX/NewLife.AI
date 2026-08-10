using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using NewLife.AI.Coding;
using Xunit;

namespace XUnitTest.Coding;

/// <summary>Copilot 技能加载器（CopilotSkillLoader）单元测试。覆盖 frontmatter 解析、glob 匹配（A-25）、关键词匹配与 Prompt 构建</summary>
[DisplayName("CopilotSkillLoader 单元测试")]
public class CopilotSkillLoaderTests : IDisposable
{
    private readonly String _tempDir;

    public CopilotSkillLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "copilot_loader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    private void WriteFile(String relativePath, String content)
    {
        var path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private void WriteSkill(String name, String description, String content)
    {
        WriteFile($".copilot/skills/{name}/SKILL.md", $"---\nname: {name}\ndescription: {description}\n---\n\n{content}");
    }

    // ── 加载与 frontmatter ───────────────────────────────────────────────────

    [Fact]
    [DisplayName("LoadAll—加载 .github/instructions 指令并剥离 frontmatter")]
    public void LoadAll_LoadsInstructions()
    {
        WriteFile(".github/instructions/dev.instructions.md", """
---
description: '研发流程指引'
applyTo: "**"
---

# 规则
- 必须写测试
""");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        Assert.Single(loader.Instructions);
        var instruction = loader.Instructions[0];
        Assert.Equal("dev", instruction.Name);
        Assert.Equal("**", instruction.ApplyTo);
        Assert.Contains("必须写测试", instruction.Content);
        Assert.DoesNotContain("description", instruction.Content);
    }

    [Fact]
    [DisplayName("LoadAll—加载用户 .copilot/skills 技能并剥离 frontmatter")]
    public void LoadAll_LoadsSkills()
    {
        WriteSkill("unit-testing-skill", "单元测试 集成测试", "# 测试规范\n- 必须写测试");

        var loader = new CopilotSkillLoader(_tempDir, _tempDir);
        loader.LoadAll();

        var skill = Assert.Single(loader.Skills.Where(s => s.Name == "unit-testing-skill"));
        Assert.Contains("必须写测试", skill.Content);
        Assert.DoesNotContain("description", skill.Content);
    }

    [Fact]
    [DisplayName("LoadAll—frontmatter 无结束标记时整文件作为内容")]
    public void LoadAll_MalformedFrontmatter_WholeContent()
    {
        WriteFile(".github/instructions/malformed.instructions.md", "---\n没有结束标记");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        Assert.Single(loader.Instructions);
        Assert.Contains("没有结束标记", loader.Instructions[0].Content);
    }

    [Fact]
    [DisplayName("LoadAll—加载 .github/agents Agent 定义并解析 tools")]
    public void LoadAll_LoadsAgents()
    {
        WriteFile(".github/agents/code-review.agent.md", """
---
name: code-review
description: 代码审查
tools: read_file, run_command
---

# 审查标准
- 检查命名
""");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        var agent = Assert.Single(loader.Agents);
        Assert.Equal("code-review", agent.Name);
        Assert.Equal(2, agent.Tools!.Length);
        Assert.Contains("read_file", agent.Tools);
        Assert.Contains("检查命名", agent.Content);
    }

    // ── glob 匹配（A-25 绝对路径）───────────────────────────────────────────

    [Fact]
    [DisplayName("MatchInstructions—applyTo ** 始终匹配")]
    public void MatchInstructions_ApplyAll_AlwaysMatches()
    {
        WriteFile(".github/instructions/global.instructions.md", "---\napplyTo: \"**\"\n---\n\n全局规则");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        Assert.Single(loader.MatchInstructions("C:/work/SomeFile.cs"));
    }

    [Fact]
    [DisplayName("MatchInstructions—Doc/** 匹配目录下任意绝对路径")]
    public void MatchInstructions_ApplyDoc_MatchesAbsolutePath()
    {
        WriteFile(".github/instructions/doc.instructions.md", "---\napplyTo: \"Doc/**\"\n---\n\n文档规则");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        Assert.Single(loader.MatchInstructions("C:/X/StarChat/Doc/架构设计.md"));
        Assert.Empty(loader.MatchInstructions("C:/X/StarChat/README.md"));
    }

    [Fact]
    [DisplayName("MatchInstructions—精确文件 glob 匹配")]
    public void MatchInstructions_ExactFile_Matches()
    {
        WriteFile(".github/instructions/api.instructions.md", "---\napplyTo: \"Doc/API.md\"\n---\n\nAPI 规则");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        Assert.Single(loader.MatchInstructions("C:/X/StarChat/Doc/API.md"));
        Assert.Empty(loader.MatchInstructions("C:/X/StarChat/Doc/Other.md"));
    }

    // ── 关键词匹配 ───────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("MatchSkills—名称命中时返回匹配技能")]
    public void MatchSkills_NameMatch()
    {
        WriteSkill("unit-testing-skill", "单元测试 集成测试", "内容");
        var loader = new CopilotSkillLoader(_tempDir, _tempDir);
        loader.LoadAll();

        var matches = loader.MatchSkills("请用 unit-testing-skill 帮我验证功能");

        Assert.Contains(matches, s => s.Name == "unit-testing-skill");
    }

    [Fact]
    [DisplayName("MatchSkills—描述关键词命中时返回匹配技能")]
    public void MatchSkills_DescriptionKeywordMatch()
    {
        WriteSkill("skill-b", "单元测试 回归测试 覆盖率", "测试规范");
        var loader = new CopilotSkillLoader(_tempDir, _tempDir);
        loader.LoadAll();

        var matches = loader.MatchSkills("帮我编写单元测试并提升覆盖率");

        Assert.Contains(matches, s => s.Name == "skill-b");
    }

    [Fact]
    [DisplayName("MatchSkills—无关键词命中的技能不返回")]
    public void MatchSkills_NoMatch_Excluded()
    {
        WriteSkill("skill-c", "量子物理 粒子加速", "内容");
        var loader = new CopilotSkillLoader(_tempDir, _tempDir);
        loader.LoadAll();

        var matches = loader.MatchSkills("帮我订个外卖");

        Assert.DoesNotContain(matches, s => s.Name == "skill-c");
    }

    // ── Prompt 构建 ──────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("BuildCodingStandardsPrompt—注入 applyTo ** 指令的规则段")]
    public void BuildCodingStandardsPrompt_IncludesGlobalRules()
    {
        WriteFile(".github/instructions/dev.instructions.md", """
---
description: '研发流程指引'
applyTo: "**"
---

# 概述
描述性内容

## 编码规范
- 必须写单元测试
- 禁止使用别名
""");

        var loader = new CopilotSkillLoader(_tempDir);
        loader.LoadAll();

        var prompt = loader.BuildCodingStandardsPrompt();

        Assert.Contains("编码规范", prompt);
        Assert.Contains("必须写单元测试", prompt);
        Assert.Contains("来源: dev", prompt);
    }
}
