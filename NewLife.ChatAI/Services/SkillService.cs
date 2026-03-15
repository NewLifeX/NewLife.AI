using System.Text;
using System.Text.RegularExpressions;
using NewLife.ChatAI.Entity;
using NewLife.Log;
using XCode;

namespace NewLife.ChatAI.Services;

/// <summary>技能服务。提供技能查询、使用记录和系统提示词构建</summary>
public class SkillService
{
    private readonly ILog _log;

    /// <summary>@引用最大递归深度</summary>
    private const Int32 MaxReferenceDepth = 3;

    /// <summary>实例化技能服务</summary>
    /// <param name="log">日志</param>
    public SkillService(ILog log) => _log = log;

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
        var userSkills = GetUserSkills(userId);
        var recentSkills = userSkills
            .Where(e => e.LastUseTime > DateTime.MinValue)
            .OrderByDescending(e => e.LastUseTime)
            .ToList();

        foreach (var us in recentSkills)
        {
            if (result.Count >= maxCount) break;
            var skill = GetSkillById(us.SkillId);
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

        return Skill.FindAll(Skill._.Enable == true, Skill._.Sort.Desc() & Skill._.Id.Asc(), null, 0, 0);
    }

    /// <summary>获取全部分类列表</summary>
    /// <returns></returns>
    public IDictionary<String, String> GetCategories() => Skill.GetCategoryList();
    #endregion

    #region 使用记录
    /// <summary>记录技能使用。更新或创建 UserSkill 记录</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="skillId">技能编号</param>
    public void RecordUsage(Int32 userId, Int32 skillId)
    {
        if (userId <= 0 || skillId <= 0) return;

        var us = UserSkill.FindByUserIdAndSkillId(userId, skillId);
        if (us == null)
        {
            us = new UserSkill
            {
                UserId = userId,
                SkillId = skillId,
            };
        }

        us.LastUseTime = DateTime.Now;
        us.Save();
    }
    #endregion

    #region 系统提示词构建
    /// <summary>构建技能系统提示词。按优先级拼接：系统技能 → 会话激活技能 → 消息@引用技能</summary>
    /// <param name="conversationSkillId">会话当前激活的技能编号</param>
    /// <param name="messageContent">用户消息内容（用于解析@引用）</param>
    /// <returns>拼接后的技能提示词，无技能时返回 null</returns>
    public String? BuildSkillPrompt(Int32 conversationSkillId, String? messageContent)
    {
        var parts = new List<String>();

        // 1. 系统内置技能
        var systemSkills = GetSystemSkills();
        foreach (var skill in systemSkills)
        {
            if (!String.IsNullOrWhiteSpace(skill.Content))
            {
                var resolved = ResolveReferences(skill.Content, 0, []);
                parts.Add(resolved);
            }
        }

        // 2. 会话激活的技能
        if (conversationSkillId > 0)
        {
            var skill = GetSkillById(conversationSkillId);
            if (skill != null && skill.Enable && !String.IsNullOrWhiteSpace(skill.Content))
            {
                var resolved = ResolveReferences(skill.Content, 0, []);
                parts.Add(resolved);
            }
        }

        // 3. 消息中的 @技能名 引用
        if (!String.IsNullOrEmpty(messageContent))
        {
            var referencedParts = ResolveMessageReferences(messageContent);
            if (referencedParts != null)
                parts.AddRange(referencedParts);
        }

        if (parts.Count == 0) return null;

        return String.Join("\n\n", parts);
    }

    /// <summary>解析消息中的 @技能名 引用，返回对应技能内容列表</summary>
    /// <param name="content">消息内容</param>
    /// <returns></returns>
    private List<String>? ResolveMessageReferences(String content)
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

            // 按名称查找技能
            var skill = FindSkillByName(skillName);
            if (skill != null && skill.Enable && !String.IsNullOrWhiteSpace(skill.Content))
            {
                var content2 = ResolveReferences(skill.Content, 0, []);
                parts.Add(content2);
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
                _log?.Warn("技能@引用循环检测: {0}", skillName);
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
    protected virtual IList<Skill> GetSystemSkills() =>
        Skill.FindAll(Skill._.IsSystem == true & Skill._.Enable == true, Skill._.Sort.Desc(), null, 0, 0);

    /// <summary>根据编号获取技能</summary>
    /// <param name="id">技能编号</param>
    /// <returns></returns>
    protected virtual Skill? GetSkillById(Int32 id) => Skill.FindById(id);

    /// <summary>获取用户技能使用记录</summary>
    /// <param name="userId">用户编号</param>
    /// <returns></returns>
    protected virtual IList<UserSkill> GetUserSkills(Int32 userId) => UserSkill.FindAllByUserId(userId);

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
