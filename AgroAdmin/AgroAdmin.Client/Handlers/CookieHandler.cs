namespace AgroAdmin.Client.Handlers;

public class CookieHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Просто передаем запрос дальше - куки отправятся автоматически
        return await base.SendAsync(request, cancellationToken);
    }
}