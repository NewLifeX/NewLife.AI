namespace NewLife.AI.Models;

/// <summary>Artifact 检测结果类型</summary>
public enum ArtifactEventKind
{
    /// <summary>普通内容，非 Artifact 部分</summary>
    Normal,

    /// <summary>检测到 Artifact 代码块开始</summary>
    ArtifactStart,

    /// <summary>Artifact 代码块增量内容</summary>
    ArtifactDelta,

    /// <summary>Artifact 代码块结束</summary>
    ArtifactEnd,
}

/// <summary>Artifact 检测事件</summary>
/// <param name="Kind">事件类型</param>
/// <param name="Content">文本内容（Normal/ArtifactDelta 时有值）</param>
/// <param name="Language">代码语言（ArtifactStart 时有值）</param>
public record ArtifactEvent(ArtifactEventKind Kind, String? Content = null, String? Language = null);

/// <summary>Artifact 代码块检测器。在 content_delta 流中检测 ```html/```svg/```mermaid 代码块的开始和结束</summary>
/// <remarks>
/// <para>状态机设计：在文本流中累积内容，检测三反引号代码块模式。</para>
/// <para>支持的预览类型：html、svg、mermaid（与前端 isPreviewable() 保持一致）。</para>
/// <para>非预览型语言（如 python、csharp）的代码块不触发 Artifact 事件。</para>
/// </remarks>
public class ArtifactDetector
{
    /// <summary>可预览的语言列表</summary>
    private static readonly HashSet<String> _previewableLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "svg", "mermaid"
    };

    private Boolean _inCodeBlock;
    private Boolean _isPreviewable;
    private String _pendingBuffer = "";

    /// <summary>当前是否处于 Artifact 代码块内</summary>
    public Boolean InArtifact => _inCodeBlock && _isPreviewable;

    /// <summary>处理一段 content_delta 文本，返回检测到的事件序列</summary>
    /// <param name="delta">增量文本内容</param>
    /// <returns>事件序列。调用方应依次处理并转为对应的 ChatStreamEvent</returns>
    public IList<ArtifactEvent> Process(String delta)
    {
        if (String.IsNullOrEmpty(delta)) return [];

        var events = new List<ArtifactEvent>();
        var text = _pendingBuffer + delta;
        _pendingBuffer = "";

        var i = 0;
        while (i < text.Length)
        {
            if (!_inCodeBlock)
            {
                // 查找 ``` 开始标记
                var tickPos = text.IndexOf("```", i, StringComparison.Ordinal);
                if (tickPos < 0)
                {
                    // 末尾可能是不完整的 ``` （1-2 个反引号），缓存起来
                    var remaining = text[i..];
                    var trailingTicks = CountTrailingBackticks(remaining);
                    if (trailingTicks > 0 && trailingTicks < 3)
                    {
                        var normalPart = remaining[..^trailingTicks];
                        if (normalPart.Length > 0)
                            events.Add(new ArtifactEvent(ArtifactEventKind.Normal, normalPart));
                        _pendingBuffer = remaining[^trailingTicks..];
                    }
                    else
                    {
                        if (remaining.Length > 0)
                            events.Add(new ArtifactEvent(ArtifactEventKind.Normal, remaining));
                    }
                    break;
                }

                // 输出 ``` 前的普通内容
                if (tickPos > i)
                    events.Add(new ArtifactEvent(ArtifactEventKind.Normal, text[i..tickPos]));

                // 查找语言标识（```后到换行符之间的文本）
                var afterTicks = tickPos + 3;
                var newlinePos = text.IndexOf('\n', afterTicks);
                if (newlinePos < 0)
                {
                    // 换行符还没到，缓存等下次
                    _pendingBuffer = text[tickPos..];
                    break;
                }

                var language = text[afterTicks..newlinePos].Trim();
                _inCodeBlock = true;
                _isPreviewable = _previewableLanguages.Contains(language);

                if (_isPreviewable)
                    events.Add(new ArtifactEvent(ArtifactEventKind.ArtifactStart, null, language));

                i = newlinePos + 1;
            }
            else
            {
                // 在代码块内，查找 ``` 结束标记（必须在行首或前面是换行符）
                var closePos = FindClosingFence(text, i);
                if (closePos < 0)
                {
                    // 末尾可能是不完整的结束标记，缓存
                    var remaining = text[i..];
                    var trailingTicks = CountTrailingBackticks(remaining);

                    // 检查是否是换行+不完整反引号
                    if (trailingTicks > 0 && trailingTicks < 3)
                    {
                        var contentPart = remaining[..^trailingTicks];
                        if (contentPart.Length > 0)
                        {
                            if (_isPreviewable)
                                events.Add(new ArtifactEvent(ArtifactEventKind.ArtifactDelta, contentPart));
                        }
                        _pendingBuffer = remaining[^trailingTicks..];
                    }
                    else
                    {
                        if (remaining.Length > 0 && _isPreviewable)
                            events.Add(new ArtifactEvent(ArtifactEventKind.ArtifactDelta, remaining));
                        else if (remaining.Length > 0 && !_isPreviewable)
                        {
                            // 非预览代码块内容作为普通内容输出
                        }
                    }
                    break;
                }

                // 输出关闭前的代码内容
                if (closePos > i)
                {
                    var codeContent = text[i..closePos];
                    if (_isPreviewable)
                        events.Add(new ArtifactEvent(ArtifactEventKind.ArtifactDelta, codeContent));
                }

                if (_isPreviewable)
                    events.Add(new ArtifactEvent(ArtifactEventKind.ArtifactEnd));

                _inCodeBlock = false;
                _isPreviewable = false;

                // 跳过 ``` 和后面可能的换行符
                var afterClose = closePos + 3;
                if (afterClose < text.Length && text[afterClose] == '\n')
                    afterClose++;

                i = afterClose;
            }
        }

        return events;
    }

    /// <summary>查找关闭围栏标记的位置（行首的 ```）</summary>
    private static Int32 FindClosingFence(String text, Int32 start)
    {
        var pos = start;
        while (pos < text.Length)
        {
            var tickPos = text.IndexOf("```", pos, StringComparison.Ordinal);
            if (tickPos < 0) return -1;

            // 关闭围栏必须在行首（位于文本开头或前一个字符是换行）
            if (tickPos == 0 || text[tickPos - 1] == '\n')
                return tickPos;

            pos = tickPos + 3;
        }
        return -1;
    }

    /// <summary>计算字符串末尾连续反引号数量</summary>
    private static Int32 CountTrailingBackticks(String text)
    {
        var count = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == '`')
                count++;
            else
                break;
        }
        return count;
    }

    /// <summary>重置检测器状态</summary>
    public void Reset()
    {
        _inCodeBlock = false;
        _isPreviewable = false;
        _pendingBuffer = "";
    }
}
