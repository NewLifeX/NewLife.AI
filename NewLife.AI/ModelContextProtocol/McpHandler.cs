using System.Reflection;
using System.Threading;
using NewLife.Remoting;

namespace NewLife.AI.ModelContextProtocol;

internal class McpHandler : ApiHandler
{
    /// <summary>参数绑定。MCP 工具调用的 arguments 以字典直接传入（非 IPacket），基类 Prepare 只从解码字典取参，此处合并保证参数正确绑定</summary>
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

        var result = base.GetParameterValues(method, dic, raw, args, encoder);

        // CancellationToken / IProgress 等基础设施参数由框架注入，客户端不会提供：
        // 基类对非常量默认值（= default）的参数会得到 null，FastInvoker 拆箱 NRE。此处显式给装箱默认值。
        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(CancellationToken))
                result[p.Name!] = CancellationToken.None;
            else if (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(IProgress<>))
                result[p.Name!] = null;
        }

        return result;
    }
}
