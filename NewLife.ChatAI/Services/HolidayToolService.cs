using System.Globalization;
using NewLife.AI.Tools;
using NewLife.Collections;
using NewLife.Holiday;

namespace NewLife.ChatAI.Services;

/// <summary>增强版时间工具服务。集成 NewLife.Holiday 组件，在基础时间信息之上提供农历、24节气、工作日状态等中国特色日历信息</summary>
/// <remarks>
/// 通过 ToolRegistry 先于 <see cref="BuiltinToolService"/> 注册，以同名工具 get_current_time 覆盖原版，
/// 无需修改核心库即可为 ChatAI 提供带节假日信息的增强版时间工具。
/// </remarks>
public class HolidayToolService
{
    #region 时间工具

    /// <summary>获取当前日期和时间信息，包括完整日期、星期、时间、时区、Unix时间戳、农历、生肖、天干地支、工作日状态（工作/放假/调休）、最近节气等</summary>
    [ToolDescription("get_current_time", IsSystem = true)]
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
        sb.AppendLine($"unixTimestamp: {now.ToUnixTimeSeconds()}");
        AppendLunarInfo(sb, now.DateTime);
        AppendWorkdayStatus(sb, now.DateTime);
        AppendSolarTermInfo(sb, now.DateTime);
        while (sb.Length > 0 && sb[^1] is '\r' or '\n') sb.Length--;
        return sb.Return(true);
    }

    #endregion

    #region 辅助

    private static void AppendLunarInfo(System.Text.StringBuilder sb, DateTime dt)
    {
        try
        {
            var lunar = Lunar.FromDateTime(dt);
            sb.AppendLine($"lunarDate: {lunar}");
            sb.AppendLine($"yearGanzhi: {lunar.YearGanzhi}");
            sb.AppendLine($"zodiac: {lunar.Zodiac}");
            sb.AppendLine($"lunarMonth: {(lunar.IsLeapMonth ? "闰" : "")}{lunar.MonthText}月");
            sb.AppendLine($"lunarDay: {lunar.DayText}");
        }
        catch { }
    }

    private static void AppendWorkdayStatus(System.Text.StringBuilder sb, DateTime dt)
    {
        var holidays = HolidayExtensions.China.Query(dt).ToList();
        if (holidays.Count > 0)
        {
            var holiday = holidays[0];
            var statusText = holiday.Status switch
            {
                HolidayStatus.On => "放假",
                HolidayStatus.Off => "调休（需上班）",
                _ => dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "休息" : "工作",
            };
            sb.AppendLine($"workdayStatus: {statusText}");
            sb.AppendLine($"holidayName: {holiday.Name}");
        }
        else
        {
            sb.AppendLine($"workdayStatus: {(dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "休息" : "工作")}");
        }
    }

    private static void AppendSolarTermInfo(System.Text.StringBuilder sb, DateTime dt)
    {
        try
        {
            var lunar = Lunar.FromDateTime(dt);
            var term = lunar.GetNearestSolarTerm();
            sb.AppendLine($"solarTerm: {term.Term}");
            sb.AppendLine($"solarTermDate: {term.TermTime:yyyy-MM-dd}");
            if (term.IsTermDay) sb.AppendLine("isSolarTermDay: true");
            else sb.AppendLine($"daysToSolarTerm: {term.DaysTo:+0.#;-0.#}天");
        }
        catch { }
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
}
