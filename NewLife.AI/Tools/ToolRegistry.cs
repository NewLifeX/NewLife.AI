using System.ComponentModel;
using System.Reflection;
using NewLife.AI.Models;
using NewLife.AI.Interfaces;
using NewLife.Log;
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
/// <b>线程契约</b>：注册应在服务启动阶段（并发请求到达前）完成——内部集合（工具列表/处理器/别名/系统名）在注册后为只读并发读安全；
/// 运行时动态注册（AddTool/AddTools）与并发请求同时发生存在竞态，非受支持场景。同名工具重复注册时保留首次注册并记录警告日志。
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
    private readonly Dictionary<String, String> _aliases = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>注册工具名别名。LLM 调用别名时路由到目标工具；别名仅服务端 fallback，不进 LLM Schema</summary>
    /// <param name="alias">别名</param>
    /// <param name="target">目标工具名</param>
    public void AddToolAlias(String alias, String target)
    {
        if (String.IsNullOrWhiteSpace(alias)) throw new ArgumentNullException(nameof(alias));
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        _aliases[alias] = target;
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
                    .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>(true) != null)
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
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>(true) != null)
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
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>(true)
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
        if (!TryGetHandler(name, out var handler))
            throw new KeyNotFoundException(BuildUnknownToolMessage(name));
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
        if (!TryGetHandler(name, out var handler))
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

    /// <summary>解析工具处理器。优先直接命中，其次工具名别名路由（服务端 fallback，不进 LLM Schema）</summary>
    /// <param name="name">工具名或别名</param>
    /// <param name="handler">解析到的处理器</param>
    /// <returns>命中返回 true</returns>
    private Boolean TryGetHandler(String name, out Func<String?, ToolCallContext?, CancellationToken, Task<String>> handler)
    {
        if (_handlers.TryGetValue(name, out handler!)) return true;

        if (_aliases.TryGetValue(name, out var target) && _handlers.TryGetValue(target, out handler!))
            return true;

        handler = null!;
        return false;
    }

    /// <summary>构建未注册工具的异常消息。附带相近工具名建议与可用工具列表，便于 LLM 自行纠正</summary>
    /// <param name="name">未注册的工具名</param>
    /// <returns>错误消息</returns>
    private String BuildUnknownToolMessage(String name)
    {
        var candidate = FindSimilarToolName(name);
        var msg = $"工具 '{name}' 未注册到 ToolRegistry";
        if (!candidate.IsNullOrEmpty())
            msg += $"，是否想调用 '{candidate}'？";

        var all = _handlers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        if (all.Count > 0)
        {
            var list = all.Count > 30 ? String.Join(", ", all.Take(30)) + " 等" : String.Join(", ", all);
            msg += $" 可用工具: {list}";
        }
        return msg;
    }

    /// <summary>查找与指定名称最相近的已注册工具名。优先级：忽略大小写精确 > 前缀 > 反向前缀 > 包含 > 反向包含</summary>
    /// <param name="name">目标名称</param>
    /// <returns>相近工具名；不存在返回 null</returns>
    private String? FindSimilarToolName(String name)
    {
        return _handlers.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? _handlers.Keys.FirstOrDefault(k => k.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? _handlers.Keys.FirstOrDefault(k => name.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            ?? _handlers.Keys.FirstOrDefault(k => k.Contains(name, StringComparison.OrdinalIgnoreCase))
            ?? _handlers.Keys.FirstOrDefault(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 辅助

    private void AddToolsFromInstance(Type type, Object instance)
    {
        if (!_registeredTypes.Contains(type))
            _registeredTypes.Add(type);

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>(true) != null);
        foreach (var method in methods)
            RegisterMethod(method, instance);
    }

    private void RegisterMethod(MethodInfo method, Object instance)
    {
        var tool = ToolSchemaBuilder.BuildFromMethod(method);
        var toolName = tool.Function!.Name;

        // 已注册则跳过不覆盖（保护首次注册）。静默丢弃会掩盖名称冲突，记录警告便于排查
        if (_handlers.ContainsKey(toolName))
        {
            XTrace.WriteLine("[ToolRegistry] 工具 {0} 已注册，跳过重复注册（来源：{1}.{2}）", toolName, method.DeclaringType?.FullName, method.Name);
            return;
        }

        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>(true);
        if (attr is { IsSystem: true })
            _systemNames.Add(toolName);

        _tools.Add(tool);
        _handlers[toolName] = (args, ctx, ct) => InvokeMethodAsync(method, instance, args, ctx, ct);
    }

    private static async Task<String> InvokeMethodAsync(MethodInfo method, Object instance, String? arguments, ToolCallContext? context, CancellationToken cancellationToken)
    {
        // 一次获取全部参数并过滤出业务参数（GetParameters 是反射开销，避免对同一 method 重复调用）
        var allParams = method.GetParameters();
        var parameters = allParams
            .Where(p => p.ParameterType != typeof(CancellationToken) && p.ParameterType != typeof(ToolCallContext))
            .ToArray();

        Object?[] args;
        if (parameters.Length == 0 || arguments.IsNullOrWhiteSpace())
            args = BuildDefaultArgs(method);
        else
            args = DeserializeArguments(parameters, arguments);

        // 将所有 CancellationToken / ToolCallContext 参数替换为传入实例
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
        if (result is ValueTask<String> valueTaskStr)
            return await valueTaskStr.AsTask().ConfigureAwait(false);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            // Task<T>：通过反射获取 Result 属性
            var resultProp = result.GetType().GetProperty("Result");
            result = resultProp?.GetValue(result);
        }
        else if (result.GetType().IsGenericType && result.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            // 泛型 ValueTask<T>：AsTask() 转 Task<T> 后 await，再读 Result（A-73）
            var asTask = (Task)result.GetType().GetMethod("AsTask")!.Invoke(result, null)!;
            await asTask.ConfigureAwait(false);
            var resultProp = asTask.GetType().GetProperty("Result");
            result = resultProp?.GetValue(asTask);
        }
        if (result == null) return "null";

        // IToolResult：存入上下文供 CallToolAsync 直接使用，本方法返回 LLM 受众内容（兼容 InvokeAsync 调用者）
        if (result is IToolResult toolResult)
        {
            context?.ToolResult = toolResult;

            var llmContent = toolResult.Contents
                .Where(c => c.Audience.HasFlag(ToolAudience.Llm))
                .Select(c => c.Data)
                .Join("\n");
            return llmContent.IsNullOrEmpty() ? "工具已执行" : llmContent;
        }

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

        // Phase 1: 标准 JSON 解析
        IDictionary<String, Object?>? parsed;
        try
        {
            parsed = JsonParser.Decode(arguments);
        }
        catch (Exception ex)
        {
            // Phase 2: 后备 — 从格式异常的原始字符串中手动提取
            XTrace.WriteLine("[ToolRegistry] JSON 解析失败，Length={0}，错误：{1}，参数前200字符：{2}",
                arguments?.Length ?? 0, ex.Message, arguments?.Substring(0, Math.Min(arguments?.Length ?? 0, 200)));
            ExtractFromRawString(arguments, parameters, result);
            return result;
        }
        if (parsed == null) return result;

        // Phase 1: 从已解析的字典中精确匹配 → 别名匹配 → 默认值
        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.Name == null) continue;

            if (parsed.TryGetValue(p.Name, out var value))
                result[i] = ConvertValue(value, p.ParameterType);
            else if (TryMatchAlias(parsed, p, out var aliasValue))
                result[i] = aliasValue;
            else if (p.HasDefaultValue)
                result[i] = p.DefaultValue;
            // 无默认值的必需参数保持 null，由工具方法自行校验
        }
        return result;
    }

    /// <summary>从已解析的字典中尝试按别名匹配参数值</summary>
    /// <param name="parsed">已解析的 JSON 参数字典</param>
    /// <param name="p">参数信息</param>
    /// <param name="value">匹配成功时输出转换后的值</param>
    /// <returns>是否找到匹配的别名</returns>
    private static Boolean TryMatchAlias(IDictionary<String, Object?> parsed, ParameterInfo p, out Object? value)
    {
        value = null;
        var attr = p.GetCustomAttribute<ParameterAliasAttribute>();
        if (attr == null) return false;

        foreach (var alias in attr.Aliases)
        {
            if (parsed.TryGetValue(alias, out var v))
            {
                value = ConvertValue(v, p.ParameterType);
                return true;
            }
        }
        return false;
    }

    /// <summary>从格式异常的原始 JSON 字符串中按字段名逐一提取参数值（后备路径）</summary>
    private static void ExtractFromRawString(String? json, ParameterInfo[] parameters, Object?[] result)
    {
        if (json.IsNullOrWhiteSpace()) return;

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.Name == null) continue;

            // 尝试精确匹配
            if (TryDecodeField(json, p.Name!, p.ParameterType, out var val))
            {
                result[i] = val;
                continue;
            }

            // 尝试别名匹配
            var attr = p.GetCustomAttribute<ParameterAliasAttribute>();
            if (attr != null)
            {
                foreach (var alias in attr.Aliases)
                {
                    if (TryDecodeField(json, alias, p.ParameterType, out val))
                    {
                        result[i] = val;
                        break;
                    }
                }
            }

            // 都不匹配时使用默认值
            if (result[i] == null && p.HasDefaultValue)
                result[i] = p.DefaultValue;
        }
    }

    /// <summary>从原始 JSON 字符串中提取指定字段的值并解析为目标类型。
    /// 简单类型先 Decode 再 Convert，避免 raw 字符串带引号；
    /// 复杂类型走 ConvertValue 的 Qwen JSON 字符串分支。</summary>
    private static Boolean TryDecodeField(String? json, String fieldName, Type targetType, out Object? value)
    {
        value = null;
        var raw = TryExtractFieldJson(json, fieldName);
        if (raw == null) return false;

        // 简单类型（String/数值/布尔/枚举）：必须先 Decode 去除 JSON 包裹（引号等）
        if (IsSimpleType(targetType))
        {
            try
            {
                var decoded = JsonParser.Decode(raw);
                value = ConvertValue(decoded ?? (Object?)raw, targetType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 复杂类型：ConvertValue 内部有 Qwen 兼容的 JSON 字符串→对象转换
        value = ConvertValue(raw, targetType);
        return true;
    }

    /// <summary>判断类型是否为简单类型。复杂类型由 ConvertValue 的 Qwen 分支处理 JSON 字符串→对象转换</summary>
    private static Boolean IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(String) ||
               underlying == typeof(Boolean) ||
               underlying == typeof(Int32) ||
               underlying == typeof(Int64) ||
               underlying == typeof(Int16) ||
               underlying == typeof(Byte) ||
               underlying == typeof(Double) ||
               underlying == typeof(Single) ||
               underlying == typeof(Decimal) ||
               underlying.IsEnum;
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
        // 使用 NewLife 安全扩展方法：ToInt/ToBoolean 等内部基于 TryParse，不抛异常，失败返回默认值
        if (underlyingType == typeof(Boolean)) return value.ToBoolean();
        if (underlyingType == typeof(Int32)) return value.ToInt();
        if (underlyingType == typeof(Int64)) return value.ToLong();
        if (underlyingType == typeof(Int16)) return (Int16)value.ToInt();
        if (underlyingType == typeof(Byte)) return (Byte)value.ToInt();
        if (underlyingType == typeof(Double)) return value.ToDouble();
        if (underlyingType == typeof(Single)) return (Single)value.ToDouble();
        if (underlyingType == typeof(Decimal)) return value.ToDecimal();
        if (underlyingType.IsEnum)
        {
            try { return Enum.Parse(underlyingType, value.ToString() ?? String.Empty, ignoreCase: true); }
            catch { return null; }
        }

        // 复杂类型：序列化回 JSON 再反序列化为目标类型
        if (value is IDictionary<String, Object?> || value is IList<Object?>)
            return JsonHelper.Default.Convert(value, underlyingType);

        // Qwen3.6 兼容：复杂类型参数收到 JSON 字符串时（模型将对象序列化为字符串传入），尝试二次解析后转换
        // 约 15% 概率出现，直接返回字符串会导致类型不符、工具参数错误
        if (value is String jsonStr)
        {
            // new JsonParser().Decode() 支持解析顶层 JSON 数组，而静态 JsonParser.Decode 只支持对象
            if (TryDecodeJsonString(jsonStr, underlyingType, out var converted)) return converted;

            // LLM 常见 JSON-in-JSON 转义错误（元素间多余引号/二次转义/包裹引号）：修复后二次解析
            // 典型失败：items=[{"date":"2017"...},"{"date":"2018"...}] 导致 'd' is invalid after a value
            if (ToolHelper.TryRepairJson(jsonStr, out var repaired) && TryDecodeJsonString(repaired, underlyingType, out converted))
                return converted;
        }

        return value;
    }

    /// <summary>将 JSON 字符串二次解析并转换为目标复杂类型（Qwen 兼容分支）。成功返回 true</summary>
    /// <param name="json">待解析的 JSON 字符串</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="value">转换后的值</param>
    /// <returns>解析并转换成功返回 true</returns>
    private static Boolean TryDecodeJsonString(String json, Type targetType, out Object? value)
    {
        value = null;
        // 先做严格结构校验，避免宽松解析器放行畸形 JSON 导致部分数据静默丢失（如 },"{ 多引号）
        if (!ToolHelper.IsStrictJson(json)) return false;
        try
        {
            // new JsonParser().Decode() 支持解析顶层 JSON 数组，而静态 JsonParser.Decode 只支持对象
            var reparsed = new JsonParser(json).Decode();
            if (reparsed is IDictionary<String, Object?> || reparsed is IList<Object?>)
            {
                value = JsonHelper.Default.Convert(reparsed, targetType);
                return true;
            }
        }
        catch { }
        return false;
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
        // 过滤语义（与 IToolProvider 契约一致）：
        // filterNames=null 返回全量；空集合仅返回系统工具；非空集合返回系统工具 + 指定名称工具
        // includeSystem=false 时始终排除系统工具（覆盖以上过滤结果）
        var query = _tools.AsEnumerable();
        if (filterNames != null)
        {
            if (filterNames.Count == 0)
                query = query.Where(t => t.Function?.Name != null && _systemNames.Contains(t.Function.Name));
            else
                query = query.Where(t => t.Function?.Name != null && (_systemNames.Contains(t.Function.Name) || filterNames.Contains(t.Function.Name)));
        }
        if (!includeSystem)
            query = query.Where(t => t.Function?.Name is not null && !_systemNames.Contains(t.Function.Name));
        return [.. query];
    }

    async Task<IToolResult> IToolProvider.CallToolAsync(String toolName, String? arguments, ToolCallContext? context, CancellationToken cancellationToken)
    {
        var result = await InvokeAsync(toolName, arguments, context, cancellationToken).ConfigureAwait(false);

        // 检查上下文：若工具方法返回了 IToolResult，直接使用（保留受众分离），避免重复包装为 Both
        if (context?.ToolResult is { } toolResult)
            return toolResult;

        return new ToolResult(result);
    }

    #endregion
}
