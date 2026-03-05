using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.Infrastructure.Services;

public class AuthService(AppDbContext context, ILogger<AuthService> logger)
    : IAuthService
{
    // Временное хранилище статуса (для упрощения api/auth/status в рамках одного запуска)
    private static bool _isManualAuthenticated = false;
    private static string? _currentUsername = null;

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            logger.LogInformation("Login attempt for user: {Username}", username);

            var admin = await context.AdminUsers
                .FirstOrDefaultAsync(u => u.Username == username);

            if (admin == null || !admin.VerifyPassword(password))
            {
                logger.LogWarning("Login failed for {Username}", username);
                return false;
            }

            admin.LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _isManualAuthenticated = true;
            _currentUsername = username;

            logger.LogInformation("User {Username} verified", username);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during login for {Username}", username);
            return false;
        }
    }

    public bool IsAuthenticated() => _isManualAuthenticated;

    public string? GetCurrentUser() => _currentUsername;

    public int? GetCurrentUserId() => 1; // Упрощено для админа

    public Task LogoutAsync()
    {
        _isManualAuthenticated = false;
        _currentUsername = null;
        return Task.CompletedTask;
    }
}