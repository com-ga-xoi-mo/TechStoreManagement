using System.Net;
using System.Net.Http.Headers;
using TechStore.Client.Services.Session;

namespace TechStore.Client.Services.Api;

public sealed class BearerTokenHandler(ISessionStore sessionStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not null)
        {
            throw new InvalidOperationException("Authorization is managed by the current client session.");
        }

        var snapshot = sessionStore.Current;
        if (snapshot.Status == SessionStatus.Active && !string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && snapshot.Status == SessionStatus.Active)
        {
            sessionStore.TryExpire(snapshot.Generation);
        }

        return response;
    }
}
