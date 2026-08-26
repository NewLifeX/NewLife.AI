using System.Reflection;
using NewLife.Collections;
using NewLife.Reflection;
using NewLife.Remoting;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>MCP 工具定义。封装工具方法的注册、参数绑定与调用</summary>
/// <remarks>
/// 自包含实现：不依赖 NewLife.Remoting 的 ApiManager/ApiHandler，直接基于反射完成
/// 工具实例创建、参数绑定（含 CancellationToken/IProgress 注入、必填校验）与调用（异步解包）。
/// </remarks>
public class McpTool
{
    /// <summary>工具名（snake_case）</summary>
    public String Name { get; set; } = "";

    /// <summary>工具方法</summary>
    public MethodInfo Method { get; set; } = null!;

    /// <summary>缓存的 inputSchema（懒生成）</summary>
    public Object? Schema { get; set; }

    /// <summary>创建工具实例。优先从 DI 解析，回退无参构造</summary>
    /// <param name="serviceProvider">服务提供者（可为 null）</param>
    /// <returns>工具实例；创建失败返回 null</returns>
    public Object? CreateInstance(IServiceProvider? serviceProvider)
    {
        var type = Method.DeclaringType;
        if (type == null) return null;

        var instance = serviceProvider?.GetService(type);
        if (instance != null) return instance;

        try { return Reflect.CreateInstance(type); }
        catch { return null; }
    }

    /// <summary>绑定参数并调用工具方法。异步方法自动解包返回结果</summary>
    /// <param name="instance">工具实例</param>
    /// <param name="args">参数字典（MCP arguments）</param>
    /// <returns>工具返回值；无返回值或异步 void 时返回 null</returns>
    /// <exception cref="ApiException">必填参数缺失或参数非法时抛出 InvalidParams 错误码</exception>
    public Object? Invoke(Object? instance, IDictionary<String, Object?>? args)
    {
        var parameters = Method.GetParameters();
        if (parameters == null || parameters.Length == 0) return InvokeCore(instance, []);

        var values = new Object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            values[i] = BindParameter(parameters[i], args);

        return InvokeCore(instance, values);
    }

    /// <summary>执行方法调用并解包异步结果</summary>
    /// <param name="instance">工具实例</param>
    /// <param name="args">已绑定的参数数组</param>
    /// <returns>工具返回值</returns>
    private Object? InvokeCore(Object? instance, Object?[] args)
    {
        Object? rs;
        try
        {
            rs = Method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // 解包反射调用包装，保留工具抛出的原始异常（Process catch 据此正确分类错误码与消息）
            throw ex.InnerException;
        }

        if (rs is Task task)
        {
            task.GetAwaiter().GetResult();

            // 方法返回非泛型 Task（async Task）→ 无返回值。注意：async Task 的运行时对象是
            // Task&lt;VoidTaskResult&gt;（编译器实现细节），不能靠运行时类型泛型判断，须看方法签名
            if (Method.ReturnType == typeof(Task)) return null;

            // 泛型 Task&lt;T&gt; 取 Result
            var prop = task.GetType().GetProperty("Result");
            if (prop != null) rs = prop.GetValue(task);
        }

        return rs;
    }

    /// <summary>绑定单个参数值。基础设施参数（CancellationToken/IProgress）由框架注入，其余按名称从参数字典取值</summary>
    /// <param name="p">参数信息</param>
    /// <param name="args">参数字典</param>
    /// <returns>参数值</returns>
    /// <exception cref="ApiException">必填标量参数缺失时抛出 InvalidParams</exception>
    private Object? BindParameter(ParameterInfo p, IDictionary<String, Object?>? args)
    {
        var name = p.Name;
        if (name.IsNullOrEmpty()) return null;

        // 基础设施参数由框架注入，客户端不会提供
        if (p.ParameterType == typeof(CancellationToken)) return CancellationToken.None;
        if (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(IProgress<>))
            return CreateProgress(p.ParameterType);

        // 具名参数从字典取值
        Object? value = null;
        var found = args != null && args.TryGetValue(name, out value);

        if (!found && p.HasDefaultValue) return p.DefaultValue;

        // 缺失的必填标量参数 → 协议级无效参数（MCP 规范要求 tools/call 缺必填字段返回 InvalidParams）
        if (!found && Reflect.GetTypeCode(p.ParameterType) != TypeCode.Object)
            throw new ApiException(McpErrorCode.InvalidParams, $"Missing required argument '{name}' for tool '{Method.Name}'");

        if (Reflect.GetTypeCode(p.ParameterType) != TypeCode.Object)
            return value == null ? null : Reflect.ChangeType(value, p.ParameterType);

        if (p.ParameterType == typeof(Byte[]))
            return value == null ? null : Convert.FromBase64String(value.ToString() ?? "");

        // 对象类型参数：缺失时用整个字典转换（对齐原 ApiHandler 绑定逻辑）
        if (value == null) value = args;
        return value == null ? null : Reflect.ChangeType(value, p.ParameterType);
    }

    /// <summary>创建 NoOp 进度实现。注入 null 会导致工具内 <c>progress.Report()</c> 空引用崩溃；真正的 progress 通知推送（服务端→客户端）需要独立 SSE 通道，属后续增强</summary>
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

/// <summary>MCP 工具管理器。自包含实现：反射注册工具类、查找工具、生成工具 schema（不依赖 NewLife.Remoting）</summary>
public class McpToolManager
{
    private readonly Dictionary<String, McpTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>工具集合。name（snake_case）→ 工具定义</summary>
    public IDictionary<String, McpTool> Tools => _tools;

    /// <summary>工具集合（兼容别名，等价 <see cref="Tools"/>）。对齐原 ApiManager.Services 语义</summary>
    public IDictionary<String, McpTool> Services => _tools;

    /// <summary>注册工具类型。其公共方法将作为 MCP 工具暴露（方法名自动转 snake_case，剥 Async 后缀）</summary>
    /// <typeparam name="T">工具类型</typeparam>
    public void Register<T>() => Register(typeof(T));

    /// <summary>注册工具类型。其公共方法将作为 MCP 工具暴露（snake_case 命名）</summary>
    /// <param name="type">工具类型</param>
    public void Register(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            // 排除属性访问器/运算符等特殊方法、Object 基类方法（含重写：ToString/GetHashCode/Equals）与泛型方法
            if (method.IsSpecialName) continue;
            if (method.GetBaseDefinition().DeclaringType == typeof(Object)) continue;
            if (method.ContainsGenericParameters) continue;

            var name = ToSnakeCase(method.Name);
            _tools[name] = new McpTool { Name = name, Method = method };
        }
    }

    /// <summary>查找工具</summary>
    /// <param name="name">工具名（snake_case）</param>
    /// <returns>工具定义；未找到返回 null</returns>
    public McpTool? Find(String name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>方法名转 snake_case。先剥 Async 后缀（对齐官方 SDK：异步方法工具名不带 _async 后缀）</summary>
    /// <param name="name">方法名</param>
    /// <returns>snake_case 名称</returns>
    private static String ToSnakeCase(String name)
    {
        if (String.IsNullOrEmpty(name)) return name;

        if (name.EndsWith("Async", StringComparison.Ordinal) && name.Length > 5)
            name = name[..^5];

        var sb = Pool.StringBuilder.Get();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (Char.IsUpper(c))
            {
                // 大写字母前插入下划线（首字母除外）：
                // - 前一个字符是小写（PascalCase 边界：GetName → get_name）
                // - 前一个字符是大写、后一个字符是小写（缩写末尾：GetAPIKey → get_api_key，避免 get_apikey）
                if (sb.Length > 0 && i > 0 &&
                    (Char.IsLower(name[i - 1]) ||
                     (Char.IsUpper(name[i - 1]) && i + 1 < name.Length && Char.IsLower(name[i + 1]))))
                    sb.Append('_');
                sb.Append(Char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Return(true);
    }
}

