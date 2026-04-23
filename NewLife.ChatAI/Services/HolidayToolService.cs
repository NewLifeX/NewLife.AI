using System.ComponentModel;
using NewLife.AI.Tools;
using NewLife.Collections;
using NewLife.Holiday;

namespace NewLife.ChatAI.Services;

/// <summary>日期信息工具服务。集成 NewLife.Holiday 组件，按指定日期查询农历、24节气、工作日状态等中国特色日历信息</summary>
public class HolidayToolService
{
    #region 日期查询工具

    /// <summary>查询指定日期的农历、节气及工作日状态（工作/放假/调休）</summary>
    /// <param name="date">要查询的日期，格式 yyyy-MM-dd。不传则默认今天</param>
    [ToolDescription("query_date_info", Triggers = "日期查询,节假日查询,是否上班,节气", IsSystem = true)]
    [DisplayName("日期节假日查询")]
    [Description("查询指定日期的农历、节气及工作日状态（工作/放假/调休）")]
    public Object QueryDateInfo([Description("要查询的日期，格式 yyyy-MM-dd。不传则默认今天")] String? date = null)
    {
        DateTime dt;
        if (String.IsNullOrWhiteSpace(date))
            dt = DateTime.Today;
        else if (!DateTime.TryParse(date, out dt))
            return new { error = $"无效的日期格式：{date}，请使用 yyyy-MM-dd" };

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine($"date: {dt:yyyy-MM-dd}");
        sb.AppendLine($"dayOfWeek: {dt.DayOfWeek} ({GetChineseDayOfWeek(dt.DayOfWeek)})");
        AppendWorkdayStatus(sb, dt);
        AppendLunarInfo(sb, dt);
        AppendSolarTermInfo(sb, dt);
        while (sb.Length > 0 && sb[sb.Length - 1] is '\r' or '\n') sb.Length--;
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