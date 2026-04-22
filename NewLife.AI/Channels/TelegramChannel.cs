using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>Telegram 消息渠道。通过 Bot API 发送消息到 Telegram 群组或用户</summary>
/// <remarks>
/// 配置格式（target 字段）：{BotToken}:{ChatId}
/// 例：1234567890:AAHxxxxxxxxxxxxxxxxxxxxxxxx:-1001234567890
///
/// BotToken 由 @BotFather 创建机器人后获得。
/// ChatId 为群组（负数）或用户 ID（正数），可通过 @userinfobot 获取。
/// Telegram Bot API 文档：https://core.telegram.org/bots/api
/// </remarks>
public class TelegramChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "Telegram";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    // 每个 BotToken 对应一个 ApiHttpClient，避免重复创建连接
    private readonly ConcurrentDictionary<String, ApiHttpClient> _clients = new();
    #endregion

    private static readonly String _baseUrl = "https://api.telegram.org/bot{0}/";

    private ApiHttpClient GetClient(String botToken)
    {
        var url = String.Format(_baseUrl, botToken);
        var client = _clients.GetOrAdd(botToken, _ => new ApiHttpClient(url));
        client.Log = Log;
        return client;
    }

    /// <summary>解析目标字符串，格式为 {BotToken}:{ChatId}</summary>
    /// <param name="target">目标字符串</param>
    /// <param name="botToken">Bot Token</param>
    /// <param name="chatId">聊天 ID</param>
    private static void ParseTarget(String target, out String botToken, out String chatId)
    {
        // 格式：{BotToken}:{ChatId}
        // BotToken 本身含有 ":" 分隔符（格式为 数字:字符串），因此取最后一个 ":" 前的部分作为 token
        var lastColon = target.LastIndexOf(':');
        if (lastColon <= 0)
            throw new ArgumentException($"Telegram target 格式错误，期望 {{BotToken}}:{{ChatId}}，实际：{target}");

        // BotToken 格式：{数字}:{字符串}，ChatId 可能为负数（群组）或正数（用户）
        // 由于 BotToken 含一个冒号，格式为 数字:字母数字...，ChatId 为最后一段（可能以 - 开头）
        // 需要找到 BotToken 的结束位置，BotToken 本身是 "数字:字母串"
        // 策略：从后往前找第二个冒号（即 BotToken 和 ChatId 的分隔冒号）
        var secondLastColon = target.LastIndexOf(':', lastColon - 1);
        if (secondLastColon < 0)
            throw new ArgumentException($"Telegram target 格式错误，需包含 BotToken（含冒号）和 ChatId，实际：{target}");

        botToken = target[..lastColon];
        chatId = target[(lastColon + 1)..];
    }

    /// <summary>发送消息到 Telegram</summary>
    /// <param name="target">目标。格式：{BotToken}:{ChatId}</param>
    /// <param name="content">消息内容（支持 HTML 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        ParseTarget(target, out var botToken, out var chatId);

        // Telegram sendMessage 参数
        var payload = new
        {
            chat_id = chatId,
            text = ConvertToHtml(content),
            parse_mode = "HTML",
        };

        var client = GetClient(botToken);
        try
        {
            // Telegram Bot API：POST /bot{token}/sendMessage
            var result = await client.InvokeAsync<Object>("sendMessage", payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("Telegram 发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Telegram 发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效（要求格式为 {BotToken}:{ChatId}）</summary>
    /// <param name="config">Telegram target 字符串</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (config.IsNullOrWhiteSpace()) return Task.FromResult(false);
        // 格式验证：至少含两个冒号（BotToken 含一个冒号，再加上分隔符）
        var colonCount = 0;
        foreach (var c in config)
        {
            if (c == ':') colonCount++;
        }
        return Task.FromResult(colonCount >= 2);
    }

    /// <summary>将 Markdown 转换为 Telegram HTML 格式</summary>
    /// <param name="markdown">标准 Markdown 文本</param>
    /// <returns>Telegram HTML 格式文本</returns>
    private static String ConvertToHtml(String markdown)
    {
        if (markdown.IsNullOrEmpty()) return markdown;

        // 按块处理：代码块不做 Markdown 转换，普通文本转义后再转换格式
        var sb = new System.Text.StringBuilder();
        var inCodeBlock = false;
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    sb.AppendLine("<pre><code>");
                    inCodeBlock = true;
                }
                else
                {
                    sb.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                continue;
            }

            if (inCodeBlock)
            {
                // 代码块内容：仅转义 HTML 特殊字符
                sb.AppendLine(EscapeHtml(line));
            }
            else
            {
                var processed = EscapeHtml(line);
                // 粗体：**text** → <b>text</b>
                processed = System.Text.RegularExpressions.Regex.Replace(
                    processed, @"\*\*(.+?)\*\*", "<b>$1</b>");
                // 斜体：*text* → <i>text</i>（排除已处理的粗体）
                processed = System.Text.RegularExpressions.Regex.Replace(
                    processed, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");
                // 行内代码：`code` → <code>code</code>
                processed = System.Text.RegularExpressions.Regex.Replace(
                    processed, @"`([^`]+)`", "<code>$1</code>");
                // 链接：[text](url) → <a href="url">text</a>
                processed = System.Text.RegularExpressions.Regex.Replace(
                    processed, @"\[(.+?)\]\((https?://[^\)]+)\)", "<a href=\"$2\">$1</a>");
                sb.AppendLine(processed);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static String EscapeHtml(String text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
