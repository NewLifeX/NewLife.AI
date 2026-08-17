using System.Runtime.Serialization;
using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>轻量 AI 对话请求。魔方式页面内嵌 AI 助手的统一入参，供 <see cref="NewLife.AI.Services.AiChatService"/> 编排</summary>
/// <remarks>
/// 仅承载通用对话字段（会话编号/消息/思考/流式）。宿主的页面上下文字段（如目标控制器、查询条件）经
/// <see cref="Items"/> 扩展字典承载，避免把具体业务字段写进基础库。
/// </remarks>
public class AiChatRequest : IExtend
{
    /// <summary>会话编号。前端生成并持久化；为空时按单轮处理（不保留会话历史）</summary>
    public String? SessionId { get; set; }

    /// <summary>用户消息</summary>
    public String? Message { get; set; }

    /// <summary>是否深度推理。为 true 时默认开启思考并降低温度</summary>
    public Boolean Think { get; set; }

    /// <summary>是否流式输出（SSE）。默认 true</summary>
    public Boolean Stream { get; set; } = true;

    /// <summary>扩展数据。承载宿主自定义字段（如目标页面上下文），经索引器读写</summary>
    [IgnoreDataMember]
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据。读取时 Items 为 null 返回 null；写入时自动创建，防止空异常</summary>
    [IgnoreDataMember]
    public Object? this[String key]
    {
        get => Items != null && Items.TryGetValue(key, out var value) ? value : null;
        set => (Items ??= new Dictionary<String, Object?>())[key] = value;
    }
}
