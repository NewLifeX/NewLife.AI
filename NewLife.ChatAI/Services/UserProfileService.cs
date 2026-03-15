using NewLife.ChatAI.Entity;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Services;

/// <summary>用户画像服务。提供用户画像和标签的查询与更新操作</summary>
public class UserProfileService
{
    #region 属性
    private readonly ILog _log;
    #endregion

    #region 构造
    /// <summary>实例化用户画像服务</summary>
    /// <param name="log">日志</param>
    public UserProfileService(ILog log) => _log = log;
    #endregion

    #region 画像
    /// <summary>获取或创建用户画像</summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户画像实体</returns>
    public UserProfile GetOrCreateProfile(Int32 userId)
    {
        if (userId <= 0) throw new ArgumentException("用户ID无效", nameof(userId));
        return UserProfile.GetOrCreate(userId);
    }

    /// <summary>更新用户画像摘要和 JSON 字段</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="summary">总结文本（可选）</param>
    /// <param name="preferences">偏好 JSON（可选）</param>
    /// <param name="habits">习惯 JSON（可选）</param>
    /// <param name="interests">兴趣 JSON（可选）</param>
    /// <returns>更新后的画像</returns>
    public UserProfile UpdateProfile(Int32 userId, String? summary = null, String? preferences = null, String? habits = null, String? interests = null)
    {
        var profile = GetOrCreateProfile(userId);
        var dirty = false;

        if (summary != null) { profile.Summary = summary; dirty = true; }
        if (preferences != null) { profile.Preferences = preferences; dirty = true; }
        if (habits != null) { profile.Habits = habits; dirty = true; }
        if (interests != null) { profile.Interests = interests; dirty = true; }

        if (dirty) profile.Update();
        return profile;
    }
    #endregion

    #region 标签
    /// <summary>获取用户全部标签，按权重降序</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类过滤（可选）</param>
    /// <returns>标签列表</returns>
    public IList<UserTag> GetTags(Int32 userId, String? category = null)
    {
        if (userId <= 0) return [];
        return category.IsNullOrEmpty()
            ? UserTag.FindAllByUserId(userId)
            : UserTag.FindAllByUserIdAndCategory(userId, category!);
    }

    /// <summary>保存标签（已存在则更新权重，否则新增）</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="name">标签名</param>
    /// <param name="category">分类</param>
    /// <param name="weight">权重（0-100）</param>
    /// <returns>标签实体</returns>
    public UserTag UpsertTag(Int32 userId, String name, String category, Int32 weight = 50)
    {
        if (userId <= 0) throw new ArgumentException("用户ID无效", nameof(userId));
        if (name.IsNullOrEmpty()) throw new ArgumentException("标签名称不能为空", nameof(name));
        return UserTag.Upsert(userId, name, category, weight);
    }

    /// <summary>删除标签</summary>
    /// <param name="tagId">标签ID</param>
    /// <returns>是否删除成功</returns>
    public Boolean DeleteTag(Int32 tagId)
    {
        var tag = UserTag.FindById(tagId);
        if (tag == null) return false;
        tag.Delete();
        return true;
    }

    /// <summary>获取用户的标签分类统计</summary>
    /// <param name="userId">用户ID</param>
    /// <returns>各分类下的标签数量字典</returns>
    public IDictionary<String, Int32> GetTagCategoryStats(Int32 userId)
    {
        var tags = UserTag.FindAllByUserId(userId);
        return tags.GroupBy(t => t.Category ?? "other").ToDictionary(g => g.Key, g => g.Count());
    }
    #endregion
}
