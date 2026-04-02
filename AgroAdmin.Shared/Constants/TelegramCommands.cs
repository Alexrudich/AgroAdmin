namespace AgroAdmin.Shared.Constants;

public static class TelegramCommands
{
    // Все команды, которые знает бот
    public static readonly string[] AllCommands =
    {
        "/start",
        "/help",
        "/nearestbookings",
        "/checkfreeslots",
        "/createfullbackup",
        "/bookingsummary",
        "/health"
    };

    // Базовые команды, которые показываем всем в приветствии
    public static readonly string[] BasicCommands =
    {
        "/help",
        "/nearestbookings",
        "/checkfreeslots",
        "/bookingsummary"
    };

    // Команды, доступные только для Manager и Admin
    public static readonly string[] RestrictedCommands =
    {
        "/createfullbackup",
        "/health"
    };

    // Проверка, доступна ли команда для роли
    public static bool IsCommandAvailable(string command, string? role)
    {
        command = command.ToLower();

        if (!AllCommands.Contains(command))
            return false;

        if (RestrictedCommands.Contains(command))
        {
            return role?.ToLower() is "manager" or "admin";
        }

        return true;
    }

    // Получить список команд для отображения в help
    public static string[] GetAvailableCommands(string? role)
    {
        return AllCommands
            .Where(cmd => IsCommandAvailable(cmd, role))
            .ToArray();
    }

    // Отформатировать список базовых команд (для приветствия)
    public static string FormatBasicCommandList()
    {
        var result = "";

        foreach (var cmd in BasicCommands)
        {
            result += cmd switch
            {
                "/help" => "/help - показать список команд\n",
                "/nearestbookings" => "/nearestBookings - ближайшие бронирования\n",
                "/checkfreeslots" => "/checkFreeSlots - свободные даты\n",
                "/bookingsummary" => "/bookingSummary - сводка за период\n",
                _ => ""
            };
        }

        return result.TrimEnd();
    }

    // Отформатировать список команд для вывода (в зависимости от роли)
    public static string FormatCommandList(string? role)
    {
        var availableCommands = GetAvailableCommands(role);
        var result = "";

        foreach (var cmd in availableCommands)
        {
            result += cmd switch
            {
                "/start" => "/start - начать работу с ботом\n",
                "/help" => "/help - показать список команд\n",
                "/nearestbookings" => "/nearestBookings - ближайшие бронирования\n",
                "/checkfreeslots" => "/checkFreeSlots - свободные даты\n",
                "/createfullbackup" => "/createFullBackup - создать полный бэкап\n",
                "/bookingsummary" => "/bookingSummary - сводка за период\n",
                "/health" => "/health - проверить состояние системы\n",
                _ => ""
            };
        }

        return result.TrimEnd();
    }
}