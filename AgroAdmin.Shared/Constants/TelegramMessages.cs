namespace AgroAdmin.Shared.Constants;

public static class TelegramMessages
{
    public const string Welcome = """
                                  🌿 *Добро пожаловать в AgroAdmin Bot!*

                                  Я помогу вам управлять усадьбой прямо из Telegram.

                                  🔔 Вы будете получать уведомления о новых бронированиях.

                                  💡 Для получения списка команд используйте /help
                                  """;

    // Шаблон для приветствия с подстановкой команд
    public static string GetWelcomeMessage(string commandsList)
    {
        return $"""
                🌿 *Добро пожаловать в AgroAdmin Bot!*

                Я помогу вам управлять усадьбой прямо из Telegram.

                *Доступные команды:*
                {commandsList}

                🔔 Вы будете получать уведомления о новых бронированиях.

                💡 Для получения полного списка команд используйте /help
                """;
    }

    // Шаблон для справки с подстановкой команд
    public static string GetHelpMessage(string commandsList)
    {
        return $"📖 *Справка по командам*\n\n{commandsList}";
    }
}