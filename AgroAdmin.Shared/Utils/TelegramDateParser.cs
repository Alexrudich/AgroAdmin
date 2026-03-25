namespace AgroAdmin.Shared.Utils;

public static class TelegramDateParser
{
    public class ParseResult
    {
        public bool Success { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasDates { get; set; }  // true - есть даты, false - нет параметров
    }

    public static ParseResult ParseDateRange(string? input, int maxDays = 90)
    {
        var result = new ParseResult();

        if (string.IsNullOrWhiteSpace(input))
        {
            result.Success = true;
            result.HasDates = false;
            return result;
        }

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Если нет параметров (только команда)
        if (parts.Length < 2)
        {
            result.Success = true;
            result.HasDates = false;
            return result;
        }

        // Парсим первую дату
        if (!DateTime.TryParse(parts[1], out var startDate))
        {
            result.Success = false;
            result.ErrorMessage = $"Неверный формат даты: {parts[1]}";
            return result;
        }

        DateTime endDate;

        // Если есть вторая дата
        if (parts.Length >= 3 && DateTime.TryParse(parts[2], out endDate))
        {
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }
        }
        else
        {
            // Если только одна дата — показываем месяц
            endDate = startDate.AddMonths(1).AddDays(-1);
        }

        // Проверяем лимит
        if ((endDate - startDate).Days > maxDays)
        {
            result.Success = false;
            result.ErrorMessage = $"Период не может превышать {maxDays} дней. " +
                                  $"Ваш период: {(endDate - startDate).Days} дней.";
            return result;
        }

        result.Success = true;
        result.StartDate = startDate;
        result.EndDate = endDate;
        result.HasDates = true;

        return result;
    }
}