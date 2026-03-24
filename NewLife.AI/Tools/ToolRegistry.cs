using System.Reflection;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>工具注册表。管理原生 .NET 工具的注册、查询与调用分发</summary>
/// <remarks>
/// 支持三种注册方式：
/// <list type="number">
/// <item>注册单个委托（通过 <see cref="AddTool"/>）</item>
/// <item>注册整个服务类中所有 <see cref="ToolDescriptionAttribute"/> 标注方法（通过 <see cref="AddTools{T}"/>）</item>
/// <item>扫描程序集批量注册（通过 <see cref="AddToolsFromAssembly"/>）</item>
/// </list>
/// </remarks>
public class ToolRegistry : IToolProvider
{
    #region 属性

    /// <summary>已注册工具的 ChatTool 定义列表，可直接注入到 ChatCompletionRequest.Tools</summary>
    public IReadOnlyList<ChatTool> Tools => _tools.AsReadOnly();

    private readonly List<ChatTool> _tools = [];
    private readonly Dictionary<String, Func<String?, CancellationToken, Task<String>>> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 注册方法

    /// <summary>注册单个委托为命名工具</summary>
    /// <param name="name">工具名称</param>
    /// <param name="handler">处理委托，参数为 JSON 字符串，返回 JSON 字符串结果</param>
    /// <param name="description">工具功能描述（可选）</param>
    public void AddTool(String name, Func<String?, CancellationToken, Task<String>> handler, String? description = null)
    {
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        _tools.Add(new ChatTool
        {
            Function = new FunctionDefinition
            {
                Name = name,
                Description = description
            }
        });
        _handlers[name] = handler;
    }

    /// <summary>扫描类型 <typeparamref name="T"/> 中所有标注 <see cref="ToolDescriptionAttribute"/> 的公共方法并注册</summary>
    /// <typeparam name="T">包含工具方法的服务类型</typeparam>
    /// <param name="instance">工具方法的宿主实例</param>
    public void AddTools<T>(T instance) where T : notnull
    {
        AddToolsFromInstance(typeof(T), instance);
    }

    /// <summary>扫描给定实例的类型中所有标注 <see cref="ToolDescriptionAttribute"/> 的公共方法并注册</summary>
    /// <param name="instance">工具方法的宿主实例</param>
    public void AddTools(Object instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        AddToolsFromInstance(instance.GetType(), instance);
    }

    /// <summary>扫描程序集中所有具有无参构造函数的类型，注册全部 <see cref="ToolDescriptionAttribute"/> 方法</summary>
    /// <param name="assembly">目标程序集</param>
    public void AddToolsFromAssembly(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
                .ToList();
            if (methods.Count == 0) continue;

            // 尝试用无参构造函数实例化;不支持则跳过
            Object? instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch
            {
                continue;
            }
            if (instance == null) continue;

            foreach (var method in methods)
                RegisterMethod(method, instance);
        }
    }

    #endregion

    #region 调用分发

    /// <summary>根据工具名称和 JSON 参数调用已注册的工具处理器</summary>
    /// <param name="name">工具名称（大小写不敏感）</param>
    /// <param name="arguments">JSON 格式的参数字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果的 JSON 字符串</returns>
    /// <exception cref="KeyNotFoundException">工具名称未注册</exception>
    public Task<String> InvokeAsync(String name, String? arguments, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(name, out var handler))
            throw new KeyNotFoundException($"工具 '{name}' 未注册到 ToolRegistry");
        return handler(arguments, cancellationToken);
    }

    /// <summary>尝试调用工具，工具未注册或执行出错时返回错误描述（不抛异常）</summary>
    /// <param name="name">工具名称</param>
    /// <param name="arguments">JSON 格式参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果 JSON 字符串，或错误描述字符串</returns>
    public async Task<String> TryInvokeAsync(String name, String? arguments, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(name, out var handler))
            return $"{{\"error\":\"tool '{name}' not registered\"}}";
        try
        {
            return await handler(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"{{\"error\":{ex.Message.ToJson()}}}";
        }
    }

    #endregion

    #region 辅助

    private void AddToolsFromInstance(Type type, Object instance)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null);
        foreach (var method in methods)
            RegisterMethod(method, instance);
    }

    private void RegisterMethod(MethodInfo method, Object instance)
    {
        var tool = ToolSchemaBuilder.BuildFromMethod(method);
        var toolName = tool.Function!.Name;

        if (_handlers.ContainsKey(toolName)) return;  // 已注册则跳过，不覆盖

        _tools.Add(tool);
        _handlers[toolName] = (args, ct) => InvokeMethodAsync(method, instance, args, ct);
    }

    private static async Task<String> InvokeMethodAsync(MethodInfo method, Object instance, String? arguments, CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        Object?[] args;
        if (parameters.Length == 0 || String.IsNullOrWhiteSpace(arguments))
            args = BuildDefaultArgs(method);
        else
            args = DeserializeArguments(parameters, arguments);

        // 将所有 CancellationToken 参数替换为传入的 ct
        var allParams = method.GetParameters();
        var finalArgs = new Object?[allParams.Length];
        var argIdx = 0;
        for (var i = 0; i < allParams.Length; i++)
        {
            if (allParams[i].ParameterType == typeof(CancellationToken))
                finalArgs[i] = cancellationToken;
            else
                finalArgs[i] = argIdx < args.Length ? args[argIdx++] : (allParams[i].HasDefaultValue ? allParams[i].DefaultValue : null);
        }

        Object? result;
        try
        {
            result = method.Invoke(instance, finalArgs);
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }

        if (result == null)
            return "null";
        if (result is Task<String> taskStr)
            return await taskStr.ConfigureAwait(false);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            // ValueTask<T> or Task<T>：通过反射获取 Result 属性
            var resultProp = result.GetType().GetProperty("Result");
            result = resultProp?.GetValue(result);
        }
        if (result == null) return "null";
        if (result is String str) return str;

        return result.ToJson();
    }

    private static Object?[] BuildDefaultArgs(MethodInfo method)
    {
        var nonCt = method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).ToArray();
        var defaults = new Object?[nonCt.Length];
        for (var i = 0; i < nonCt.Length; i++)
            defaults[i] = nonCt[i].HasDefaultValue ? nonCt[i].DefaultValue : null;
        return defaults;
    }

    private static Object?[] DeserializeArguments(ParameterInfo[] parameters, String arguments)
    {
        var result = new Object?[parameters.Length];
        Dictionary<String, Object?>? parsed;
        try
        {
            parsed = arguments.ToJsonEntity<Dictionary<String, Object?>>();
        }
        catch
        {
            // 参数 JSON 格式异常（如流式截断导致不完整 JSON），使用默认值
            return result;
        }
        if (parsed == null) return result;

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.Name == null) continue;

            if (parsed.TryGetValue(p.Name, out var value))
                result[i] = ConvertValue(value, p.ParameterType);
            else if (p.HasDefaultValue)
                result[i] = p.DefaultValue;
        }
        return result;
    }

    private static Object? ConvertValue(Object? value, Type targetType)
    {
        if (value == null) return null;
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(String)) return value.ToString();
        if (underlyingType == typeof(Boolean)) return Convert.ToBoolean(value);
        if (underlyingType == typeof(Int32)) return Convert.ToInt32(value);
        if (underlyingType == typeof(Int64)) return Convert.ToInt64(value);
        if (underlyingType == typeof(Double)) return Convert.ToDouble(value);
        if (underlyingType == typeof(Single)) return Convert.ToSingle(value);
        if (underlyingType == typeof(Decimal)) return Convert.ToDecimal(value);
        if (underlyingType.IsEnum) return Enum.Parse(underlyingType, value.ToString() ?? String.Empty, ignoreCase: true);

        // 复杂类型：序列化回 JSON 再反序列化为目标类型
        if (value is IDictionary<String, Object?> || value is IList<Object?>)
            return value.ToJson().ToJsonEntity(underlyingType);

        return value;
    }

    #endregion

    #region IToolProvider

    IList<ChatTool> IToolProvider.GetTools() => new List<ChatTool>(_tools);

    Task<String> IToolProvider.CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken)
        => InvokeAsync(toolName, argumentsJson, cancellationToken);

    #endregion
}
