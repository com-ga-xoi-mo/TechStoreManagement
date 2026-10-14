using System.Net;
using System.Net.Http.Headers;

namespace TechStore.Client.UnitTests.Infrastructure;

internal sealed class RecordingHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private readonly List<CapturedRequest> _requests = [];
    private readonly object _gate = new();

    public IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var captured = new CapturedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase),
            request.Content?.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            body,
            cancellationToken);

        lock (_gate)
        {
            _requests.Add(captured);
        }

        return await respond(request, cancellationToken);
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, string[]> ContentHeaders,
    string? Body,
    CancellationToken CancellationToken)
{
    public AuthenticationHeaderValue? Authorization =>
        Headers.TryGetValue("Authorization", out var values)
            ? AuthenticationHeaderValue.Parse(values.Single())
            : null;
}
