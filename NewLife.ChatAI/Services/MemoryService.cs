using System.Text;
using NewLife.ChatAI.Entity;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>记忆服务。管理用户记忆条目的增删改查，并为对话构建记忆上下文</summary>
public class MemoryService
{
    #region 属性
    private readonly ILog _log;

    /// <summary>注入系统提示词时每类记忆的最大条数</summary>
    private const Int32 MaxMemoriesPerCategory = 10;

    /// <summary>注入系统提示词时记忆总条数上限</summary>
    private const Int32 MaxTotalMemories = 30;
    #endregion

    #region 构造
    /// <summary>实例化记忆服务</summary>
    /// <param name="log">日志</param>
    public MemoryService(ILog log) => _log = log;
    #endregion

    #region 写入记忆
    /// <summary>保存或更新一条记忆条目（key 相同则更新，否则新增）</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类（preference/habit/interest/background）</param>
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

        var existing = UserMemory.FindByUserIdAndKey(userId, key);
        if (existing != null)
        {
            // 只有置信度更高或内容变化时才更新
            if (existing.Confidence < confidence || !existing.Value.EqualIgnoreCase(value))
            {
                existing.Value = value;
                existing.Confidence = confidence;
                existing.ConversationId = conversationId;
                existing.IsActive = true;
                existing.Update();
            }
            return existing;
        }

        var memory = new UserMemory
        {
            UserId = userId,
            Category = category,
            Key = key,
            Value = value,
            Confidence = confidence,
            ConversationId = conversationId,
            IsActive = true,
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
        return UserMemory.FindActiveByUserId(userId);
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
            var label = group.Key switch
            {
                "preference" => "偏好",
                "habit" => "习惯",
                "interest" => "兴趣",
                "background" => "背景",
                _ => group.Key
            };
            sb.Append("**").Append(label).AppendLine("：**");
            foreach (var m in group.Take(MaxMemoriesPerCategory))
                sb.Append("- ").Append(m.Key).Append("：").AppendLine(m.Value);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
    #endregion

    #region 画像统计
    /// <summary>刷新用户画像的记忆统计数量</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RefreshProfileAsync(Int32 userId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var profile = UserProfile.GetOrCreate(userId);
        var activeCount = (Int32)UserMemory.FindCount(UserMemory._.UserId == userId & UserMemory._.IsActive == true);
        profile.MemoryCount = activeCount;
        profile.LastAnalyzeTime = DateTime.Now;
        profile.AnalyzeCount++;
        profile.Update();
    }
    #endregion
}
