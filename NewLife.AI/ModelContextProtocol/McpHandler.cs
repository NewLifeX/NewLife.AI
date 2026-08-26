using System.Reflection;
using NewLife.Reflection;
using NewLife.Remoting;

namespace NewLife.AI.ModelContextProtocol;

internal class McpHandler : ApiHandler
{
    /// <summary>参数绑定。MCP 工具调用的 arguments 以字典直接传入（非 IPacket），基类 Prepare 只从解码字典取参，此处合并保证参数正确绑定。
    /// 同时为 CancellationToken / IProgress 等基础设施参数注入默认值：基类对缺失的接口类型参数会用整个字典 Convert 到接口类型（必然失败）</summary>
    /// <param name="method">工具方法</param>
    /// <param name="dic">解码字典</param>
    /// <param name="raw">原始解码参数</param>
    /// <param name="args">原始参数（MCP 场景为 arguments 字典）</param>
    /// <param name="encoder">编解码器</param>
    /// <returns>参数值集合</returns>
    protected override IDictionary<String, Object?> GetParameterValues(MethodInfo method, IDictionary<String, Object?> dic, Object? raw, Object? args, IEncoder encoder)
    {
        // dic 为空且 args 为参数字典时，用 args 作为绑定源
        if ((dic == null || dic.Count == 0) && args is IDictionary<String, Object?> adic)
            dic = adic;

        var result = new Dictionary<String, Object?>();
        var parameters = method.GetParameters();
        if (parameters == null || parameters.Length == 0) return result;

        foreach (var p in parameters)
        {
            var name = p.Name;
            if (name.IsNullOrEmpty()) continue;

            // 基础设施参数由框架注入，客户端不会提供
            if (p.ParameterType == typeof(CancellationToken))
            {
                result[name] = CancellationToken.None;
                continue;
            }
            if (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(IProgress<>))
            {
                result[name] = CreateProgress(p.ParameterType);
                continue;
            }

            // 具名参数从字典取值（对齐基类绑定逻辑）
            Object? value = null;
            var found = dic != null && dic.TryGetValue(name, out value);

            if (!found && p.HasDefaultValue)
            {
                result[name] = p.DefaultValue;
                continue;
            }

            // 缺失的必填标量参数 → 协议级无效参数（MCP 规范要求 tools/call 缺必填字段返回 InvalidParams）
            if (!found && Reflect.GetTypeCode(p.ParameterType) != TypeCode.Object)
                throw new ApiException(McpErrorCode.InvalidParams, $"Missing required argument '{name}' for tool '{method.Name}'");

            if (Reflect.GetTypeCode(p.ParameterType) != TypeCode.Object)
            {
                result[name] = value == null ? null : encoder.Convert(value, p.ParameterType);
                continue;
            }
            if (p.ParameterType == typeof(Byte[]))
            {
                result[name] = value == null ? null : Convert.FromBase64String(value.ToString() ?? "");
                continue;
            }

            // 对象类型参数：缺失时用整个字典转换（与基类一致）
            if (value == null) value = dic;
            result[name] = value == null ? null : encoder.Convert(value, p.ParameterType);
        }

        return result;
    }

    /// <summary>创建 NoOp 进度实现。注入 null 会导致工具内 <c>progress.Report()</c> 空引用崩溃；真正的进度通知推送（服务端→客户端）需要独立 SSE 通道，属后续增强</summary>
    /// <param name="progressType">IProgress&lt;T&gt; 类型</param>
    /// <returns>NoOp 实例</returns>
    private static Object CreateProgress(Type progressType)
    {
        var itemType = progressType.GetGenericArguments()[0];
        var type = typeof(NoOpProgress<>).MakeGenericType(itemType);
        return Activator.CreateInstance(type)!;
    }

    /// <summary>空进度实现，静默丢弃 Report 调用</summary>
    /// <typeparam name="T">进度值类型</typeparam>
    private sealed class NoOpProgress<T> : IProgress<T>
    {
        /// <summary>报告进度。当前不发送 MCP progress 通知</summary>
        /// <param name="value">进度值</param>
        public void Report(T value) { }
    }
}
