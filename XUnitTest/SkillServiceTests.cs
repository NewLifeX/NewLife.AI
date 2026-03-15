using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Log;
using Xunit;

namespace XUnitTest;

[DisplayName("技能服务测试")]
public class SkillServiceTests
{
    #region 可测试子类

    /// <summary>覆盖所有 DB 访问方法的可测试 SkillService，使用内存数据</summary>
    private sealed class TestableSkillService : SkillService
    {
        private readonly IList<Skill> _skills;
        private readonly IList<UserSkill> _userSkills;

        public TestableSkillService(IList<Skill>? skills = null, IList<UserSkill>? userSkills = null)
            : base(Logger.Null)
        {
            _skills = skills ?? [];
            _userSkills = userSkills ?? [];
        }

        protected override IList<Skill> GetSystemSkills() =>
            _skills.Where(s => s.IsSystem && s.Enable).OrderByDescending(s => s.Sort).ToList();

        protected override Skill? GetSkillById(Int32 id) =>
            _skills.FirstOrDefault(s => s.Id == id);

        protected override IList<UserSkill> GetUserSkills(Int32 userId) =>
            _userSkills.Where(us => us.UserId == userId).ToList();

        protected override Skill? FindSkillByName(String name) =>
            _skills.FirstOrDefault(s => s.Code == name || s.Name == name);

        private static Skill MakeSkill(Int32 id, String code, String name, String content,
                                       Boolean isSystem = false, Boolean enable = true, Int32 sort = 0) =>
            new Skill { Id = id, Code = code, Name = name, Content = content,
                        IsSystem = isSystem, Enable = enable, Sort = sort > 0 ? sort : id * 10 };

        public static TestableSkillService Create(params (Int32 id, String code, String name, String content, Boolean isSystem)[] definitions)
        {
            var list = definitions.Select(d => MakeSkill(d.id, d.code, d.name, d.content, d.isSystem)).ToList();
            return new TestableSkillService(list);
        }

        public static TestableSkillService WithUserSkills(IList<Skill> skills, Int32 userId, params (Int32 skillId, DateTime lastUse)[] usages)
        {
            var userSkills = usages.Select((u, i) =>
                new UserSkill { Id = i + 1, UserId = userId, SkillId = u.skillId, LastUseTime = u.lastUse }
            ).ToList<UserSkill>();
            return new TestableSkillService(skills, userSkills);
        }
    }

    #endregion

    #region BuildSkillPrompt 测试

    [Fact]
    [DisplayName("BuildSkillPrompt：无技能时返回 null")]
    public void BuildSkillPrompt_NoSkills_ReturnsNull()
    {
        var svc = new TestableSkillService();
        var result = svc.BuildSkillPrompt(0, null);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("BuildSkillPrompt：含会话技能时内容出现在返回值")]
    public void BuildSkillPrompt_WithConversationSkill_IncludesContent()
    {
        var svc = TestableSkillService.Create(
            (1, "coder", "编程助手", "你是专业编程助手", false)
        );
        var result = svc.BuildSkillPrompt(1, null);
        Assert.NotNull(result);
        Assert.Contains("你是专业编程助手", result);
    }

    [Fact]
    [DisplayName("BuildSkillPrompt：消息中的 @引用 递归展开")]
    public void BuildSkillPrompt_WithAtReference_Resolves()
    {
        var skills = new List<Skill>
        {
            new Skill { Id = 1, Code = "base", Name = "基础", Content = "基础内容", IsSystem = false, Enable = true, Sort = 10 },
            new Skill { Id = 2, Code = "composer", Name = "组合", Content = "组合内容 @base", IsSystem = false, Enable = true, Sort = 20 },
        };
        var svc = new TestableSkillService(skills);
        // 消息引用 composer，composer 内容又引用 @base，应递归展开
        var result = svc.BuildSkillPrompt(0, "@composer");
        Assert.NotNull(result);
        Assert.Contains("基础内容", result);
    }

    [Fact]
    [DisplayName("BuildSkillPrompt：循环引用 A→B→A 不导致无限循环")]
    public void BuildSkillPrompt_CircularReference_DoesNotInfiniteLoop()
    {
        var skills = new List<Skill>
        {
            new Skill { Id = 1, Code = "a", Name = "A技能", Content = "A内容 @b", IsSystem = false, Enable = true, Sort = 10 },
            new Skill { Id = 2, Code = "b", Name = "B技能", Content = "B内容 @a", IsSystem = false, Enable = true, Sort = 20 },
        };
        var svc = new TestableSkillService(skills);
        // 不应抛出 StackOverflowException
        var result = svc.BuildSkillPrompt(0, "@a");
        Assert.NotNull(result);
        Assert.Contains("A内容", result);
    }

    [Fact]
    [DisplayName("BuildSkillPrompt：系统技能内容自动包含在返回值中")]
    public void BuildSkillPrompt_SystemSkillAlwaysIncluded()
    {
        var svc = TestableSkillService.Create(
            (1, "sys", "系统技能", "系统技能内容", true)
        );
        // 无会话技能、无消息，系统技能依然注入
        var result = svc.BuildSkillPrompt(0, null);
        Assert.NotNull(result);
        Assert.Contains("系统技能内容", result);
    }

    #endregion

    #region GetSkillBarList 测试

    [Fact]
    [DisplayName("GetSkillBarList：最近使用的技能排在系统技能前面")]
    public void GetSkillBarList_UserHasRecentSkill_IsFirst()
    {
        var skills = new List<Skill>
        {
            new Skill { Id = 1, Code = "sys1", Name = "系统技能1", IsSystem = true, Enable = true, Sort = 100 },
            new Skill { Id = 2, Code = "user1", Name = "用户最近", IsSystem = false, Enable = true, Sort = 50 },
        };
        var svc = TestableSkillService.WithUserSkills(
            skills, 10,
            (skillId: 2, lastUse: DateTime.Now.AddMinutes(-1))
        );
        var result = svc.GetSkillBarList(userId: 10);
        Assert.True(result.Count >= 2);
        Assert.Equal(2, result[0].Id); // 最近使用排第一
    }

    #endregion

    #region RecordUsage 测试

    [Fact]
    [DisplayName("RecordUsage：userId=0 时静默返回不抛异常")]
    public void RecordUsage_ZeroUserId_NoException()
    {
        var svc = new TestableSkillService();
        // userId=0 应被守卫提前返回，不触发任何 DB 调用
        var ex = Record.Exception(() => svc.RecordUsage(0, 1));
        Assert.Null(ex);
    }

    #endregion
}
