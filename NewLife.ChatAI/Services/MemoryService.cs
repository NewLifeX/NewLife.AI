using System.Text;
using NewLife.ChatData.Entity;
using NewLife.ChatAI.Models;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>记忆服务。管理用户记忆条目的增删改查，并为对话构建记忆上下文</summary>
/// <remarks>实例化记忆服务</remarks>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class MemoryService(ITracer tracer, ILog log)
{
    #region 属性
    /// <summary>注入系统提示词时每类记忆的最大条数</summary>
    private const Int32 MaxMemoriesPerCategory = 10;

    /// <summary>注入系统提示词时记忆总条数上限</summary>
    private const Int32 MaxTotalMemories = 30;
    #endregion

    #region 分类规范化
    /// <summary>英文枚举值到中文标准分类名的映射</summary>
    public static readonly Dictionary<String, String> CategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = "身份信息",
        ["preference"] = "偏好",
        ["habit"] = "习惯",
        ["interest"] = "兴趣",
        ["background"] = "背景",
        ["profession"] = "职业",
        ["goal"] = "目标",
        ["relationship"] = "人际关系",
        ["skill"] = "技能",
        ["instruction"] = "交互指令",
    };

    /// <summary>中文别名到标准中文分类名的映射（消除 AI 返回的中文变体）</summary>
    public static readonly Dictionary<String, String> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["技能专长"] = "技能",
        ["目标计划"] = "目标",
        ["背景信息"] = "背景",
        ["职业信息"] = "职业",
        ["兴趣爱好"] = "兴趣",
        ["行为习惯"] = "习惯",
    };

    /// <summary>有效的记忆分类集合（英文枚举值 + 中文标准名，用于校验）</summary>
    public static readonly HashSet<String> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "identity", "preference", "habit", "interest", "background",
        "profession", "goal", "relationship", "skill", "instruction",
        "身份信息", "偏好", "习惯", "兴趣", "背景",
        "职业", "目标", "人际关系", "技能", "交互指令",
    };

    /// <summary>规范化分类名。英文枚举值映射为中文标准名，中文别名归一化，无效值回退为"通用"</summary>
    /// <param name="category">AI 返回的分类名（英文枚举值或中文）</param>
    /// <returns>中文标准分类名</returns>
    public static String NormalizeCategory(String? category)
    {
        if (category.IsNullOrWhiteSpace()) return "通用";

        // 英文枚举值 → 中文标准名
        if (CategoryNames.TryGetValue(category!, out var chineseName))
            return chineseName;

        // 中文别名 → 标准中文名
        if (CategoryAliases.TryGetValue(category!, out var standard))
            return standard;

        // 已经是标准中文分类名
        if (CategoryNames.ContainsValue(category!))
            return category!;

        return "通用";
    }

    /// <summary>将中文分类名转换为英文枚举值（供需要英文的场景使用）</summary>
    /// <param name="category">中文分类名或英文枚举值</param>
    /// <returns>英文枚举值</returns>
    public static String GetEnglishCategory(String? category)
    {
        if (category.IsNullOrWhiteSpace()) return "general";

        // 已经是英文枚举值
        if (CategoryNames.ContainsKey(category!))
            return category!.ToLower();

        // 标准中文名 → 英文
        foreach (var kvp in CategoryNames)
        {
            if (kvp.Value.EqualIgnoreCase(category!))
                return kvp.Key;
        }

        // 中文别名 → 先获取标准中文名 → 再转英文
        if (CategoryAliases.TryGetValue(category!, out var standard))
            return GetEnglishCategory(standard);

        return "general";
    }
    #endregion

    #region 写入记忆
    /// <summary>保存或更新一条记忆条目（key 相同则更新，否则新增）</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类（偏好/习惯/兴趣/背景）</param>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="confidence">置信度（0-100）</param>
    /// <param name="conversationId">来源会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存后的记忆实体</returns>
    public async Task<UserMemory> UpsertMemoryAsync(
        Int32 userId,
        String category,
        String key,
        String value,
        Int32 confidence,
        Int64 conversationId,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // 允许调用方继续（实体操作为同步，保持异步接口一致性）

        // 规范化分类名，确保始终以中文存储
        category = NormalizeCategory(category);

        var existing = UserMemory.FindAllByUserId(userId).FirstOrDefault(e => e.Key.EqualIgnoreCase(key));
        if (existing != null)
        {
            // 只有置信度更高或内容变化时才更新
            if (existing.Confidence < confidence || !existing.Value.EqualIgnoreCase(value))
            {
                var oldValue = existing.Value;
                existing.Category = category;
                existing.Value = value;
                existing.Confidence = confidence;
                existing.ConversationId = conversationId;
                existing.Enable = true;
                existing.Version++;
                existing.Update();
            }
            return existing;
        }

        // 新记忆：根据信任等级决定是否需要审核
        var memory = new UserMemory
        {
            UserId = userId,
            Category = category,
            Key = key,
            Value = value,
            Confidence = confidence,
            ConversationId = conversationId,
            Scope = "user",
            Status = 1,
            Version = 1,
            Enable = true,
        };
        memory.Insert();

        return memory;
    }

    /// <summary>停用一条记忆（软删除）</summary>
    /// <param name="memoryId">记忆ID</param>
    /// <returns>是否找到并停用</returns>
    public Boolean Deactivate(Int64 memoryId)
    {
        var memory = UserMemory.FindById(memoryId);
        if (memory == null) return false;
        memory.Deactivate();
        return true;
    }

    /// <summary>删除一条记忆（硬删除）</summary>
    /// <param name="memoryId">记忆ID</param>
    /// <returns>是否找到并删除</returns>
    public Boolean Delete(Int64 memoryId)
    {
        var memory = UserMemory.FindById(memoryId);
        if (memory == null) return false;
        memory.Delete();
        return true;
    }
    #endregion

    #region 查询记忆
    /// <summary>获取用户所有有效记忆，按置信度降序排列</summary>
    /// <param name="userId">用户ID</param>
    /// <returns>有效记忆列表</returns>
    public virtual IList<UserMemory> GetActiveMemories(Int32 userId)
    {
        if (userId <= 0) return [];

        var list = UserMemory.FindActiveByUserId(userId);

        // 自动迁移历史英文分类名为中文并回写数据库（首次查询时一次性修复）
        foreach (var m in list)
        {
            if (m.Category.IsNullOrEmpty() || !CategoryNames.ContainsKey(m.Category)) continue;
            m.Category = NormalizeCategory(m.Category);
            m.Update();
        }

        return list;
    }

    /// <summary>获取指定分类的有效记忆</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类</param>
    /// <returns>记忆列表</returns>
    public IList<UserMemory> GetMemoriesByCategory(Int32 userId, String category)
    {
        if (userId <= 0) return [];

        return UserMemory.FindAllByUserIdAndCategory(userId, category);
    }

    /// <summary>获取用户有效记忆的分页列表</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类过滤（可选）</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页记忆列表</returns>
    public MemoryListDto GetActiveMemoriesPaged(Int32 userId, String? category, Int32 page, Int32 pageSize)
    {
        var memories = category.IsNullOrEmpty()
            ? GetActiveMemories(userId)
            : GetMemoriesByCategory(userId, category!);

        var total = memories.Count;
        var items = memories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MemoryItemDto
            {
                Id = m.Id,
                Category = m.Category,
                Key = m.Key,
                Value = m.Value,
                Confidence = m.Confidence,
                Enable = m.Enable,
                CreateTime = m.CreateTime,
                UpdateTime = m.UpdateTime,
            })
            .ToList();

        return new MemoryListDto { Total = total, Items = items, Page = page, PageSize = pageSize };
    }
    #endregion

    #region 上下文构建
    /// <summary>为用户构建记忆上下文字符串，用于注入系统提示词</summary>
    /// <param name="userId">用户ID</param>
    /// <returns>格式化的记忆上下文；无有效记忆时返回 null</returns>
    public virtual String? BuildContextForUser(Int32 userId)
    {
        if (userId <= 0) return null;

        var memories = GetActiveMemories(userId);
        if (memories.Count == 0) return null;

        using var span = tracer?.NewSpan("ai:BuildContextForUser", userId, memories.Count);

        // 按分类分组，每分类最多取 MaxMemoriesPerCategory 条（置信度最高优先）
        var grouped = memories
            .OrderByDescending(m => m.Confidence)
            .Take(MaxTotalMemories)
            .GroupBy(m => m.Category);

        var sb = new StringBuilder();
        sb.AppendLine("## 关于用户的记忆");
        sb.AppendLine("以下是从历史对话中学习到的用户信息，请在回答中适当参考：");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            // 兼容历史英文分类：将英文枚举值翻译为中文显示
            var label = NormalizeCategory(group.Key);
            sb.Append("**").Append(label).AppendLine("：**");
            foreach (var m in group.Take(MaxMemoriesPerCategory))
            {
                sb.Append("- ").Append(m.Key).Append("：").AppendLine(m.Value);
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
    #endregion
}
