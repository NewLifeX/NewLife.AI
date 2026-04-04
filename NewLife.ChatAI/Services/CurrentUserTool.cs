using NewLife.AI.Tools;
using NewLife.Collections;
using XCode.Membership;

namespace NewLife.ChatAI.Services;

/// <summary>当前用户工具服务。提供当前请求用户的详细档案，作为系统工具自动注入每次 LLM 请求，供 AI 按需查询</summary>
/// <remarks>初始化当前用户工具服务</remarks>
/// <param name="serviceProvider">依赖注入服务提供者</param>
public class CurrentUserTool(IServiceProvider serviceProvider)
{
    #region 工具方法

    /// <summary>获取当前登录用户的详细信息，包括用户名、昵称、角色、部门等档案数据。当用户询问"我是谁"或需要个人信息时调用</summary>
    [ToolDescription("get_current_user", IsSystem = true)]
    public String GetCurrentUser()
    {
        var iuser = ManageProvider.User as IUser;
        if (iuser == null) return "当前为匿名访问或 API 密钥访问，无法获取用户详情";

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine($"username: {iuser.Name}");
        sb.AppendLine($"displayName: {iuser.DisplayName}");

        var roleIds = iuser.RoleIds?.SplitAsInt();
        if (roleIds?.Length > 0)
        {
            var roleNames = roleIds.Select(id => Role.FindByID(id)?.Name).Where(n => !n.IsNullOrEmpty()).Join(",");
            if (!roleNames.IsNullOrEmpty()) sb.AppendLine($"roles: {roleNames}");
        }

        if (iuser.DepartmentID > 0)
        {
            var dept = Department.FindByID(iuser.DepartmentID);
            if (dept != null) sb.AppendLine($"department: {dept.Name}");
        }

        return sb.Return(true).TrimEnd();
    }

    #endregion
}
