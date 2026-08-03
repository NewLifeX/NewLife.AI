using System.Runtime.Serialization;
using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.AI.Models;

/// <summary>对话消息</summary>
public class ChatMessage : IExtend
{
    #region 属性
    /// <summary>角色。system/user/assistant/tool</summary>
    public String Role { get; set; } = null!;

    /// <summary>内容。文本内容或多模态内容数组</summary>
    public Object? Content { get; set; }

    /// <summary>名称。函数调用时的函数名</summary>
    public String? Name { get; set; }

    /// <summary>工具调用列表。assistant 角色发起的工具调用</summary>
    public IList<ToolCall>? ToolCalls { get; set; }

    /// <summary>工具调用编号。tool 角色回传时关联的调用编号</summary>
    public String? ToolCallId { get; set; }

    /// <summary>思考内容。部分模型返回的推理链路（reasoning_content）</summary>
    public String? ReasoningContent { get; set; }

    /// <summary>扩展数据。用于在中间件管道中传递非结构化的自定义上下文（如 Anthropic 思考签名、redacted_thinking 数据等协议专属元数据，供多轮对话原样回传）</summary>
    [IgnoreDataMember]
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据。读取时 Items 为 null 返回 null；写入时自动创建，防止空异常</summary>
    [IgnoreDataMember]
    public Object? this[String key]
    {
        get => Items != null && Items.TryGetValue(key, out var value) ? value : null;
        set => (Items ??= new Dictionary<String, Object?>())[key] = value;
    }

    /// <summary>类型化内容片段列表（MEAI 兼容）。非空时优先于 <see cref="Content"/> 使用，支持多模态消息</summary>
    /// <remarks>
    /// 与 <see cref="Content"/>（Object?）的关系：两者并存以保持向后兼容。
    /// 新代码建议使用 Contents 以获得更强的类型安全性；旧代码无需修改。
    /// 序列化时不输出此属性：由各协议客户端在构建请求时将 Contents 转为协议格式赋值给 Content。
    /// </remarks>
    [IgnoreDataMember]
    public IList<AIContent>? Contents { get; set; }
    #endregion

    #region 方法
    /// <summary>确保多模态内容已解析。当 Contents 为空但 Content 是复杂对象（非字符串）时，尝试按 OpenAI 格式解析为类型化内容列表</summary>
    /// <remarks>
    /// 典型场景：网关接收 OpenAI 格式请求后 Content 被反序列化为 IList/JsonElement，
    /// 而 Contents（[IgnoreDataMember]）为 null。转发到其它协议前需先调用此方法还原。
    /// </remarks>
    public void ResolveContents()
    {
        if (Contents != null && Contents.Count > 0) return;
        if (Content == null || Content is String) return;

        var contents = ParseMultimodalContent(Content);
        if (contents != null && contents.Count > 0)
            Contents = contents;
    }

    /// <summary>尝试将 OpenAI 格式的多模态内容数组解析为 AIContent 列表</summary>
    /// <param name="content">Content 值，可能是 IList（SystemJson 转换后）或 JsonElement 等</param>
    /// <returns>解析成功返回 AIContent 列表，否则返回 null</returns>
    public static IList<AIContent>? ParseMultimodalContent(Object content)
    {
        IList<Object>? items = null;

        // NewLife SystemJson 转换器将 JSON 数组转为 IList<Object>
        if (content is IList<Object> list)
            items = list;
        else
        {
            // 可能是 JsonElement 等类型，通过 ToString() 获取 JSON 再解析
            var json = content.ToString();
            if (json == null || !json.StartsWith("[")) return null;

            try
            {
                // new JsonParser().Decode() 支持解析顶层 JSON 数组，而静态 JsonParser.Decode 只支持对象
                var parsed = new JsonParser(json).Decode();
                items = parsed as IList<Object>;
            }
            catch { return null; }
        }

        if (items == null || items.Count == 0) return null;

        var result = new List<AIContent>();
        foreach (var item in items)
        {
            if (item is not IDictionary<String, Object> dic) continue;

            var type = dic.TryGetValue("type", out var t) ? t + "" : null;
            if (type == "text")
            {
                var text = dic.TryGetValue("text", out var v) ? v + "" : "";
                result.Add(new TextContent(text));
            }
            else if (type == "image_url")
            {
                if (dic.TryGetValue("image_url", out var imgObj) && imgObj is IDictionary<String, Object> imgDic)
                {
                    var url = imgDic.TryGetValue("url", out var u) ? u + "" : null;
                    var img = new ImageContent { Uri = url };
                    if (imgDic.TryGetValue("detail", out var d))
                        img.Detail = d + "";
                    result.Add(img);
                }
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>返回字符串表示形式，格式为 "[Role]{Content}"</summary>
    public override String ToString() => $"[{Role}]{(Content is String str ? str[..Math.Min(64, str.Length)] : Content)}";
    #endregion
}
