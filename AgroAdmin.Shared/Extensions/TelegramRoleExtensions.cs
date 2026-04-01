using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Extensions
{
    public static class TelegramRoleExtensions
    {
        public static bool CanView(this TelegramRole role) => true;

        public static bool CanCreateBackup(this TelegramRole role) =>
            role == TelegramRole.Manager || role == TelegramRole.Admin;

        public static bool CanManageRecipients(this TelegramRole role) =>
            role == TelegramRole.Admin;

    }
}
