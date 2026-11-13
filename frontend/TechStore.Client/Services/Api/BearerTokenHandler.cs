using System.Net;
using System.Net.Http.Headers;
using TechStore.Client.Services.Session;

namespace TechStore.Client.Services.Api;

public sealed class BearerTokenHandler(ISessionStore sessionStore, SessionRefreshCoordinator? refresh = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not null)
        {
            throw new InvalidOperationException("Authorization is managed by the current client session.");
        }

        var path = request.RequestUri?.AbsolutePath;
        var isAuthentication = path is "/api/auth/login" or "/api/auth/refresh";
        var generation = sessionStore.Current.Generation;
        if (!isAuthentication && refresh is not null) await refresh.EnsureFreshAsync(cancellationToken);
        var snapshot = sessionStore.Current;
        if (!isAuthentication && snapshot.Generation != generation)
            throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng thử lại.");
        if (!isAuthentication && snapshot.Status == SessionStatus.Active && !string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && !isAuthentication && snapshot.Status == SessionStatus.Active)
        {
            sessionStore.TryExpire(snapshot.Generation, snapshot.AccessToken);
        }

        return response;
    }
}
