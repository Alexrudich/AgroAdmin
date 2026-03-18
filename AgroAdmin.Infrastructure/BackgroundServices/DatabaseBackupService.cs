using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Newtonsoft.Json;

namespace AgroAdmin.Infrastructure.BackgroundServices;

public class DatabaseBackupService(
    IServiceProvider services,
    ILogger<DatabaseBackupService> logger)
    : BackgroundService
{
    private readonly TimeSpan _backupInterval = TimeSpan.FromDays(7);
    private readonly string _backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "backups");
    private readonly string _oauthCredentialsPath =
        Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CREDENTIALS_PATH")
        ?? "/root/.aspnet/google/oauth-credentials.json";
    private readonly string _tokenPath =
        Environment.GetEnvironmentVariable("GOOGLE_TOKEN_PATH")
        ?? "/root/.aspnet/google/token.json";
    private readonly string? _myEmail = Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_EMAIL");
    private readonly string _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🔄 Database backup service started (weekly)");

        if (string.IsNullOrEmpty(_myEmail))
        {
            logger.LogError("❌ GOOGLE_PRIVATE_EMAIL не задан в переменных окружения");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformBackupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error in database backup");
            }

            await Task.Delay(_backupInterval, stoppingToken);
        }
    }

    private string GetEnvPrefix()
    {
        return _environment switch
        {
            "Development" => "🖥️ [ЛОКАЛКА] ",
            "Production" => "🚀 [ПРОД] ",
            _ => "⚠️ "
        };
    }

    private string GetEnvShortPrefix()
    {
        return _environment switch
        {
            "Development" => "🖥️ ",
            "Production" => "🚀 ",
            _ => "⚠️ "
        };
    }

    private string GetFilePrefix()
    {
        return _environment switch
        {
            "Production" => "prod_",
            "Development" => "dev_",
            _ => ""
        };
    }

    private async Task PerformBackupAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🔁 Запуск еженедельного бэкапа...");

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

        var now = DateTime.UtcNow;
        var lastBackup = await dbContext.BackupInfos
            .OrderByDescending(b => b.LastBackupDate)
            .FirstOrDefaultAsync(stoppingToken);

        if (lastBackup != null &&
            lastBackup.LastBackupDate.Year == now.Year &&
            lastBackup.LastBackupDate.Month == now.Month)
        {
            logger.LogInformation("📭 Бэкап за текущий месяц уже есть.");
            //await telegram.SendMessageAsync($"{GetEnvShortPrefix()}📭 Бэкап за текущий месяц уже есть.");
            return;
        }

        var backupMonth = now.AddMonths(-1);
        var filePath = await CreateMonthlyBackupAsync(
            dbContext,
            backupMonth.Year,
            backupMonth.Month,
            stoppingToken
        );

        if (filePath == null)
        {
            logger.LogInformation("📭 Нет данных за {0}-{1:D2}", backupMonth.Year, backupMonth.Month);
            await telegram.SendMessageAsync($"{GetEnvShortPrefix()}📭 Нет данных за {backupMonth.Year}-{backupMonth.Month:D2}");
            return;
        }

        var fileName = $"{GetFilePrefix()}{backupMonth.Year}-{backupMonth.Month:D2}.xlsx";
        var fileLink = await UploadToGoogleDriveAsync(filePath, fileName);

        if (string.IsNullOrEmpty(fileLink))
        {
            await telegram.SendMessageAsync($"{GetEnvShortPrefix()}❌ Ошибка загрузки бэкапа за {backupMonth.Year}-{backupMonth.Month:D2}");
            return;
        }

        var backupInfo = new BackupInfo
        {
            LastBackupDate = DateTime.UtcNow,
            TotalRecords = await dbContext.Bookings
                .CountAsync(b => b.ArrivalDate.Year == backupMonth.Year &&
                                b.ArrivalDate.Month == backupMonth.Month,
                                stoppingToken),
            LastBackupFile = fileName
        };

        dbContext.BackupInfos.Add(backupInfo);
        await dbContext.SaveChangesAsync(stoppingToken);

        var message = $"{GetEnvPrefix()}📦 Бэкап за {backupMonth.Year}-{backupMonth.Month:D2} готов\n📄 {fileName}\n🔗 {fileLink}";
        await telegram.SendMessageAsync(message);
    }

    private async Task<string?> CreateMonthlyBackupAsync(
        AppDbContext dbContext,
        int year,
        int month,
        CancellationToken stoppingToken)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var bookings = await dbContext.Bookings
            .Include(b => b.Guest)
            .Where(b => b.ArrivalDate >= startDate && b.ArrivalDate <= endDate)
            .OrderBy(b => b.ArrivalDate)
            .ToListAsync(stoppingToken);

        if (!bookings.Any()) return null;

        ExcelPackage.License.SetNonCommercialPersonal("AgroAdmin");

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Bookings");

        worksheet.Cells[1, 1].Value = "GuestName";
        worksheet.Cells[1, 2].Value = "Phone";
        worksheet.Cells[1, 3].Value = "ArrivalDate";
        worksheet.Cells[1, 4].Value = "DepartureDate";
        worksheet.Cells[1, 5].Value = "ReservedUnit";
        worksheet.Cells[1, 6].Value = "AdultsCount";
        worksheet.Cells[1, 7].Value = "ChildrenCount";
        worksheet.Cells[1, 8].Value = "InfantsCount";
        worksheet.Cells[1, 9].Value = "HasDog";
        worksheet.Cells[1, 10].Value = "NeedsSauna";
        worksheet.Cells[1, 11].Value = "NeedsBanquetHall";
        worksheet.Cells[1, 12].Value = "AdminNotes";

        var row = 2;
        foreach (var b in bookings)
        {
            worksheet.Cells[row, 1].Value = b.Guest?.FullName;
            worksheet.Cells[row, 2].Value = b.Guest?.Phone;
            worksheet.Cells[row, 3].Value = b.ArrivalDate.ToString("yyyy-MM-dd");
            worksheet.Cells[row, 4].Value = b.DepartureDate.ToString("yyyy-MM-dd");
            worksheet.Cells[row, 5].Value = (int)b.ReservedUnit;
            worksheet.Cells[row, 6].Value = b.AdultsCount;
            worksheet.Cells[row, 7].Value = b.ChildrenCount;
            worksheet.Cells[row, 8].Value = b.InfantsCount;
            worksheet.Cells[row, 9].Value = b.HasDog;
            worksheet.Cells[row, 10].Value = b.NeedsSauna;
            worksheet.Cells[row, 11].Value = b.NeedsBanquetHall;
            worksheet.Cells[row, 12].Value = b.AdminNotes;
            row++;
        }

        worksheet.Cells.AutoFitColumns();

        var fileName = $"{GetFilePrefix()}{year}-{month:D2}.xlsx";
        var yearPath = Path.Combine(_backupDir, year.ToString());
        Directory.CreateDirectory(yearPath);
        var filePath = Path.Combine(yearPath, fileName);
        await package.SaveAsAsync(new FileInfo(filePath), stoppingToken);

        logger.LogInformation("✅ Создан XLSX: {FilePath}", filePath);
        return filePath;
    }

    private async Task<string?> UploadToGoogleDriveAsync(string filePath, string fileName)
    {
        try
        {
            logger.LogInformation("☁️ Загрузка в Google Drive: {FileName}", fileName);

            if (!File.Exists(_tokenPath))
            {
                logger.LogError("❌ Файл токена не найден: {Path}", _tokenPath);
                return null;
            }

            var tokenJson = await File.ReadAllTextAsync(_tokenPath);
            var token = JsonConvert.DeserializeObject<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(tokenJson);

            using var oauthStream = new FileStream(_oauthCredentialsPath, FileMode.Open, FileAccess.Read);
            var secrets = GoogleClientSecrets.FromStream(oauthStream).Secrets;

            var initializer = new Google.Apis.Auth.OAuth2.Flows.AuthorizationCodeFlow.Initializer(
                "https://accounts.google.com/o/oauth2/auth",
                "https://oauth2.googleapis.com/token")
            {
                ClientSecrets = secrets,
                Scopes = new[] { DriveService.Scope.Drive }
            };

            var flow = new Google.Apis.Auth.OAuth2.Flows.AuthorizationCodeFlow(initializer);
            var credential = new UserCredential(flow, _myEmail, token);

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "AgroAdmin"
            });

            // Настройка для поиска папки с поддержкой общих дисков
            var folderId = await GetOrCreateFolderAsync(service, "AgroAdminBackups");
            if (string.IsNullOrEmpty(folderId))
            {
                logger.LogError("❌ Не удалось получить/создать папку");
                return null;
            }

            // Проверяем, есть ли уже файл с таким именем
            var fileListRequest = service.Files.List();
            fileListRequest.Q = $"name = '{fileName}' and '{folderId}' in parents and trashed = false";
            fileListRequest.Fields = "files(id)";
            fileListRequest.SupportsAllDrives = true;
            fileListRequest.IncludeItemsFromAllDrives = true;
            var existingFiles = await fileListRequest.ExecuteAsync();

            if (existingFiles.Files != null && existingFiles.Files.Any())
            {
                logger.LogInformation("📄 Файл {FileName} уже существует, удаляем старый...", fileName);
                var deleteRequest = service.Files.Delete(existingFiles.Files.First().Id);
                deleteRequest.SupportsAllDrives = true;
                await deleteRequest.ExecuteAsync();
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };

            await using var uploadStream = new FileStream(filePath, FileMode.Open);
            var request = service.Files.Create(fileMetadata, uploadStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            request.Fields = "id, name, webViewLink";
            request.SupportsAllDrives = true;

            var progress = await request.UploadAsync();

            if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
            {
                logger.LogError("❌ Ошибка загрузки. Статус: {Status}", progress.Status);
                return null;
            }

            var uploadedFile = request.ResponseBody;
            var fileUrl = uploadedFile.WebViewLink ?? $"https://drive.google.com/file/d/{uploadedFile.Id}/view";

            logger.LogInformation("✅ Файл успешно загружен в Google Drive");
            return fileUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка загрузки в Google Drive");
            return null;
        }
    }

    private async Task<string?> GetOrCreateFolderAsync(DriveService service, string folderName)
    {
        try
        {
            logger.LogInformation("🔍 Ищем папку '{FolderName}' в Google Drive...", folderName);

            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            listRequest.Fields = "files(id, name)";
            listRequest.IncludeItemsFromAllDrives = true;
            listRequest.SupportsAllDrives = true;

            var existingFolders = await listRequest.ExecuteAsync();

            if (existingFolders.Files != null && existingFolders.Files.Any())
            {
                var existingFolder = existingFolders.Files.First();
                logger.LogInformation("✅ Папка найдена: {FolderName} (ID: {FolderId})",
                    existingFolder.Name, existingFolder.Id);
                return existingFolder.Id;
            }

            logger.LogInformation("📁 Папка не найдена, создаём новую...");

            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };

            var createRequest = service.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            createRequest.SupportsAllDrives = true;
            var folder = await createRequest.ExecuteAsync();

            logger.LogInformation("✅ Папка создана, ID: {FolderId}", folder.Id);
            return folder.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Критическая ошибка при работе с папкой");
            return null;
        }
    }
}