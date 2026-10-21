using System.Collections;
using NewLife.AI.Models;

namespace NewLife.AI.Tools;

/// <summary>工具 Schema 本地化。把工具描述与参数描述按当前界面语言翻译后再提交 LLM，缺少译文时回退源文</summary>
/// <remarks>
/// 工具方法上的 <c>[Description]</c> 与参数上的 <c>[Description]</c> 以英文为源文，译文由各程序集的 <c>Locales/*.yml</c> 提供，
/// 运行时经 <see cref="Localization.Translate(String)"/> 查表。翻译发生在每次请求组装工具列表时
/// （<see cref="ToolChatClient"/> 与 <see cref="FunctionCallingPlanner"/>），不修改注册表中的原始定义，
/// 因此同一进程内不同请求（不同 <c>CurrentUICulture</c>）可以使用不同语言。
/// <para>只翻译 JSON Schema 中的 <c>description</c> 节点：工具名、参数名、枚举值属于调用契约，始终原样提交。</para>
/// <code>
/// // 默认按当前界面语言查词表，缺少译文回退英文源文；可替换为机器翻译等自定义实现
/// ToolSchemaLocalizer.Translator = text =&gt; Localization.Translate(text);
/// ToolSchemaLocalizer.Enabled = false;   // 需要始终提交英文源文时关闭
/// </code>
/// </remarks>
public static class ToolSchemaLocalizer
{
    #region 属性
    /// <summary>是否启用本地化。默认 true；关闭后工具定义按源文原样提交</summary>
    public static Boolean Enabled { get; set; } = true;

    /// <summary>翻译委托。默认按当前界面语言查询编译期词表，缺少译文时返回原文；可替换为机器翻译等自定义实现</summary>
    /// <remarks>
    /// 委托每次请求都会按工具数量调用若干次，有网络开销的实现应在委托内部自行缓存。
    /// 委托抛出的异常会被吞掉并回退源文，避免翻译故障影响对话。
    /// </remarks>
    public static Func<String, String> Translator { get; set; } = L.T;
    #endregion

    #region 方法
    /// <summary>翻译单段工具文本（工具描述、参数描述、工具目录摘要等）。本地化关闭、缺少译文或翻译失败时返回源文</summary>
    /// <param name="text">英文源文</param>
    /// <returns>译文；未命中译文时返回源文</returns>
    public static String? TranslateText(String? text) => Enabled ? Translate(text) : text;

    /// <summary>本地化工具定义列表。没有可翻译内容时复用原实例，返回值始终为新列表</summary>
    /// <param name="tools">工具定义列表</param>
    /// <returns>本地化后的工具定义列表</returns>
    public static List<ChatTool> Localize(IEnumerable<ChatTool>? tools)
    {
        var list = new List<ChatTool>();
        if (tools == null) return list;

        foreach (var item in tools)
        {
            if (item == null) continue;
            list.Add(Localize(item));
        }

        return list;
    }

    /// <summary>本地化单个工具定义。没有可翻译内容时返回原实例</summary>
    /// <param name="tool">工具定义</param>
    /// <returns>本地化后的工具定义；未命中译文时返回原实例</returns>
    public static ChatTool Localize(ChatTool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        if (!Enabled) return tool;

        var function = tool.Function;
        if (function == null) return tool;

        var description = Translate(function.Description);
        var parameters = LocalizeSchema(function.Parameters);
        if (ReferenceEquals(description, function.Description) && ReferenceEquals(parameters, function.Parameters)) return tool;

        return new ChatTool
        {
            Type = tool.Type,
            Function = new FunctionDefinition
            {
                Name = function.Name,
                Description = description,
                Parameters = parameters,
            },
            Mcp = tool.Mcp,
            Config = tool.Config,
        };
    }
    #endregion

    #region 辅助
    /// <summary>翻译 JSON Schema 中的 description 节点。未命中译文时返回原对象</summary>
    /// <param name="value">Schema 节点</param>
    /// <returns>翻译后的节点；没有变化时返回原对象</returns>
    private static Object? LocalizeSchema(Object? value)
    {
        if (value is IDictionary<String, Object?> dic) return LocalizeDictionary(dic);
        if (value is IDictionary table) return LocalizeDictionary(table);
        if (value is IList<Object?> list) return LocalizeList(list);
        if (value is IList array) return LocalizeList(array);

        return value;
    }

    private static Object? LocalizeDictionary(IDictionary<String, Object?> dic)
    {
        var changed = false;
        var entries = new List<KeyValuePair<String, Object?>>(dic.Count);
        foreach (var item in dic)
        {
            // description 是自然语言说明，需要翻译；属性名与枚举值是契约，保持原样
            var child = item.Key == "description" && item.Value is String text ? Translate(text) : LocalizeSchema(item.Value);
            if (!ReferenceEquals(child, item.Value)) changed = true;
            entries.Add(new KeyValuePair<String, Object?>(item.Key, child));
        }
        if (!changed) return dic;

        var copy = new Dictionary<String, Object?>(entries.Count, StringComparer.Ordinal);
        foreach (var item in entries) copy[item.Key] = item.Value;
        return copy;
    }

    private static Object? LocalizeDictionary(IDictionary dic)
    {
        var changed = false;
        var entries = new List<KeyValuePair<String, Object?>>(dic.Count);
        foreach (DictionaryEntry item in dic)
        {
            var key = item.Key as String;
            if (key == null) return dic;

            var child = key == "description" && item.Value is String text ? Translate(text) : LocalizeSchema(item.Value);
            if (!ReferenceEquals(child, item.Value)) changed = true;
            entries.Add(new KeyValuePair<String, Object?>(key, child));
        }
        if (!changed) return dic;

        var copy = new Dictionary<String, Object?>(entries.Count, StringComparer.Ordinal);
        foreach (var item in entries) copy[item.Key] = item.Value;
        return copy;
    }

    private static Object? LocalizeList(IList<Object?> list)
    {
        var changed = false;
        var items = new List<Object?>(list.Count);
        foreach (var item in list)
        {
            var child = LocalizeSchema(item);
            if (!ReferenceEquals(child, item)) changed = true;
            items.Add(child);
        }
        if (!changed) return list;

        return items;
    }

    private static Object? LocalizeList(IList list)
    {
        var changed = false;
        var items = new List<Object?>(list.Count);
        foreach (var item in list)
        {
            var child = LocalizeSchema(item);
            if (!ReferenceEquals(child, item)) changed = true;
            items.Add(child);
        }
        if (!changed) return list;

        return items;
    }

    /// <summary>翻译单段文本。空值与翻译失败均返回原文</summary>
    /// <param name="text">源文</param>
    /// <returns>译文；缺少译文或翻译失败时返回源文</returns>
    private static String? Translate(String? text)
    {
        if (text.IsNullOrEmpty()) return text;

        var translator = Translator;
        if (translator == null) return text;

        try
        {
            return translator(text!);
        }
        catch
        {
            // 翻译不可用不应影响对话，回退源文
            return text;
        }
    }
    #endregion
}
