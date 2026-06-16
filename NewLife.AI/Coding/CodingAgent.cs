using NewLife.AI.Clients;
using NewLife.AI.Coding.Models;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using System.Text;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Coding;

/// <summary>编程智能体。实现 ACP（自主编码管道）三阶段编排：Plan → Implement → Review</summary>
/// <remarks>
/// 自动加载 Copilot 技能和指令，将编码规范注入到各阶段的 System Prompt 中。
/// 支持编译-修复闭环和审查-修复闭环。
/// 使用示例：
/// <code>
/// var baseClient = new NewLifeAIChatClient(apiKey, "qwen3.5-coder", "http://localhost:5080");
/// var tools = new CodingTools(@"D:\MyProject");
/// var agent = new CodingAgent(baseClient, tools, @"D:\MyProject");
/// var report = await agent.RunAsync("给 ChatController 新增 GetById 接口");
/// </code>
/// </remarks>
public class CodingAgent
{
    #region 属性

    /// <summary>底层模型客户端</summary>
    public IChatClient BaseClient { get; }

    /// <summary>编码工具集</summary>
    public CodingTools Tools { get; }

    /// <summary>工作区路径</summary>
    public String WorkspacePath { get; }

    /// <summary>技能加载器</summary>
    public CopilotSkillLoader SkillLoader { get; }

    /// <summary>规划阶段 System Prompt。默认提供资深架构师角色提示词，可外部读取或修改</summary>
    public String PlanSystemPrompt { get; set; } = _defaultPlanPrompt;

    /// <summary>实现阶段 System Prompt。默认提供高级 C# 开发角色提示词（含编码规范），可外部读取或修改</summary>
    public String ImplementSystemPrompt { get; set; } = _defaultImplementPrompt;

    /// <summary>审查阶段 System Prompt。默认提供代码审查员角色提示词（含审查清单），可外部读取或修改</summary>
    public String ReviewSystemPrompt { get; set; } = _defaultReviewPrompt;

    /// <summary>编译修复最大重试次数。默认 3</summary>
    public Int32 MaxFixRetries { get; set; } = 3;

    /// <summary>审查修复最大重试次数。默认 2</summary>
    public Int32 MaxReviewRetries { get; set; } = 2;

    /// <summary>工具调用最大轮次。默认 10</summary>
    public Int32 MaxToolIterations { get; set; } = 10;

    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    #endregion

    #region 事件

    /// <summary>阶段变化事件。参数：(阶段名, 消息)</summary>
    public event Action<String, String>? OnPhaseChanged;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = NewLife.Log.Logger.Null;

    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; }

    #endregion

    #region 构造

    /// <summary>初始化编程智能体</summary>
    /// <param name="baseClient">底层模型客户端</param>
    /// <param name="tools">编码工具集</param>
    /// <param name="workspacePath">工作区路径</param>
    /// <param name="userProfilePath">用户目录路径（用于加载 Copilot 技能），默认自动检测</param>
    public CodingAgent(IChatClient baseClient, CodingTools tools, String workspacePath, String? userProfilePath = null)
    {
        BaseClient = baseClient ?? throw new ArgumentNullException(nameof(baseClient));
        Tools = tools ?? throw new ArgumentNullException(nameof(tools));
        WorkspacePath = workspacePath ?? throw new ArgumentNullException(nameof(workspacePath));

        // 确保工具的工作区路径与智能体一致
        tools.WorkspacePath ??= workspacePath;

        // 继承基类日志和追踪
        var log = (baseClient as ILogFeature)?.Log;
        var tracer = (baseClient as ITracerFeature)?.Tracer;
        if (log != null) Log = log;
        if (tracer != null) Tracer = tracer;

        // 加载 Copilot 技能
        SkillLoader = new CopilotSkillLoader(workspacePath, userProfilePath ?? GetDefaultUserProfilePath());
        SkillLoader.LoadAll();
    }

    #endregion

    #region 核心方法

    /// <summary>执行完整的 ACP 管道</summary>
    /// <param name="requirement">用户需求描述</param>
    /// <returns>编码报告</returns>
    public async Task<CodingReport> RunAsync(String requirement)
    {
        if (String.IsNullOrWhiteSpace(requirement))
            throw new ArgumentNullException(nameof(requirement));

        var report = new CodingReport { Requirement = requirement };

        try
        {
            using var span = Tracer?.NewSpan("ai:CodingAgent.Run");

            // Phase 1: Plan
            EmitPhase("Plan", "开始分析需求并拆解任务……");
            var plan = await PlanAsync(requirement);
            report.Plan = plan;
            WriteLog("规划完成：{0} 个任务", plan.Tasks.Count);

            if (plan.Tasks.Count == 0)
            {
                Log?.Warn("规划阶段未产出任务，管道终止");
                return report;
            }

            // Phase 2-3: 逐任务 Implement + Review
            foreach (var task in plan.Tasks)
            {
                report.TaskResults.Add(await ExecuteTaskAsync(task));
            }

            EmitPhase("Done", "ACP 管道执行完成");
        }
        catch (Exception ex)
        {
            Log?.Error("管道执行异常: {0}", ex.Message);
            report.Error = ex.Message;
        }

        return report;
    }

    /// <summary>规划阶段：分析需求，拆解为编码任务</summary>
    /// <param name="requirement">用户需求</param>
    /// <returns>编码规划</returns>
    public async Task<CodingPlan> PlanAsync(String requirement)
    {
        var prompt = GetEffectivePlanPrompt();
        var toolClient = CreatePhaseClient(PlanToolNames, prompt);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = prompt },
            new() { Role = "user", Content = BuildPlanUserMessage(requirement) },
        };

        try
        {
            var response = toolClient.StreamChatAsync(messages);
            var text = await ReadStreamResponseAsync(response);
            var plan = ParsePlanFromResponse(text);

            // 补充影响文件分析
            if (plan.Tasks.Count > 0)
            {
                foreach (var task in plan.Tasks)
                {
                    task.FilesToModify ??= [];
                }
            }

            return plan;
        }
        catch (Exception ex)
        {
            WriteLog("规划阶段失败: {0}", ex.Message);
            return new CodingPlan { Requirement = requirement, Summary = $"规划失败: {ex.Message}" };
        }
    }

    /// <summary>实现阶段：执行单个编码任务</summary>
    /// <param name="task">编码任务</param>
    /// <returns>编码结果摘要</returns>
    public async Task<String> ImplementAsync(CodingTask task)
    {
        var prompt = GetEffectiveImplementPrompt(task);
        var toolClient = CreatePhaseClient(ImplementToolNames, prompt);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = prompt },
            new() { Role = "user", Content = BuildImplementUserMessage(task) },
        };

        // 编译-修复闭环
        for (var attempt = 0; attempt <= MaxFixRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    WriteLog("编译修复第 {0} 次尝试……", attempt);
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = "上一步编译失败，请分析错误并修复代码，然后重新编译验证。",
                    });
                }

                var response = toolClient.StreamChatAsync(messages);
                var text = await ReadStreamResponseAsync(response);
                return text ?? "实现完成（无详细输出）";
            }
            catch (Exception ex)
            {
                if (attempt >= MaxFixRetries)
                {
                    WriteLog("实现阶段失败（已达最大重试 {0} 次）: {1}", MaxFixRetries, ex.Message);
                    return $"实现失败: {ex.Message}";
                }
                WriteLog("实现阶段异常，准备重试: {0}", ex.Message);
            }
        }

        return "实现完成";
    }

    /// <summary>审查阶段：按检查清单审查代码</summary>
    /// <param name="code">代码变更摘要</param>
    /// <param name="task">对应的编码任务</param>
    /// <returns>审查结果</returns>
    public async Task<ReviewResult> ReviewAsync(String code, CodingTask task)
    {
        var prompt = GetEffectiveReviewPrompt();
        var toolClient = CreatePhaseClient(ReviewToolNames, prompt);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = prompt },
            new() { Role = "user", Content = BuildReviewUserMessage(code, task) },
        };

        try
        {
            var response = toolClient.StreamChatAsync(messages);
            var text = await ReadStreamResponseAsync(response);
            return ParseReviewFromResponse(text) ?? new ReviewResult
            {
                Passed = true,
                Summary = "审查完成（无法解析 JSON，默认通过）",
            };
        }
        catch (Exception ex)
        {
            WriteLog("审查阶段失败: {0}", ex.Message);
            return new ReviewResult { Passed = false, Summary = $"审查异常: {ex.Message}" };
        }
    }

    /// <summary>根据审查问题修复代码</summary>
    /// <param name="code">原代码变更摘要</param>
    /// <param name="issues">审查问题列表</param>
    /// <returns>修复后的代码</returns>
    public async Task<String> FixAsync(String code, IList<ReviewIssue> issues)
    {
        var prompt = GetEffectiveImplementPrompt(null);
        var toolClient = CreatePhaseClient(ImplementToolNames, prompt);

        var issuesText = String.Join("\n", issues.Select(i => $"- [{i.Severity}] {i.Description}" + (i.Suggestion != null ? $" → 建议: {i.Suggestion}" : "")));

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = prompt },
            new() { Role = "user", Content = $"以下代码审查未通过，请修复以下问题：\n\n{issuesText}\n\n原代码变更：\n{code}" },
        };

        try
        {
            var response = toolClient.StreamChatAsync(messages);
            var text = await ReadStreamResponseAsync(response);
            return text ?? "修复完成";
        }
        catch (Exception ex)
        {
            WriteLog("修复失败: {0}", ex.Message);
            return $"修复失败: {ex.Message}";
        }
    }

    #endregion

    #region 内部方法

    private IChatClient CreatePhaseClient(ISet<String> toolNames, String systemPrompt)
    {
        var registry = new ToolRegistry();
        registry.AddTools(Tools);

        return new ChatClientBuilder(BaseClient)
            .UseTools(MaxToolIterations, 3000, toolNames, registry)
            .Build();
    }

    /// <summary>逐任务执行：Implement → Review → Fix 循环</summary>
    private async Task<TaskResult> ExecuteTaskAsync(CodingTask task)
    {
        var taskResult = new TaskResult { Task = task };
        task.Status = CodingTaskStatus.InProgress;

        try
        {
            using var span = Tracer?.NewSpan("ExecuteTask", task.Id);

            EmitPhase("Implement", $"执行任务: [{task.Id}] {task.Description}");

            // Implement
            var code = await ImplementAsync(task);
            taskResult.Code = code;

            // Review
            EmitPhase("Review", $"审查任务: [{task.Id}] {task.Description}");
            var review = await ReviewAsync(code, task);

            var reviewRetries = 0;
            while (!review.Passed && reviewRetries < MaxReviewRetries)
            {
                reviewRetries++;
                WriteLog("审查不通过，第 {0} 次修复……", reviewRetries);
                code = await FixAsync(code, review.Issues);
                review = await ReviewAsync(code, task);
            }

            taskResult.Review = review;
            taskResult.Passed = review.Passed;
            task.Status = review.Passed ? CodingTaskStatus.Completed : CodingTaskStatus.Failed;

            WriteLog(review.Passed ? "任务 [{0}] 通过审查" : "任务 [{0}] 未通过审查", task.Id);
        }
        catch (Exception ex)
        {
            task.Status = CodingTaskStatus.Failed;
            taskResult.Error = ex.Message;
            WriteLog("任务 [{0}] 执行异常: {1}", task.Id, ex.Message);
        }

        return taskResult;
    }

    #endregion

    #region Prompt 构建

    private String GetEffectivePlanPrompt() => PlanSystemPrompt;

    private String GetEffectiveImplementPrompt(CodingTask? task)
    {
        var sb = new StringBuilder();
        sb.Append(ImplementSystemPrompt);

        // 注入编码规范（从 Copilot 技能加载）
        var standards = SkillLoader.BuildCodingStandardsPrompt();
        if (!String.IsNullOrWhiteSpace(standards))
        {
            sb.AppendLine();
            sb.Append(standards);
        }

        // 按任务匹配相关技能
        if (task != null)
        {
            var matchedSkills = SkillLoader.MatchSkills(task.Description);
            if (matchedSkills.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 相关技能参考");
                foreach (var skill in matchedSkills)
                {
                    sb.AppendLine($"### {skill.Name}");
                    var excerpt = ExtractKeyRules(skill.Content);
                    if (!String.IsNullOrWhiteSpace(excerpt))
                        sb.AppendLine(excerpt);
                }
            }
        }

        return sb.ToString();
    }

    private String GetEffectiveReviewPrompt() => ReviewSystemPrompt;

    #endregion

    #region 默认提示词

    private static readonly String _defaultPlanPrompt = """
你是一名资深系统架构师，负责分析用户需求并拆解为可执行的编码任务。

## 工作方式
1. 先用 list_dir 工具了解项目根目录结构
2. 用 glob_search 和 grep_search 探索相关代码文件
3. 分析需求影响范围，确定需要修改的文件
4. 将需求拆解为独立可执行的编码任务

## 输出格式
严格输出以下 JSON 格式（不要包含其他文字）：
```json
{
  "summary": "整体方案简述，100字以内",
  "tasks": [
    {
      "id": "F001",
      "description": "任务描述",
      "dependencies": [],
      "acceptanceCriteria": ["验收条件1", "验收条件2"],
      "filesToModify": ["相对路径/文件.cs"],
      "estimatedComplexity": "Low|Medium|High"
    }
  ],
  "affectedFiles": ["文件路径1", "文件路径2"]
}
```

## 注意事项
- 只做规划和代码探索，不修改任何文件
- 任务按依赖关系排序，无依赖的任务优先
- 每个任务应在 1-2 小时内可完成
- 标记任务复杂度：Low（简单修改）、Medium（新增/重构方法）、High（新增类/模块）
""";

    private static readonly String _defaultImplementPrompt = """
你是一名高级 C# 开发工程师，负责根据任务描述实现代码。

## 工作流程
1. 先用 read_file 理解需要修改的文件及其上下文
2. 用 grep_search 查找相关代码引用
3. 编写/修改代码，通过 write_file 写入
4. 用 run_command 执行 `dotnet build` 验证编译
5. 编译失败时分析错误并修复，最多重试 3 次
6. 完成所有修改后回复修改摘要

## 编码规范（必须遵守）
1. 类型名使用 .NET 正式名：String/Int32/Boolean/Int64/Double/Object
   禁止使用 C# 别名：string/int/bool/long/double/object
2. 命名空间使用 file-scoped namespace
3. 私有字段使用 _camelCase，参数/局部变量使用 camelCase
4. 公共成员使用 PascalCase
5. <summary> 必须同行闭合且 ≤30 汉字，超出内容放入 <remarks>
6. 每个参数必须有 <param>，有返回值必须有 <returns>
7. 循环体（for/foreach/while）必须保留花括号，即使单语句
8. 单行 if 不加花括号
9. using var 无花括号声明
10. 集合初始化优先使用 []
11. #region 顺序：属性→静态→构造→方法→辅助→日志
12. 含 ILog Log 和 WriteLog 时，日志 region 放类末尾
13. 禁止删除防御性注释（带说明的被注释代码）
14. 禁止将 String/Int32 改为 string/int

## 代码质量
- 避免热点路径使用反射/复杂 Linq
- 池化资源明确获取与归还
- 使用精准异常类型
- 对外异常不暴露内部实现细节
""";

    private static readonly String _defaultReviewPrompt = """
你是一名严格的代码审查员，按检查清单逐条审查代码变更。

## 审查清单
对每处修改，逐条检查：

### 命名
- [ ] 类型名是否使用 .NET 正式名（String/Int32/Boolean，非 string/int/bool）
- [ ] 私有字段是否使用 _camelCase
- [ ] 参数/局部变量是否使用 camelCase
- [ ] 公共成员是否使用 PascalCase

### 结构
- [ ] 命名空间是否为 file-scoped
- [ ] #region 顺序是否为：属性→静态→构造→方法→辅助→日志
- [ ] 日志 region 是否在类末尾

### 注释
- [ ] <summary> 是否同行闭合，是否 ≤30 汉字
- [ ] 参数是否都有 <param>
- [ ] 返回值是否有 <returns>

### 语法
- [ ] 循环体是否保留花括号
- [ ] 单行 if 是否无花括号
- [ ] using var 是否无花括号
- [ ] 集合初始化是否使用 []

### 安全与性能
- [ ] 是否存在资源泄漏（未释放的 Stream/IDisposable 等）
- [ ] 是否存在空引用风险
- [ ] 是否存在硬编码凭据
- [ ] 热点路径是否避免反射/复杂 Linq

### 兼容性
- [ ] 是否兼容目标框架（检查 .csproj 的 TargetFrameworks）
- [ ] 是否使用了高版本专属 BCL API

## 输出格式
严格输出以下 JSON 格式（不要包含其他文字）：
```json
{
  "passed": true,
  "summary": "审查摘要",
  "issues": [
    {
      "severity": "error|warning|suggestion",
      "file": "文件路径",
      "line": "行号",
      "description": "问题描述",
      "suggestion": "修复建议"
    }
  ]
}
```
""";

    #endregion

    #region 解析

    private static CodingPlan ParsePlanFromResponse(String? response)
    {
        if (String.IsNullOrWhiteSpace(response))
            return new CodingPlan();

        try
        {
            // 提取 JSON 块
            var json = ExtractJson(response);
            if (json == null) return new CodingPlan { Summary = response };

            return json.ToJsonEntity<CodingPlan>() ?? new CodingPlan();
        }
        catch
        {
            return new CodingPlan { Summary = response };
        }
    }

    private static ReviewResult? ParseReviewFromResponse(String? response)
    {
        if (String.IsNullOrWhiteSpace(response))
            return null;

        try
        {
            var json = ExtractJson(response);
            if (json == null) return null;

            return json.ToJsonEntity<ReviewResult>();
        }
        catch
        {
            return null;
        }
    }

    private static String? ExtractJson(String text)
    {
        // 查找 ```json ... ``` 代码块
        var startIndex = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            startIndex = text.IndexOf('\n', startIndex) + 1;
            var endIndex = text.IndexOf("```", startIndex);
            if (endIndex > startIndex)
                return text[startIndex..endIndex].Trim();
        }

        // 查找裸 JSON { ... }
        var braceStart = text.IndexOf('{');
        var braceEnd = text.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return text[braceStart..(braceEnd + 1)];

        return null;
    }

    #endregion

    #region 消息构建

    private String BuildPlanUserMessage(String requirement)
    {
        return $"请分析以下需求并生成编码计划：\n\n{requirement}";
    }

    private String BuildImplementUserMessage(CodingTask task)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任务");
        sb.AppendLine($"- ID: {task.Id}");
        sb.AppendLine($"- 描述: {task.Description}");

        if (task.AcceptanceCriteria.Count > 0)
        {
            sb.AppendLine("- 验收条件:");
            foreach (var ac in task.AcceptanceCriteria)
            {
                sb.AppendLine($"  - [ ] {ac}");
            }
        }

        if (task.FilesToModify is { Count: > 0 })
        {
            sb.AppendLine("- 涉及文件:");
            foreach (var file in task.FilesToModify)
            {
                sb.AppendLine($"  - {file}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("请按照工作流程实现上述任务。先理解相关代码上下文，再编写修改，最后编译验证。");

        return sb.ToString();
    }

    private String BuildReviewUserMessage(String code, CodingTask task)
    {
        return $"请审查以下任务 [{task.Id}] {task.Description} 的代码变更：\n\n{code}";
    }

    /// <summary>从流式响应中读取完整文本</summary>
    private static async Task<String?> ReadStreamResponseAsync(IAsyncEnumerable<IChatResponse> stream)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in stream)
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?.Content is String text && !String.IsNullOrEmpty(text))
                sb.Append(text);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    #endregion

    #region 工具集名称

    private static readonly HashSet<String> PlanToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "list_dir", "glob_search", "grep_search",
    };

    private static readonly HashSet<String> ImplementToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "write_file", "list_dir", "glob_search", "grep_search",
        "run_command", "get_errors", "ask_user",
    };

    private static readonly HashSet<String> ReviewToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "grep_search",
    };

    #endregion

    #region 辅助

    private void EmitPhase(String phase, String message)
    {
        OnPhaseChanged?.Invoke(phase, message);
    }

    /// <summary>获取默认用户配置目录</summary>
    private static String? GetDefaultUserProfilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !String.IsNullOrEmpty(home) ? home : null;
    }

    /// <summary>从文本中提取关键规则行（跳过纯描述性文字）</summary>
    private static String ExtractKeyRules(String content)
    {
        if (String.IsNullOrWhiteSpace(content)) return "";

        var lines = content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // 保留：标题、列表项、代码块标记、规则行
            if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("-") || trimmed.StartsWith("*")
                || trimmed.StartsWith("`") || trimmed.StartsWith(">") || trimmed.StartsWith("||")
                || trimmed.Contains("必须") || trimmed.Contains("禁止") || trimmed.Contains("✅") || trimmed.Contains("❌"))
            {
                sb.AppendLine(trimmed);
            }
        }

        return sb.ToString();
    }

    #endregion

    #region 日志

    /// <summary>写日志</summary>
    private void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);

    #endregion
}

/// <summary>编码报告。聚合 ACP 管道执行结果</summary>
public class CodingReport
{
    /// <summary>用户需求</summary>
    public String Requirement { get; set; } = null!;

    /// <summary>编码规划</summary>
    public CodingPlan? Plan { get; set; }

    /// <summary>各任务执行结果</summary>
    public IList<TaskResult> TaskResults { get; set; } = [];

    /// <summary>异常信息</summary>
    public String? Error { get; set; }

    /// <summary>是否全部通过</summary>
    public Boolean AllPassed => TaskResults.Count > 0 && TaskResults.All(t => t.Passed);
}

/// <summary>单任务执行结果</summary>
public class TaskResult
{
    /// <summary>任务定义</summary>
    public CodingTask Task { get; set; } = null!;

    /// <summary>实现产出的代码</summary>
    public String? Code { get; set; }

    /// <summary>审查结果</summary>
    public ReviewResult? Review { get; set; }

    /// <summary>是否通过审查</summary>
    public Boolean Passed { get; set; }

    /// <summary>异常信息</summary>
    public String? Error { get; set; }
}
