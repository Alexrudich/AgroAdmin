namespace AgroAdmin.Domain.Models;

public class BackupInfo
{
    public int Id { get; set; }
    public DateTime LastBackupDate { get; set; }
    public int TotalRecords { get; set; }
    public string? LastBackupFile { get; set; }
}