using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Extensions;
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
    : BackgroundService, IDatabaseBackupService
{
    private readonly TimeSpan _backupInterval = TimeSpan.FromDays(7);
    private readonly TimeSpan _tokenRefreshInterval = TimeSpan.FromMinutes(30);
    private readonly string _backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "backups");
    private readonly string _oauthCredentialsPath =
        Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CREDENTIALS_PATH")
        ?? "/root/.aspnet/google/oauth-credentials.json";
    private readonly string _tokenPath =
        Environment.GetEnvironmentVariable("GOOGLE_TOKEN_PATH")
        ?? "/root/.aspnet/google/token.json";
    private readonly string? _myEmail = Environment.GetEnvironmentVariable("GOOGLE_PRIVATE_EMAIL");
    private readonly string _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";

    private string GetFilePrefix()
    {
        return _environment switch
        {
            "Production" => "prod_",
            "Development" => "dev_",
            _ => ""
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🔄 Database backup service started (weekly)");

        if (string.IsNullOrEmpty(_myEmail))
        {
            logger.LogError("❌ GOOGLE_PRIVATE_EMAIL не задан в переменных окружения");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        await RefreshTokenIfNeededAsync();

        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_tokenRefreshInterval, stoppingToken);
                    await RefreshTokenIfNeededAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Ошибка в периодическом обновлении токена");
                }
            }
        }, stoppingToken);

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

    private async Task RefreshTokenIfNeededAsync()
    {
        try
        {
            logger.LogInformation("🔄 Проверка Google Drive токена...");
            var credential = await GetUserCredentialAsync();
            if (credential != null)
            {
                logger.LogInformation("✅ Токен валиден");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка проверки токена");
        }
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
            return;
        }

        var fileName = $"{GetFilePrefix()}{backupMonth.Year}-{backupMonth.Month:D2}.xlsx";
        var fileLink = await UploadToGoogleDriveAsync(filePath, fileName);

        if (string.IsNullOrEmpty(fileLink))
        {
            await telegram.SendMessageAsync($"❌ Ошибка загрузки бэкапа за {backupMonth.Year}-{backupMonth.Month:D2}");
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

        var message = $"📦 <b>Бэкап за {backupMonth.Year}-{backupMonth.Month:D2} готов</b>\n\n" +
                      $"📄 {fileName}\n\n" +
                      $"🔗 <a href=\"{fileLink}\">Скачать</a>";
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

        var fileName = $"{year}-{month:D2}.xlsx";
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

            var credential = await GetUserCredentialAsync();
            if (credential == null)
            {
                return null;
            }

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "AgroAdmin"
            });

            var folderId = await GetOrCreateFolderAsync(service, "AgroAdminBackups");
            if (string.IsNullOrEmpty(folderId))
            {
                logger.LogError("❌ Не удалось получить/создать папку");
                return null;
            }

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

    public async Task<(bool Success, string Message, string? FileLink)> CreateManualBackupAsync()
    {
        try
        {
            logger.LogInformation("🔄 Ручной запуск полного бэкапа");

            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var allBookings = await dbContext.Bookings
                .Include(b => b.Guest)
                .OrderBy(b => b.ArrivalDate)
                .ToListAsync();

            if (!allBookings.Any())
            {
                return (false, "📭 Нет данных для бэкапа", null);
            }

            var now = DateTime.UtcNow;
            var fileName = $"{GetFilePrefix()}full_backup_{now:yyyy-MM-dd_HHmmss}.xlsx";
            var filePath = await CreateFullBackupExcelAsync(allBookings, fileName);

            if (filePath == null)
            {
                return (false, "❌ Ошибка создания файла бэкапа", null);
            }

            var fileLink = await UploadToGoogleDriveAsync(filePath, fileName);

            if (string.IsNullOrEmpty(fileLink))
            {
                return (false, "❌ Ошибка загрузки бэкапа на Google Drive", null);
            }

            var totalCost = allBookings.Sum(b => b.AccommodationCost ?? 0);
            var totalGuests = allBookings.Sum(b => b.TotalGuestsCount);

            logger.LogInformation("✅ Полный бэкап создан: {FileName}, записей: {Count}, сумма: {TotalCost:C}, гостей: {TotalGuests}",
                fileName, allBookings.Count, totalCost, totalGuests);

            var message = $"✅ *Полный бэкап создан*\n\n" +
                          $"📊 Записей: {allBookings.Count}\n" +
                          $"👥 Всего гостей: {totalGuests}\n" +
                          $"💰 Общая сумма: {totalCost:N0} BYN";

            return (true, message, fileLink);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка при ручном создании полного бэкапа");
            return (false, $"❌ Ошибка: {ex.Message}", null);
        }
    }

    private async Task<UserCredential?> GetUserCredentialAsync()
    {
        try
        {
            if (!File.Exists(_tokenPath))
            {
                logger.LogError("❌ Файл токена не найден: {Path}", _tokenPath);
                return null;
            }

            var tokenJson = await File.ReadAllTextAsync(_tokenPath);
            var token = JsonConvert.DeserializeObject<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(tokenJson);

            if (string.IsNullOrEmpty(token?.RefreshToken))
            {
                logger.LogError("❌ В токене отсутствует refresh_token. Нужно получить новый token.json");
                return null;
            }

            await using var oauthStream = new FileStream(_oauthCredentialsPath, FileMode.Open, FileAccess.Read);
            var secrets = (await GoogleClientSecrets.FromStreamAsync(oauthStream)).Secrets;

            var initializer = new Google.Apis.Auth.OAuth2.Flows.AuthorizationCodeFlow.Initializer(
                "https://accounts.google.com/o/oauth2/auth",
                "https://oauth2.googleapis.com/token")
            {
                ClientSecrets = secrets,
                Scopes = [DriveService.Scope.Drive]
            };

            var flow = new Google.Apis.Auth.OAuth2.Flows.AuthorizationCodeFlow(initializer);
            var credential = new UserCredential(flow, _myEmail, token);

            var needsRefresh = false;

            if (credential.Token.IsStale)
            {
                needsRefresh = true;
                logger.LogInformation("🔄 Токен IsStale, обновляем...");
            }
            else if (credential.Token.ExpiresInSeconds.HasValue && credential.Token.ExpiresInSeconds.Value < 300)
            {
                needsRefresh = true;
                logger.LogInformation("🔄 Токен истекает через {Seconds} сек, обновляем...", credential.Token.ExpiresInSeconds.Value);
            }

            if (needsRefresh)
            {
                try
                {
                    await credential.RefreshTokenAsync(CancellationToken.None);

                    var newTokenJson = JsonConvert.SerializeObject(credential.Token, Formatting.Indented);
                    await File.WriteAllTextAsync(_tokenPath, newTokenJson);
                    logger.LogInformation("✅ Токен обновлен и сохранен");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Ошибка обновления токена. Возможно, refresh_token протух");
                    return null;
                }
            }

            var expiresAt = credential.Token.ExpiresInSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(credential.Token.ExpiresInSeconds.Value)
                : DateTime.UtcNow.AddHours(1);

            logger.LogInformation("✅ Токен действителен до: {Expiry}", expiresAt.ToString("yyyy-MM-dd HH:mm:ss"));

            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка получения UserCredential");
            return null;
        }
    }

    private async Task<string?> CreateFullBackupExcelAsync(List<Booking> bookings, string fileName)
    {
        try
        {
            ExcelPackage.License.SetNonCommercialPersonal("AgroAdmin");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("FullBackup");

            worksheet.Cells[1, 1].Value = "ID";
            worksheet.Cells[1, 2].Value = "GuestName";
            worksheet.Cells[1, 3].Value = "Phone";
            worksheet.Cells[1, 4].Value = "ArrivalDate";
            worksheet.Cells[1, 5].Value = "DepartureDate";
            worksheet.Cells[1, 6].Value = "ReservedUnit";
            worksheet.Cells[1, 7].Value = "AdultsCount";
            worksheet.Cells[1, 8].Value = "ChildrenCount";
            worksheet.Cells[1, 9].Value = "InfantsCount";
            worksheet.Cells[1, 10].Value = "TotalGuestsCount";
            worksheet.Cells[1, 11].Value = "HasDog";
            worksheet.Cells[1, 12].Value = "NeedsSauna";
            worksheet.Cells[1, 13].Value = "NeedsBanquetHall";
            worksheet.Cells[1, 14].Value = "AccommodationCost";
            worksheet.Cells[1, 15].Value = "CheckInTime";
            worksheet.Cells[1, 16].Value = "CreatedAt";
            worksheet.Cells[1, 17].Value = "AdminNotes";
            worksheet.Cells[1, 18].Value = "FeedbackComment";

            var row = 2;
            foreach (var b in bookings)
            {
                worksheet.Cells[row, 1].Value = b.Id;
                worksheet.Cells[row, 2].Value = b.Guest?.FullName;
                worksheet.Cells[row, 3].Value = b.Guest?.Phone;
                worksheet.Cells[row, 4].Value = b.ArrivalDate.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 5].Value = b.DepartureDate.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 6].Value = b.ReservedUnit.ToFriendlyString();
                worksheet.Cells[row, 7].Value = b.AdultsCount;
                worksheet.Cells[row, 8].Value = b.ChildrenCount;
                worksheet.Cells[row, 9].Value = b.InfantsCount;
                worksheet.Cells[row, 10].Value = b.TotalGuestsCount;
                worksheet.Cells[row, 11].Value = b.HasDog;
                worksheet.Cells[row, 12].Value = b.NeedsSauna;
                worksheet.Cells[row, 13].Value = b.NeedsBanquetHall;
                worksheet.Cells[row, 14].Value = b.AccommodationCost;
                worksheet.Cells[row, 15].Value = b.CheckInTime.ToString(@"hh\:mm");
                worksheet.Cells[row, 16].Value = b.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[row, 17].Value = b.AdminNotes;
                worksheet.Cells[row, 18].Value = b.FeedbackComment;
                row++;
            }

            worksheet.Cells.AutoFitColumns();

            var yearPath = Path.Combine(_backupDir, DateTime.UtcNow.Year.ToString());
            Directory.CreateDirectory(yearPath);
            var filePath = Path.Combine(yearPath, fileName);
            await package.SaveAsAsync(new FileInfo(filePath));

            logger.LogInformation("✅ Создан полный бэкап: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка создания Excel файла");
            return null;
        }
    }
}