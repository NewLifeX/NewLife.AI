using System.Globalization;
using System.Text;

namespace NewLife.AI.Tools;

/// <summary>内置工具服务。提供系统级原生 .NET 工具，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>
/// 内置工具无需外部依赖，开箱即用。典型场景：
/// <list type="bullet">
/// <item>获取当前时间 — 模型无法感知实时时间，必须通过工具调用获取</item>
/// <item>数学计算 — 避免模型计算错误</item>
/// <item>编码转换 — Base64、URL 编码等实用操作</item>
/// </list>
/// </remarks>
public class BuiltinToolService
{
    #region 时间工具

    /// <summary>获取当前日期和时间信息，包括完整日期、星期、时间、时区、Unix时间戳等</summary>
    /// <param name="timezone">时区名称，如 Asia/Shanghai、America/New_York。默认使用服务器本地时区</param>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime(String? timezone = null)
    {
        DateTimeOffset now;
        String tzName;

        if (!String.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz);
                tzName = tz.DisplayName;
            }
            catch (TimeZoneNotFoundException)
            {
                now = DateTimeOffset.Now;
                tzName = TimeZoneInfo.Local.DisplayName;
            }
        }
        else
        {
            now = DateTimeOffset.Now;
            tzName = TimeZoneInfo.Local.DisplayName;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"datetime: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"date: {now:yyyy-MM-dd}");
        sb.AppendLine($"time: {now:HH:mm:ss}");
        sb.AppendLine($"dayOfWeek: {now.DayOfWeek} ({GetChineseDayOfWeek(now.DayOfWeek)})");
        sb.AppendLine($"weekOfYear: {CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)}");
        sb.AppendLine($"timezone: {tzName}");
        sb.AppendLine($"utcOffset: {now.Offset}");
        sb.Append($"unixTimestamp: {now.ToUnixTimeSeconds()}");

        return sb.ToString();
    }

    private static String GetChineseDayOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Tuesday => "星期二",
        DayOfWeek.Wednesday => "星期三",
        DayOfWeek.Thursday => "星期四",
        DayOfWeek.Friday => "星期五",
        DayOfWeek.Saturday => "星期六",
        DayOfWeek.Sunday => "星期日",
        _ => day.ToString(),
    };

    #endregion

    #region 数学工具

    /// <summary>计算数学表达式的结果。支持加减乘除、括号、取模等基本运算</summary>
    /// <param name="expression">数学表达式，如 (3 + 5) * 2 - 10 / 3</param>
    [ToolDescription("calculate")]
    public String Calculate(String expression)
    {
        if (String.IsNullOrWhiteSpace(expression))
            return "{\"error\": \"expression is required\"}";

        // 安全校验：仅允许数字、运算符、括号、空格、小数点
        var sanitized = expression.Trim();
        foreach (var c in sanitized)
        {
            if (!Char.IsDigit(c) && c != '+' && c != '-' && c != '*' && c != '/' && c != '%'
                && c != '(' && c != ')' && c != '.' && c != ' ')
                return $"{{\"error\": \"invalid character '{c}' in expression\"}}";
        }

        try
        {
            using var dt = new System.Data.DataTable();
            var result = dt.Compute(sanitized, null);
            return $"{{\"expression\": \"{sanitized}\", \"result\": {result}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    #endregion

    #region 编码工具

    /// <summary>对文本进行 Base64 编码或解码</summary>
    /// <param name="text">要编码或解码的文本</param>
    /// <param name="action">操作类型：encode（编码）或 decode（解码）</param>
    [ToolDescription("base64_convert")]
    public String Base64Convert(String text, String action = "encode")
    {
        if (String.IsNullOrEmpty(text))
            return "{\"error\": \"text is required\"}";

        try
        {
            if (action.EqualIgnoreCase("decode"))
            {
                var bytes = Convert.FromBase64String(text);
                return Encoding.UTF8.GetString(bytes);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                return Convert.ToBase64String(bytes);
            }
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    /// <summary>生成一个全局唯一标识符（UUID/GUID）</summary>
    /// <param name="format">格式：N（无连字符32位）、D（带连字符标准格式）、B（带花括号）。默认 D</param>
    [ToolDescription("generate_uuid")]
    public String GenerateUuid(String format = "D")
    {
        var guid = Guid.NewGuid();
        return format?.ToUpper() switch
        {
            "N" => guid.ToString("N"),
            "B" => guid.ToString("B"),
            _ => guid.ToString("D"),
        };
    }

    #endregion

    #region 实用工具

    /// <summary>获取服务器运行环境信息，包括操作系统、运行时版本、内存使用等</summary>
    [ToolDescription("get_system_info")]
    public String GetSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"os: {Environment.OSVersion}");
        sb.AppendLine($"machineName: {Environment.MachineName}");
        sb.AppendLine($"processorCount: {Environment.ProcessorCount}");
        sb.AppendLine($"runtime: {Environment.Version}");
        sb.AppendLine($"is64BitOS: {Environment.Is64BitOperatingSystem}");

        var process = System.Diagnostics.Process.GetCurrentProcess();
        sb.AppendLine($"workingSet: {process.WorkingSet64 / 1024 / 1024} MB");
        sb.Append($"uptime: {(DateTime.Now - process.StartTime):d\\.hh\\:mm\\:ss}");

        return sb.ToString();
    }

    /// <summary>统计文本的字符数、字数、行数等信息</summary>
    /// <param name="text">要统计的文本内容</param>
    [ToolDescription("count_text")]
    public String CountText(String text)
    {
        if (String.IsNullOrEmpty(text))
            return "{\"characters\": 0, \"words\": 0, \"lines\": 0, \"bytes\": 0}";

        var charCount = text.Length;
        var lineCount = text.Split('\n').Length;
        var wordCount = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
        var byteCount = Encoding.UTF8.GetByteCount(text);

        return $"{{\"characters\": {charCount}, \"words\": {wordCount}, \"lines\": {lineCount}, \"bytes\": {byteCount}}}";
    }

    #endregion
}
