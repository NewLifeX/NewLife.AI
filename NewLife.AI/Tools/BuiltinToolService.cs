using System.ComponentModel;
using System.Globalization;
using NewLife.Collections;

namespace NewLife.AI.Tools;

/// <summary>内置基础工具服务。提供时间查询与数学计算等开箱即用的原生工具，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>网络访问、搜索、IP 查询、天气、翻译等功能请使用 <see cref="NetworkToolService"/></remarks>
public class BuiltinToolService
{
    #region 时间工具

    /// <summary>获取当前日期和时间信息，包括完整日期、星期、时间、时区、Unix时间戳等</summary>
    [ToolDescription("get_current_time", IsSystem = true)]
    [DisplayName("当前时间")]
    [Description("获取当前日期和时间信息，包括完整日期、星期、时间、时区、Unix时间戳等")]
    public String GetCurrentTime()
    {
        var now = DateTimeOffset.Now;
        var tzName = TimeZoneInfo.Local.DisplayName;

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine($"datetime: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"date: {now:yyyy-MM-dd}");
        sb.AppendLine($"time: {now:HH:mm:ss}");
        sb.AppendLine($"dayOfWeek: {now.DayOfWeek} ({GetChineseDayOfWeek(now.DayOfWeek)})");
        sb.AppendLine($"weekOfYear: {CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)}");
        sb.AppendLine($"timezone: {tzName}");
        sb.AppendLine($"utcOffset: {now.Offset}");
        sb.Append($"unixTimestamp: {now.ToUnixTimeSeconds()}");
        return sb.Return(true);
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
    [ToolDescription("calculate", Triggers = "计算一下,帮我计算,数学计算,表达式求值", Enable = false)]
    [DisplayName("数学计算")]
    [Description("计算数学表达式的结果。支持加减乘除、括号、取模等基本运算")]
    public Object Calculate([Description("数学表达式，如 (3 + 5) * 2 - 10 / 3")] String expression)
    {
        if (String.IsNullOrWhiteSpace(expression))
            return new { error = "expression is required" };

        // 安全校验：仅允许数字、运算符、括号、空格、小数点
        var sanitized = expression.Trim();
        foreach (var c in sanitized)
        {
            if (!Char.IsDigit(c) && c != '+' && c != '-' && c != '*' && c != '/' && c != '%'
                && c != '(' && c != ')' && c != '.' && c != ' ')
                return new { error = $"invalid character '{c}' in expression" };
        }

        try
        {
            using var dt = new System.Data.DataTable();
            var result = dt.Compute(sanitized, null);
            return new { expression = sanitized, result };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    #endregion
}

