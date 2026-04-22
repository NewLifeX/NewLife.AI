using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理</summary>
public partial interface INativeTool
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>名称。工具唯一标识（snake_case），如get_current_time</summary>
    String? Name { get; set; }

    /// <summary>显示名称。工具的中文名称，用于后台展示</summary>
    String? DisplayName { get; set; }

    /// <summary>类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService</summary>
    String? ClassName { get; set; }

    /// <summary>方法名。工具对应的C#方法名，如GetCurrentTime</summary>
    String? MethodName { get; set; }

    /// <summary>描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖</summary>
    String? Description { get; set; }

    /// <summary>参数Schema。JSON格式的函数参数定义，锁定后不再覆盖</summary>
    String? Parameters { get; set; }

    /// <summary>触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）</summary>
    String? Triggers { get; set; }

    /// <summary>启用。是否启用此工具，禁用后不传给LLM调用</summary>
    Boolean Enable { get; set; }

    /// <summary>系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息</summary>
    Boolean IsSystem { get; set; }

    /// <summary>锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整</summary>
    Boolean IsLocked { get; set; }

    /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
    String? RoleIds { get; set; }

    /// <summary>部门组。逗号分隔部门ID列表，命中即放行；为空时不限制</summary>
    String? DepartmentIds { get; set; }

    /// <summary>项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制</summary>
    String? ProjectIds { get; set; }

    /// <summary>服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo</summary>
    String? Providers { get; set; }

    /// <summary>远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com</summary>
    String? Endpoint { get; set; }

    /// <summary>API密钥。调用远程服务所需的访问密钥</summary>
    String? ApiKey { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
