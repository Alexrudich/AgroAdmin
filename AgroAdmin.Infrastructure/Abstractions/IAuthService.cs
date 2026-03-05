namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(string username, string password);
        Task LogoutAsync();
        bool IsAuthenticated();
        string? GetCurrentUser();
        int? GetCurrentUserId();
    }
}
