using System.ComponentModel;
using System.Reflection;
using NewLife.AI.Models;
using NewLife.AI.Interfaces;
using NewLife.Model;
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

    /// <summary>已注册工具服务的类型列表，供数据预热等流程扫描工具元信息</summary>
    public IReadOnlyList<Type> RegisteredTypes => _registeredTypes.AsReadOnly();

    /// <summary>服务提供者。用于解析内部工具对象</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private readonly List<ChatTool> _tools = [];
    private readonly List<Type> _registeredTypes = [];
    private readonly Dictionary<String, Func<String?, ToolCallContext?, CancellationToken, Task<String>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<String> _systemNames = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region 注册方法

    /// <summary>注册单个委托为命名工具</summary>
    /// <param name="name">工具名称</param>
    /// <param name="handler">处理委托，参数为 JSON 字符串，返回 JSON 字符串结果</param>
    /// <param name="description">工具功能描述（可选）</param>
    public void AddTool(String name, Func<String?, ToolCallContext?, CancellationToken, Task<String>> handler, String? description = null)
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
    public void AddTools<T>()
    {
        var instance = ServiceProvider?.CreateInstance(typeof(T)) ?? Activator.CreateInstance<T>();
        AddToolsFromInstance(typeof(T), instance!);
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

    #region 内置工具同步

    /// <summary>获取类型上所有标注 <see cref="ToolDescriptionAttribute"/> 的公开实例方法</summary>
    /// <param name="type">工具服务类型</param>
    /// <returns>方法列表</returns>
    public static IList<MethodInfo> GetToolMethods(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();
    }

    /// <summary>描述单个工具方法，并将工具名、显示名、参数Schema、触发词等信息写入目标实体</summary>
    /// <param name="type">工具服务类型</param>
    /// <param name="method">工具方法</param>
    /// <param name="model">待填充的内置工具实体</param>
    public static void DescribeMethod(Type type, MethodInfo method, INativeTool model)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (method == null) throw new ArgumentNullException(nameof(method));
        if (model == null) throw new ArgumentNullException(nameof(model));

        var chatTool = ToolSchemaBuilder.BuildFromMethod(method);
        var function = chatTool.Function;
        var toolName = function?.Name;
        if (toolName.IsNullOrEmpty()) throw new InvalidOperationException($"无法从方法 {type.FullName}.{method.Name} 解析工具名称");

        if (function == null) throw new InvalidOperationException($"方法 {type.FullName}.{method.Name} 未生成函数定义");

        var description = function.Description;
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()
            ?? throw new InvalidOperationException($"方法 {type.FullName}.{method.Name} 缺少 ToolDescriptionAttribute");

        model.Name = toolName;
        model.DisplayName = ResolveDisplayName(method, description, toolName);
        model.Description = description;
        model.Parameters = function.Parameters?.ToJson();
        model.Triggers = NormalizeTriggers(attr.Triggers);
        model.AssistantTriggers = NormalizeTriggers(attr.AssistantTriggers);
        model.IsSystem = attr.IsSystem;
        model.Enable = attr.Enable;
        model.ClassName = type.FullName;
        model.MethodName = method.Name;
    }

    /// <summary>规范化触发词字符串，去重并统一使用英文逗号连接</summary>
    /// <param name="triggers">原始触发词文本</param>
    /// <returns>规范化后的触发词文本</returns>
    public static String? NormalizeTriggers(String? triggers)
    {
        if (triggers.IsNullOrWhiteSpace()) return null;

        var words = triggers.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !String.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return words.Length == 0 ? null : String.Join(",", words);
    }

    /// <summary>解析显示名称。优先级：DisplayNameAttribute &gt; 描述首句（中文句号前）&gt; 工具名</summary>
    /// <param name="method">工具方法</param>
    /// <param name="description">工具描述</param>
    /// <param name="toolName">工具名称</param>
    /// <returns>显示名称</returns>
    public static String ResolveDisplayName(MethodInfo method, String? description, String toolName)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        if (String.IsNullOrEmpty(toolName)) throw new ArgumentNullException(nameof(toolName));

        var displayName = method.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
        if (!displayName.IsNullOrEmpty()) return displayName;
        if (!String.IsNullOrEmpty(description))
        {
            var value = description!;
            var idx = value.IndexOf('。');
            if (idx > 0) return value[..idx];
        }

        return toolName;
    }

    /// <summary>同步内置工具元数据到业务表。扫描已注册工具类型并按规则写入实体表</summary>
    /// <typeparam name="TNativeTool">内置工具实体类型</typeparam>
    /// <param name="findByName">按工具名称查找实体的方法</param>
    /// <param name="save">保存实体的方法</param>
    /// <param name="onError">错误回调。单个工具同步失败时触发，不中断后续同步</param>
    /// <returns>处理的工具数量</returns>
    public Int32 SyncNativeTools<TNativeTool>(Func<String, TNativeTool?> findByName, Action<TNativeTool> save, Action<Exception>? onError = null)
        where TNativeTool : class, INativeTool, new()
    {
        if (findByName == null) throw new ArgumentNullException(nameof(findByName));
        if (save == null) throw new ArgumentNullException(nameof(save));

        var count = 0;
        foreach (var type in _registeredTypes)
        {
            var methods = GetToolMethods(type);
            foreach (var method in methods)
            {
                try
                {
                    SyncNativeToolMethod(type, method, findByName, save);
                    count++;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            }
        }

        return count;
    }

    /// <summary>将单个工具方法的信息同步到内置工具表</summary>
    /// <typeparam name="TNativeTool">内置工具实体类型</typeparam>
    /// <param name="type">工具服务类型</param>
    /// <param name="method">工具方法</param>
    /// <param name="findByName">按工具名称查找实体的方法</param>
    /// <param name="save">保存实体的方法</param>
    private static void SyncNativeToolMethod<TNativeTool>(Type type, MethodInfo method, Func<String, TNativeTool?> findByName, Action<TNativeTool> save)
        where TNativeTool : class, INativeTool, new()
    {
        var model = new TNativeTool();
        DescribeMethod(type, method, model);

        var toolName = model.Name;
        if (toolName.IsNullOrEmpty()) throw new InvalidOperationException($"无法从方法 {type.FullName}.{method.Name} 解析工具名称");

        var existing = findByName(toolName);
        var isNew = existing == null;
        var record = existing ?? new TNativeTool
        {
            Name = toolName,
            Enable = model.Enable,
            IsLocked = false,
        };

        // 显式禁用的内置工具在同步时强制关闭，避免被误触发或误暴露
        if (!model.Enable) record.Enable = false;

        var displayNameAttr = method.GetCustomAttribute<DisplayNameAttribute>();
        // 新增记录时初始化 DisplayName；或存在明确的 [DisplayName] 标注且未锁定时更新
        if (isNew || (!record.IsLocked && displayNameAttr != null))
            record.DisplayName = model.DisplayName;

        // 始终更新类/方法定位信息
        record.ClassName = model.ClassName;
        record.MethodName = model.MethodName;

        // 未锁定时才更新描述和参数，保护手工调整的内容
        if (!record.IsLocked)
        {
            record.IsSystem = model.IsSystem;
            record.Description = model.Description;
            record.Parameters = model.Parameters;
            record.Triggers = model.Triggers;
            record.AssistantTriggers = model.AssistantTriggers;
        }

        save(record);
    }

    #endregion

    #region 调用分发

    /// <summary>根据工具名称和 JSON 参数调用已注册的工具处理器</summary>
    /// <param name="name">工具名称（大小写不敏感）</param>
    /// <param name="arguments">JSON 格式的参数字符串</param>
    /// <param name="context">调用上下文，可为 null</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果的 JSON 字符串</returns>
    /// <exception cref="KeyNotFoundException">工具名称未注册</exception>
    public Task<String> InvokeAsync(String name, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(name, out var handler))
            throw new KeyNotFoundException($"工具 '{name}' 未注册到 ToolRegistry");
        return handler(arguments, context, cancellationToken);
    }

    /// <summary>尝试调用工具，工具未注册或执行出错时返回错误描述（不抛异常）</summary>
    /// <param name="name">工具名称</param>
    /// <param name="arguments">JSON 格式参数</param>
    /// <param name="context">调用上下文，可为 null</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果 JSON 字符串，或错误描述字符串</returns>
    public async Task<String> TryInvokeAsync(String name, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(name, out var handler))
            return $"{{\"error\":\"tool '{name}' not registered\"}}";
        try
        {
            return await handler(arguments, context, cancellationToken).ConfigureAwait(false);
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
        if (!_registeredTypes.Contains(type))
            _registeredTypes.Add(type);

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

        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>();
        if (attr is { IsSystem: true })
            _systemNames.Add(toolName);

        _tools.Add(tool);
        _handlers[toolName] = (args, ctx, ct) => InvokeMethodAsync(method, instance, args, ctx, ct);
    }

    private static async Task<String> InvokeMethodAsync(MethodInfo method, Object instance, String? arguments, ToolCallContext? context, CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken) && p.ParameterType != typeof(ToolCallContext))
            .ToArray();

        Object?[] args;
        if (parameters.Length == 0 || arguments.IsNullOrWhiteSpace())
            args = BuildDefaultArgs(method);
        else
            args = DeserializeArguments(parameters, arguments);

        // 将所有 CancellationToken / ToolCallContext 参数替换为传入实例
        var allParams = method.GetParameters();
        var finalArgs = new Object?[allParams.Length];
        var argIdx = 0;
        for (var i = 0; i < allParams.Length; i++)
        {
            if (allParams[i].ParameterType == typeof(CancellationToken))
                finalArgs[i] = cancellationToken;
            else if (allParams[i].ParameterType == typeof(ToolCallContext))
                finalArgs[i] = context;
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
        var nonCt = method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken) && p.ParameterType != typeof(ToolCallContext)).ToArray();
        var defaults = new Object?[nonCt.Length];
        for (var i = 0; i < nonCt.Length; i++)
            defaults[i] = nonCt[i].HasDefaultValue ? nonCt[i].DefaultValue : null;
        return defaults;
    }

    private static Object?[] DeserializeArguments(ParameterInfo[] parameters, String arguments)
    {
        var result = new Object?[parameters.Length];
        IDictionary<String, Object?>? parsed;
        try
        {
            parsed = JsonParser.Decode(arguments);
        }
        catch
        {
            // 参数 JSON 格式异常（如流式截断导致不完整 JSON，或 LLM 输出含匿名对象等畸形结构）
            // 后备策略：按参数名逐一提取字段原始 JSON，放宽整体解析要求
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.Name == null) continue;
                var rawJson = TryExtractFieldJson(arguments, p.Name);
                if (rawJson != null)
                {
                    try
                    {
                        var val = JsonParser.Decode(rawJson);
                        result[i] = ConvertValue(val ?? (Object?)rawJson, p.ParameterType);
                    }
                    catch
                    {
                        if (p.HasDefaultValue) result[i] = p.DefaultValue;
                    }
                }
                else if (p.HasDefaultValue)
                {
                    result[i] = p.DefaultValue;
                }
            }
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

        if (underlyingType == typeof(String))
        {
            // LLM 可能将 JSON 对象/数组直接内联传入 String 参数，将其序列化为 JSON 字符串
            if (value is IDictionary<String, Object?> || value is IList<Object?>)
                return value.ToJson();
            return value.ToString();
        }
        if (underlyingType == typeof(Boolean)) return Convert.ToBoolean(value);
        if (underlyingType == typeof(Int32)) return Convert.ToInt32(value);
        if (underlyingType == typeof(Int64)) return Convert.ToInt64(value);
        if (underlyingType == typeof(Double)) return Convert.ToDouble(value);
        if (underlyingType == typeof(Single)) return Convert.ToSingle(value);
        if (underlyingType == typeof(Decimal)) return Convert.ToDecimal(value);
        if (underlyingType.IsEnum) return Enum.Parse(underlyingType, value.ToString() ?? String.Empty, ignoreCase: true);

        // 复杂类型：序列化回 JSON 再反序列化为目标类型
        if (value is IDictionary<String, Object?> || value is IList<Object?>)
            return JsonHelper.Default.Convert(value, underlyingType);

        // Qwen3.6 兼容：复杂类型参数收到 JSON 字符串时（模型将对象序列化为字符串传入），尝试二次解析后转换
        // 约 15% 概率出现，直接返回字符串会导致类型不符、工具参数错误
        if (value is String jsonStr)
        {
            try
            {
                var reparsed = JsonParser.Decode(jsonStr);
                if (reparsed is IDictionary<String, Object?> || reparsed is IList<Object?>)
                    return JsonHelper.Default.Convert(reparsed, underlyingType);
            }
            catch { }
        }

        return value;
    }

    /// <summary>从可能格式损坏的 JSON 字符串中按字段名提取字段值的原始 JSON 表示</summary>
    /// <remarks>用于 LLM 生成含匿名对象等畸形结构时的后备解析</remarks>
    private static String? TryExtractFieldJson(String? json, String fieldName)
    {
        if (json.IsNullOrWhiteSpace()) return null;
        // 查找 "fieldName" :
        var key = $"\"{fieldName}\"";
        var pos = json.IndexOf(key, StringComparison.Ordinal);
        if (pos < 0) return null;
        pos += key.Length;
        // 跳过空白
        while (pos < json.Length && json[pos] is ' ' or '\t' or '\n' or '\r') pos++;
        if (pos >= json.Length || json[pos] != ':') return null;
        pos++;
        // 跳过空白
        while (pos < json.Length && json[pos] is ' ' or '\t' or '\n' or '\r') pos++;
        if (pos >= json.Length) return null;
        return ExtractJsonValueAt(json, pos);
    }

    /// <summary>从指定位置提取一个完整 JSON 值（字符串/数字/布尔/对象/数组）</summary>
    private static String? ExtractJsonValueAt(String json, Int32 start)
    {
        if (start >= json.Length) return null;
        var c = json[start];
        if (c == '"')
        {
            // 字符串：找结束引号（跳过转义）
            var end = start + 1;
            while (end < json.Length)
            {
                if (json[end] == '\\') { end += 2; continue; }
                if (json[end] == '"') { end++; break; }
                end++;
            }
            return json.Substring(start, end - start);
        }
        if (c is '{' or '[')
        {
            // 对象/数组：深度跟踪找结束符
            var depth = 0;
            var inStr = false;
            var esc = false;
            var end = start;
            while (end < json.Length)
            {
                var ch = json[end];
                if (esc) { esc = false; end++; continue; }
                if (ch == '\\' && inStr) { esc = true; end++; continue; }
                if (ch == '"') { inStr = !inStr; end++; continue; }
                if (!inStr)
                {
                    if (ch is '{' or '[') depth++;
                    else if (ch is '}' or ']') { depth--; if (depth == 0) { end++; break; } }
                }
                end++;
            }
            return json.Substring(start, end - start);
        }
        else
        {
            // 数字/布尔/null：遇到分隔符停止
            var end = start;
            while (end < json.Length && json[end] is not (',' or '}' or ']' or ' ' or '\t' or '\n' or '\r'))
                end++;
            return json.Substring(start, end - start);
        }
    }

    #endregion

    #region IToolProvider

    IList<ChatTool> IToolProvider.GetTools(ISet<String>? filterNames, Boolean includeSystem)
    {
        var query = _tools.AsEnumerable();
        if (!includeSystem)
            query = query.Where(t => t.Function?.Name is not null && !_systemNames.Contains(t.Function.Name));
        if (filterNames != null && filterNames.Count > 0)
            query = query.Where(t => t.Function?.Name != null && filterNames.Contains(t.Function.Name));
        return [.. query];
    }

    async Task<IToolResult> IToolProvider.CallToolAsync(String toolName, String? arguments, ToolCallContext? context, CancellationToken cancellationToken)
    {
        var result = await InvokeAsync(toolName, arguments, context, cancellationToken).ConfigureAwait(false);
        return new ToolResult(result);
    }

    #endregion
}
