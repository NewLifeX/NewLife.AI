using System.ComponentModel;
using NewLife.AI.Tools;
using NewLife.Collections;
using XCode.Membership;

namespace NewLife.ChatAI.Tools;

/// <summary>当前用户工具服务。提供当前请求用户的详细档案（含 IP 地址、所在城市与地理地址），作为系统工具自动注入每次 LLM 请求，供 AI 按需查询</summary>
/// <remarks>初始化当前用户工具服务</remarks>
public class CurrentUserTool
{
    #region 工具方法

    /// <summary>获取当前登录用户的详细信息，包括用户名、昵称、邮箱、手机号、编码、角色、部门、性别、年龄、生日、备注，以及来访 IP 地址、所在城市和详细地理地址等档案数据。当用户询问"我是谁"、"我的IP"、"我在哪"或需要个人信息时调用</summary>
    [ToolDescription("get_current_user", Triggers = "我是谁,我的信息,我的档案,我的IP,我在哪", IsSystem = true)]
    [DisplayName("当前用户信息")]
    [Description("获取当前登录用户的详细信息，包括用户名、昵称、邮箱、手机号、编码、角色、部门、性别、年龄、生日、备注，以及来访 IP 地址、所在城市和详细地理地址等档案数据")]
    public String GetCurrentUser()
    {
        var user = ManageProvider.User;
        if (user == null) return "当前为匿名访问或 API 密钥访问，无法获取用户详情";

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine($"username: {user.Name}");
        sb.AppendLine($"displayName: {user.DisplayName}");

        if (!user.Mail.IsNullOrEmpty()) sb.AppendLine($"email: {user.Mail}");
        if (!user.Mobile.IsNullOrEmpty()) sb.AppendLine($"mobile: {user.Mobile}");
        if (!user.Code.IsNullOrEmpty()) sb.AppendLine($"code: {user.Code}");

        var roleIds = user.RoleIds?.SplitAsInt();
        if (roleIds?.Length > 0)
        {
            var roleNames = roleIds.Select(id => Role.FindByID(id)?.Name).Where(n => !n.IsNullOrEmpty()).Join(",");
            if (!roleNames.IsNullOrEmpty()) sb.AppendLine($"roles: {roleNames}");
        }

        if (user.DepartmentID > 0)
        {
            var dept = Department.FindByID(user.DepartmentID);
            if (dept != null) sb.AppendLine($"department: {dept.Name}");
        }

        var area = Area.FindByID(user.AreaId);
        if (area != null) sb.AppendLine($"area: {area.Path}");

        var ip = ManageProvider.UserHost;
        if (!ip.IsNullOrEmpty())
        {
            sb.AppendLine($"ip: {ip}");
            var addr = ip.IPToAddress();
            if (!addr.IsNullOrEmpty() && !addr.Contains("局域网") && !addr.Contains("本地"))
            {
                var p = addr.IndexOf(' ');
                if (p > 0)
                {
                    sb.AppendLine($"city: {addr[..p]}");
                    sb.AppendLine($"address: {addr[(p + 1)..]}");
                }
                else
                    sb.AppendLine($"address: {addr}");
            }
        }

        if (user.Sex > 0) sb.AppendLine($"sex: {user.Sex}");
        if (user.Age > 0) sb.AppendLine($"age: {user.Age}");
        if (user.Birthday.Year > 1900) sb.AppendLine($"birthday: {user.Birthday:yyyy-MM-dd}");
        if (!user.Remark.IsNullOrEmpty()) sb.AppendLine($"detail: {user.Remark}");

        return sb.Return(true).TrimEnd();
    }

    #endregion
}
