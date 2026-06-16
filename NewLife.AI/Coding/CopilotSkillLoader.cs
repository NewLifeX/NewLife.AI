using System.Text;
using System.Text.RegularExpressions;

namespace NewLife.AI.Coding;

/// <summary>Copilot 技能与指令加载器。扫描工作区和用户全局目录中的 Copilot 技能、指令和 Agent 定义</summary>
/// <remarks>
/// 支持加载 YAML frontmatter 格式的 SKILL.md、.instructions.md、.agent.md 文件，
/// 并按关键词匹配和 applyTo glob 模式匹配将相关内容注入到 System Prompt 中。
/// </remarks>
public class CopilotSkillLoader
{
    #region 数据模型

    /// <summary>Copilot 指令定义（.instructions.md）</summary>
    public class CopilotInstruction
    {
        /// <summary>指令名称（文件名去扩展名）</summary>
        public String Name { get; set; } = null!;

        /// <summary>指令内容（去除 YAML frontmatter）</summary>
        public String Content { get; set; } = null!;

        /// <summary>applyTo glob 模式，如 "**" 或 "Doc/**"</summary>
        public String? ApplyTo { get; set; }

        /// <summary>来源文件路径</summary>
        public String SourcePath { get; set; } = null!;
    }

    /// <summary>Copilot 技能定义（SKILL.md）</summary>
    public class CopilotSkill
    {
        /// <summary>技能名称</summary>
        public String Name { get; set; } = null!;

        /// <summary>技能描述</summary>
        public String Description { get; set; } = null!;

        /// <summary>技能内容（去除 YAML frontmatter）</summary>
        public String Content { get; set; } = null!;

        /// <summary>参数提示</summary>
        public String? ArgumentHint { get; set; }

        /// <summary>来源目录路径</summary>
        public String SourcePath { get; set; } = null!;
    }

    /// <summary>Copilot Agent 定义（.agent.md）</summary>
    public class CopilotAgent
    {
        /// <summary>Agent 名称</summary>
        public String Name { get; set; } = null!;

        /// <summary>Agent 描述</summary>
        public String Description { get; set; } = null!;

        /// <summary>Agent 内容（去除 YAML frontmatter）</summary>
        public String Content { get; set; } = null!;

        /// <summary>允许的工具列表</summary>
        public String[]? Tools { get; set; }

        /// <summary>来源文件路径</summary>
        public String SourcePath { get; set; } = null!;
    }

    #endregion

    #region 属性

    /// <summary>工作区路径</summary>
    public String WorkspacePath { get; }

    /// <summary>用户配置文件路径（~/.copilot 或等价目录）</summary>
    public String? UserProfilePath { get; }

    /// <summary>VS Code 用户 prompts 目录</summary>
    public String? VSCodePromptsPath { get; }

    /// <summary>已加载的指令列表</summary>
    public IReadOnlyList<CopilotInstruction> Instructions => _instructions.AsReadOnly();

    /// <summary>已加载的技能列表</summary>
    public IReadOnlyList<CopilotSkill> Skills => _skills.AsReadOnly();

    /// <summary>已加载的 Agent 列表</summary>
    public IReadOnlyList<CopilotAgent> Agents => _agents.AsReadOnly();

    private readonly List<CopilotInstruction> _instructions = [];
    private readonly List<CopilotSkill> _skills = [];
    private readonly List<CopilotAgent> _agents = [];

    #endregion

    #region 构造

    /// <summary>初始化技能加载器</summary>
    /// <param name="workspacePath">工作区路径</param>
    /// <param name="userProfilePath">用户目录路径（如 C:\Users\Stone），用于定位 .copilot/skills 和 AppData 目录</param>
    public CopilotSkillLoader(String workspacePath, String? userProfilePath = null)
    {
        WorkspacePath = workspacePath ?? throw new ArgumentNullException(nameof(workspacePath));
        UserProfilePath = userProfilePath;

        if (!String.IsNullOrEmpty(userProfilePath))
        {
            // VS Code 用户 prompts 目录
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            VSCodePromptsPath = Path.Combine(appData, "Code", "User", "prompts");
        }
    }

    #endregion

    #region 加载

    /// <summary>加载全部来源的技能、指令和 Agent</summary>
    public void LoadAll()
    {
        _instructions.Clear();
        _skills.Clear();
        _agents.Clear();

        // 1. 加载工作区 .github/instructions/
        var workspaceInstructionsDir = Path.Combine(WorkspacePath, ".github", "instructions");
        LoadInstructionsFrom(workspaceInstructionsDir);

        // 2. 加载工作区 .github/agents/ （如果有）
        var workspaceAgentsDir = Path.Combine(WorkspacePath, ".github", "agents");
        LoadAgentsFrom(workspaceAgentsDir);

        // 3. 加载用户全局 VS Code prompts
        if (!String.IsNullOrEmpty(VSCodePromptsPath))
        {
            LoadInstructionsFrom(VSCodePromptsPath);
            LoadAgentsFrom(VSCodePromptsPath);
            LoadSkillsFrom(Path.Combine(VSCodePromptsPath, "skills"));
        }

        // 4. 加载用户 .copilot/skills/
        if (!String.IsNullOrEmpty(UserProfilePath))
        {
            var userSkillsDir = Path.Combine(UserProfilePath, ".copilot", "skills");
            LoadSkillsFrom(userSkillsDir);
        }
    }

    private void LoadInstructionsFrom(String dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.instructions.md"))
        {
            var parsed = ParseFrontmatter(file);
            if (parsed == null) continue;

            _instructions.Add(new CopilotInstruction
            {
                Name = Path.GetFileNameWithoutExtension(file).Replace(".instructions", ""),
                Content = parsed.Content,
                ApplyTo = parsed.GetValue("applyTo"),
                SourcePath = file,
            });
        }
    }

    private void LoadSkillsFrom(String dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var skillFile = Path.Combine(subDir, "SKILL.md");
            if (!File.Exists(skillFile)) continue;

            var parsed = ParseFrontmatter(skillFile);
            if (parsed == null) continue;

            _skills.Add(new CopilotSkill
            {
                Name = parsed.GetValue("name") ?? Path.GetFileName(subDir),
                Description = parsed.GetValue("description") ?? "",
                Content = parsed.Content,
                ArgumentHint = parsed.GetValue("argument-hint"),
                SourcePath = subDir,
            });
        }
    }

    private void LoadAgentsFrom(String dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.agent.md"))
        {
            var parsed = ParseFrontmatter(file);
            if (parsed == null) continue;

            var toolsStr = parsed.GetValue("tools");
            String[]? tools = null;
            if (!String.IsNullOrWhiteSpace(toolsStr))
            {
                var ts = new[] { ',', ' ' };
                tools = toolsStr.Split(ts, StringSplitOptions.RemoveEmptyEntries);
            }

            _agents.Add(new CopilotAgent
            {
                Name = parsed.GetValue("name") ?? Path.GetFileNameWithoutExtension(file).Replace(".agent", ""),
                Description = parsed.GetValue("description") ?? "",
                Content = parsed.Content,
                Tools = tools,
                SourcePath = file,
            });
        }
    }

    #endregion

    #region 匹配

    /// <summary>按用户需求关键词匹配相关技能</summary>
    /// <param name="requirement">用户需求文本</param>
    /// <returns>匹配的技能列表（按相关度排序）</returns>
    public IReadOnlyList<CopilotSkill> MatchSkills(String requirement)
    {
        if (String.IsNullOrWhiteSpace(requirement) || _skills.Count == 0)
            return [];

        var scored = new List<(CopilotSkill Skill, Int32 Score)>();

        foreach (var skill in _skills)
        {
            var score = CalculateMatchScore(requirement, skill.Name, skill.Description);
            if (score > 0) scored.Add((skill, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Select(s => s.Skill)
            .Take(5)
            .ToList();
    }

    /// <summary>按文件路径匹配适用的指令</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>匹配的指令列表</returns>
    public IReadOnlyList<CopilotInstruction> MatchInstructions(String filePath)
    {
        if (String.IsNullOrWhiteSpace(filePath) || _instructions.Count == 0)
            return [];

        var result = new List<CopilotInstruction>();

        foreach (var instruction in _instructions)
        {
            // applyTo: "**" 的指令始终匹配
            if (instruction.ApplyTo == "**")
            {
                result.Add(instruction);
                continue;
            }

            // 具体 glob 模式匹配
            if (!String.IsNullOrEmpty(instruction.ApplyTo) && MatchGlob(filePath, instruction.ApplyTo))
                result.Add(instruction);
        }

        return result;
    }

    private static Int32 CalculateMatchScore(String text, String name, String description)
    {
        var score = 0;
        var lowerText = text.ToLowerInvariant();

        // 名称直接命中
        if (lowerText.Contains(name.ToLowerInvariant())) score += 10;

        // 描述关键词匹配
        var seps = new[] { ' ', ',', '.', ';', '，', '。', '；' };
        var keywords = description.ToLowerInvariant().Split(seps, StringSplitOptions.RemoveEmptyEntries);
        foreach (var keyword in keywords)
        {
            if (keyword.Length < 3) continue;
            if (lowerText.Contains(keyword)) score += 2;
        }

        return score;
    }

    private static Boolean MatchGlob(String filePath, String globPattern)
    {
        // 简单 glob 匹配：* 匹配任意字符，** 匹配任意路径段
        if (globPattern == "*" || globPattern == "**") return true;

        // 将 glob 转换为正则
        var regexPattern = "^" + Regex.Escape(globPattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/\\\\]*")
            .Replace(@"\?", "[^/\\\\]") + "$";

        try
        {
            return Regex.IsMatch(filePath, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Prompt 构建

    /// <summary>构建编码规范注入文本。从已加载的指令和技能中提取编码规范内容</summary>
    /// <returns>编码规范文本，可直接追加到 System Prompt 末尾</returns>
    public String BuildCodingStandardsPrompt()
    {
        var sb = new StringBuilder();

        // 1. 注入 applyTo: "**" 的全局指令（编码基线）
        var globalInstructions = _instructions.Where(i => i.ApplyTo == "**").ToList();
        if (globalInstructions.Count > 0)
        {
            sb.AppendLine("## 编码规范（来自项目配置）");
            sb.AppendLine();
            foreach (var instruction in globalInstructions)
            {
                // 只取核心规范部分，避免注入整个指令文件（太长）
                var excerpt = ExtractKeyRules(instruction.Content);
                if (!String.IsNullOrWhiteSpace(excerpt))
                {
                    sb.AppendLine($"<!-- 来源: {instruction.Name} -->");
                    sb.AppendLine(excerpt);
                    sb.AppendLine();
                }
            }
        }

        // 2. 注入匹配的 Agent 审查清单（如 code-review agent）
        if (_agents.Count > 0)
        {
            sb.AppendLine("## 审查标准（来自 Agent 定义）");
            sb.AppendLine();
            foreach (var agent in _agents)
            {
                var excerpt = ExtractKeyRules(agent.Content);
                if (!String.IsNullOrWhiteSpace(excerpt))
                {
                    sb.AppendLine($"<!-- Agent: {agent.Name} -->");
                    sb.AppendLine(excerpt);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>从完整指令内容中提取关键规则（跳过纯描述性文字和示例）</summary>
    private static String ExtractKeyRules(String content)
    {
        if (String.IsNullOrWhiteSpace(content)) return "";

        var ls = new[] { '\n', '\r' };
        var lines = content.Split(ls, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        var inRules = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 遇到规则性内容开始
            if (trimmed.StartsWith("##") && (trimmed.Contains("规范") || trimmed.Contains("标准") || trimmed.Contains("约定") || trimmed.Contains("规则") || trimmed.Contains("禁止")))
            {
                inRules = true;
                sb.AppendLine(trimmed);
                continue;
            }

            // 遇到其他章节标题，停止提取
            if (trimmed.StartsWith("##") && inRules && !trimmed.Contains("规范") && !trimmed.Contains("标准") && !trimmed.Contains("约定"))
            {
                inRules = false;
                continue;
            }

            if (inRules && trimmed.Length > 0)
            {
                sb.AppendLine(trimmed);
            }
        }

        // 如果没有找到明确规则段，返回内容的前 50 行
        if (sb.Length == 0)
        {
            var maxLines = Math.Min(lines.Length, 50);
            for (var i = 0; i < maxLines; i++)
            {
                sb.AppendLine(lines[i]);
            }
        }

        return sb.ToString();
    }

    #endregion

    #region YAML Frontmatter 解析

    /// <summary>解析 Markdown 文件的 YAML frontmatter</summary>
    /// <returns>解析结果，包含键值对和去除 frontmatter 后的内容</returns>
    private static FrontmatterResult? ParseFrontmatter(String filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var text = File.ReadAllText(filePath);
            var result = new FrontmatterResult();

            // 查找 YAML frontmatter: 以 --- 开头和结尾
            if (text.StartsWith("---"))
            {
                var endIndex = text.IndexOf("---", 3);
                if (endIndex > 3)
                {
                    var frontmatter = text[3..endIndex].Trim();
                    ParseYamlLines(frontmatter, result);

                    // 内容为 frontmatter 之后的部分
                    result.Content = text[(endIndex + 3)..].Trim();
                }
                else
                {
                    result.Content = text;
                }
            }
            else
            {
                result.Content = text;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static void ParseYamlLines(String yaml, FrontmatterResult result)
    {
        var ys = new[] { '\n', '\r' };
        var lines2 = yaml.Split(ys, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines2)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            // 去除引号
            if (value.Length >= 2 && ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'"))))
                value = value[1..^1];

            result.SetValue(key, value);
        }
    }

    private class FrontmatterResult
    {
        private readonly Dictionary<String, String> _values = new(StringComparer.OrdinalIgnoreCase);

        public String Content { get; set; } = "";

        public String? GetValue(String key) => _values.TryGetValue(key, out var v) ? v : null;

        public void SetValue(String key, String value) => _values[key] = value;
    }

    #endregion
}
