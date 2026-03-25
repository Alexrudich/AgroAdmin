namespace AgroAdmin.Shared.Utils;

public static class TelegramDateHelper
{
    public static (DateTime startDate, DateTime endDate) GetPeriodDates(string callbackData)
    {
        var today = DateTime.Today;

        return callbackData switch
        {
            "period_current_month" => (
                today,
                new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),

            "period_next_month" => (
                new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month, 1),
                new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month,
                    DateTime.DaysInMonth(today.AddMonths(1).Year, today.AddMonths(1).Month))),

            "period_two_weeks" => (today, today.AddDays(14)),
            "period_30_days" => (today, today.AddDays(30)),
            "period_90_days" => (today, today.AddDays(90)),

            _ => (default, default)
        };
    }

    public static (DateTime startDate, DateTime endDate) GetSummaryDates(string callbackData)
    {
        var today = DateTime.Today;

        return callbackData switch
        {
            "summary_current_month" => (
                new DateTime(today.Year, today.Month, 1),
                new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),

            "summary_prev_month" => (
                new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1),
                new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month,
                    DateTime.DaysInMonth(today.AddMonths(-1).Year, today.AddMonths(-1).Month))),

            "summary_next_month" => (
                new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month, 1),
                new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month,
                    DateTime.DaysInMonth(today.AddMonths(1).Year, today.AddMonths(1).Month))),

            "summary_7_days" => (today, today.AddDays(7)),
            "summary_30_days" => (today, today.AddDays(30)),
            "summary_90_days" => (today, today.AddDays(90)),

            _ => (default, default)
        };
    }
}