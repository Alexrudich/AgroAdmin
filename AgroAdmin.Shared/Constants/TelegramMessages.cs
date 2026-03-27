namespace AgroAdmin.Shared.Constants;

public static class TelegramMessages
{
    public const string Welcome = """
                                  🌿 *Добро пожаловать в AgroAdmin Bot!*

                                  Я помогу вам управлять усадьбой прямо из Telegram.

                                  *Доступные команды:*
                                  /help - показать список команд
                                  /nearestBookings - ближайшие бронирования
                                  /checkFreeSlots - свободные даты
                                  /createFullBackup - создать полный бэкап
                                  /bookingSummary - сводка за период
                                  */health* - проверить состояние системы

                                  🔔 Вы будете получать уведомления о новых бронированиях и бэкапах.
                                  """;

    public const string Help = """
                               📖 *Справка по командам*

                               */nearestBookings* - показать бронирования на ближайшие 7 дней
                               */checkFreeSlots* - выбрать период для проверки свободных дат
                               */createFullBackup* - создать полный бэкап всех данных
                               */bookingSummary* - сводка по бронированиям за период
                               */health* - проверить состояние системы
                               """;

}