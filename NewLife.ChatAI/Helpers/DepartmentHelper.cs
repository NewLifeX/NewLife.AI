using XCode.Membership;

namespace NewLife.ChatAI.Helpers;

/// <summary>部门辅助工具</summary>
public static class DepartmentHelper
{
    /// <summary>获取指定部门的完整祖先链（含自身），从直属部门向上遍历至根节点。用于父子部门权限继承</summary>
    /// <param name="deptId">起始部门编号</param>
    /// <returns>部门编号数组，第一个元素为 deptId 自身，最后一个为根部门</returns>
    public static Int32[] GetDepartmentChain(Int32 deptId)
    {
        var chain = new List<Int32>();
        var current = deptId;
        var visited = new HashSet<Int32>();
        while (current > 0 && visited.Add(current) && chain.Count < 20)
        {
            chain.Add(current);
            var dept = Department.FindByID(current);
            if (dept == null) break;
            current = dept.ParentID;
        }
        return [.. chain];
    }
}
