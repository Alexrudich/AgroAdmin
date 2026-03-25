namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface IDatabaseBackupService
    {
        Task<(bool Success, string Message, string? FileLink)> CreateManualBackupAsync();
    }
}
