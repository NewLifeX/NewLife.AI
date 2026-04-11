using System.Text;
using System.Text.RegularExpressions;
using NewLife.ChatAI.Entity;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>技能服务。提供技能查询、使用记录和系统提示词构建</summary>
/// <remarks>实例化技能服务</remarks>
/// <param name="log">日志</param>
public class SkillService(ILog log)
{
    /// <summary>@引用最大递归深度</summary>
    private const Int32 MaxReferenceDepth = 3;

    #region 查询
    /// <summary>获取SkillBar展示列表。最近使用的技能 + 系统技能，去重后最多返回指定数量</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="maxCount">最大返回数量</param>
    /// <returns></returns>
    public IList<Skill> GetSkillBarList(Int32 userId, Int32 maxCount = 8)
    {
        var result = new List<Skill>();
        var addedIds = new HashSet<Int32>();

        // 最近使用的技能，按最后使用时间倒序
        var recentSkillIds = GetRecentSkillIds(userId);

        foreach (var skillId in recentSkillIds)
        {
            if (result.Count >= maxCount) break;
            var skill = GetSkillById(skillId);
            if (skill != null && skill.Enable && addedIds.Add(skill.Id))
                result.Add(skill);
        }

        // 补充系统技能（按排序倒序）
        if (result.Count < maxCount)
        {
            var systemSkills = GetSystemSkills();
            foreach (var skill in systemSkills)
            {
                if (result.Count >= maxCount) break;
                if (addedIds.Add(skill.Id))
                    result.Add(skill);
            }
        }

        return result;
    }

    /// <summary>获取全部启用的技能列表</summary>
    /// <param name="category">分类筛选（可选）</param>
    /// <returns></returns>
    public IList<Skill> GetAllSkills(String? category = null)
    {
        if (!String.IsNullOrEmpty(category))
            return Skill.FindAllByCategory(category).Where(e => e.Enable).OrderByDescending(e => e.Sort).ToList();

        return Skill.FindAllEnabled();
    }

    /// <summary>获取@提及下拉列表的技能。按用户最近使用优先、其余按Sort降序，支持关键词模糊过滤</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="keyword">搜索关键词（可选），按Code/Name模糊匹配</param>
    /// <param name="maxCount">最大返回数量，默认20</param>
    /// <returns></returns>
    public IList<Skill> GetMentionSkills(Int32 userId, String? keyword = null, Int32 maxCount = 20)
    {
        // 获取所有启用的技能
        var allSkills = GetAllSkills();

        // 关键词过滤
        if (!String.IsNullOrEmpty(keyword))
            allSkills = allSkills.Where(e => e.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) || e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        // 构建用户最近使用的技能ID排序字典（ID -> 排序权重，越大越靠前）
        var recentSkillIds = GetRecentSkillIds(userId);
        var recentOrder = new Dictionary<Int32, Int32>();
        for (var i = 0; i < recentSkillIds.Count; i++)
        {
            recentOrder[recentSkillIds[i]] = recentSkillIds.Count - i;
        }

        // 排序：用户最近使用的排前面（按权重倒序），其余按Sort降序
        var result = allSkills
            .OrderByDescending(e => recentOrder.TryGetValue(e.Id, out var w) ? w : 0)
            .ThenByDescending(e => e.Sort)
            .Take(maxCount)
            .ToList();

        return result;
    }

    /// <summary>获取全部分类列表</summary>
    /// <returns></returns>
    public IDictionary<String, String> GetCategories() => Skill.GetCategoryList();
    #endregion

    #region 使用记录
    /// <summary>记录技能使用。读取用户参数配置，将当前技能插入最前并保留最近3个</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="skillId">技能编号</param>
    public void RecordUsage(Int32 userId, Int32 skillId)
    {
        if (userId <= 0 || skillId <= 0) return;

        var p = XCode.Membership.Parameter.GetOrAdd(userId, "ChatAI", "RecentSkills");
        var ids = p.Value.IsNullOrEmpty() ? new List<Int32>() : p.Value.Split(',').Select(e => e.ToInt()).Where(id => id > 0).ToList();

        // 移除已有记录，插入最前面
        ids.Remove(skillId);
        ids.Insert(0, skillId);

        // 保留最近3个
        if (ids.Count > 3) ids = ids.Take(3).ToList();

        p.Value = ids.Join(",");
        p.Save();
    }
    #endregion

    #region 系统提示词构建
    /// <summary>构建技能系统提示词。按优先级拼接：系统技能 → 会话激活技能 → 消息@引用技能</summary>
    /// <param name="conversationSkillId">会话当前激活的技能编号</param>
    /// <param name="messageContent">用户消息内容（用于解析@引用）</param>
    /// <param name="selectedTools">用于收集消息中 @ToolName 引用的工具名称集合；为 null 时就是不收集</param>
    /// <param name="skillCollector">用于收集本轮实际注入的技能名称（Code/Name 格式）；为 null 时不收集</param>
    /// <returns>拼接后的技能提示词，无技能时返回 null</returns>
    public String? BuildSkillPrompt(Int32 conversationSkillId, String? messageContent, ISet<String>? selectedTools = null, ICollection<String>? skillCollector = null)
    {
        var parts = new List<String>();

        // 1. 系统内置技能
        var systemSkills = GetSystemSkills();
        foreach (var skill in systemSkills)
        {
            if (!skill.Content.IsNullOrWhiteSpace())
            {
                var resolved = ResolveReferences(skill.Content, 0, []);
                parts.Add(resolved);
                skillCollector?.Add($"{skill.Code}/{skill.Name}");
            }
        }

        // 2. 会话激活的技能
        if (conversationSkillId > 0)
        {
            var skill = GetSkillById(conversationSkillId);
            if (skill != null && skill.Enable && !skill.Content.IsNullOrWhiteSpace())
            {
                var resolved = ResolveReferences(skill.Content, 0, []);
                parts.Add(resolved);
                skillCollector?.Add($"{skill.Code}/{skill.Name}");
            }
        }

        // 3. 消息中的 @技能名/@工具名 引用
        if (!messageContent.IsNullOrEmpty())
        {
            var referencedParts = ResolveMessageReferences(messageContent, selectedTools, skillCollector);
            if (referencedParts != null)
                parts.AddRange(referencedParts);
        }

        if (parts.Count == 0) return null;

        return String.Join("\n\n", parts);
    }

    /// <summary>解析消息中的 @技能名/@工具名 引用。工具优先匹配加入 selectedTools，无工具匹配时再查找技能获取提示词内容</summary>
    /// <param name="content">消息内容</param>
    /// <param name="selectedTools">收集工具引用的集合；为 null 时不收集</param>
    /// <param name="skillCollector">收集技能名称（Code/Name 格式）的列表；为 null 时不收集</param>
    /// <returns></returns>
    private List<String>? ResolveMessageReferences(String content, ISet<String>? selectedTools = null, ICollection<String>? skillCollector = null)
    {
        // 匹配 @技能名 格式，技能名可以是中英文数字下划线
        var matches = Regex.Matches(content, @"@([\w\u4e00-\u9fff]+)");
        if (matches.Count == 0) return null;

        var parts = new List<String>();
        var resolved = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var skillName = match.Groups[1].Value;
            if (!resolved.Add(skillName)) continue;

            // 优先匹配内置工具（按 Name 或 DisplayName）
            var tool = NativeTool.FindByNameOrDisplayName(skillName);
            if (tool is { Enable: true })
            {
                selectedTools?.Add(tool.Name);
                continue;
            }

            // 未匹配工具时，尝试按名称查找技能
            var skill = FindSkillByName(skillName);
            if (skill != null && skill.Enable && !String.IsNullOrWhiteSpace(skill.Content))
            {
                var content2 = ResolveReferences(skill.Content, 0, []);
                parts.Add(content2);
                skillCollector?.Add($"{skill.Code}/{skill.Name}");
            }
        }

        return parts.Count > 0 ? parts : null;
    }

    /// <summary>递归解析技能内容中的 @引用（最多3层，检测循环引用）</summary>
    /// <param name="content">技能内容文本</param>
    /// <param name="depth">当前递归深度</param>
    /// <param name="visited">已访问的技能名集合（用于循环检测）</param>
    /// <returns>展开后的内容</returns>
    private String ResolveReferences(String content, Int32 depth, HashSet<String> visited)
    {
        if (depth >= MaxReferenceDepth) return content;

        var matches = Regex.Matches(content, @"@([\w\u4e00-\u9fff]+)");
        if (matches.Count == 0) return content;

        var sb = new StringBuilder(content);
        // 倒序替换以保持偏移量正确
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var skillName = match.Groups[1].Value;

            // 循环引用检测
            if (visited.Contains(skillName))
            {
                log?.Warn("技能@引用循环检测: {0}", skillName);
                continue;
            }

            var skill = FindSkillByName(skillName);
            if (skill != null && skill.Enable && !String.IsNullOrWhiteSpace(skill.Content))
            {
                var childVisited = new HashSet<String>(visited, StringComparer.OrdinalIgnoreCase) { skillName };
                var resolved = ResolveReferences(skill.Content, depth + 1, childVisited);
                sb.Remove(match.Index, match.Length);
                sb.Insert(match.Index, resolved);
            }
        }

        return sb.ToString();
    }

    #endregion

    #region 数据访问（可被测试覆盖）
    /// <summary>获取所有启用的系统技能，按 Sort 降序</summary>
    /// <returns></returns>
    protected virtual IList<Skill> GetSystemSkills() => Skill.GetSystemSkills();

    /// <summary>根据编号获取技能</summary>
    /// <param name="id">技能编号</param>
    /// <returns></returns>
    protected virtual Skill? GetSkillById(Int32 id) => Skill.FindById(id);

    /// <summary>获取用户最近使用的技能ID列表，按最近使用排序</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    protected virtual IList<Int32> GetRecentSkillIds(Int32 userId)
    {
        var p = XCode.Membership.Parameter.GetOrAdd(userId, "ChatAI", "RecentSkills");
        if (p.Value.IsNullOrEmpty()) return [];

        return p.Value.Split(',').Select(e => e.ToInt()).Where(id => id > 0).ToList();
    }

    /// <summary>按名称或编码查找技能</summary>
    /// <param name="name">技能名称或编码</param>
    /// <returns></returns>
    protected virtual Skill? FindSkillByName(String name)
    {
        // 先按编码精确匹配
        var skill = Skill.FindByCode(name);
        if (skill != null) return skill;

        // 再按名称匹配（实体缓存）
        if (Skill.Meta.Session.Count < 1000)
            return Skill.Meta.Cache.Find(e => e.Name == name);

        return Skill.Find(Skill._.Name == name);
    }
    #endregion
}
