using System.Text.Json;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Repositories.Orders;

/// <summary>Hand-written IApiClient for repository tests: records every call and answers with canned JSON or throws.</summary>
internal sealed class RecordingApiClient(Func<string, string, object?, string> respond) : IApiClient
{
    public List<RecordedCall> Calls { get; } = [];

    public RecordedCall Single => Assert.Single(Calls);

    public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) =>
        Handle<TResponse>("GET", relativePath, null, cancellationToken);

    public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
        Handle<TResponse>("POST", relativePath, request, cancellationToken);

    public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
        Handle<TResponse>("PUT", relativePath, request, cancellationToken);

    public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) =>
        Handle<TResponse>("PATCH", relativePath, request, cancellationToken);

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(new RecordedCall("DELETE", relativePath, null));
        respond("DELETE", relativePath, null);
        return Task.CompletedTask;
    }

    private Task<TResponse> Handle<TResponse>(string method, string path, object? body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(new RecordedCall(method, path, body));
        var json = respond(method, path, body);
        using var document = JsonDocument.Parse(json);
        return Task.FromResult((TResponse)(object)document.RootElement.Clone());
    }

    internal static ApiException Http(int status, string? detail = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null) =>
        new(ApiErrorKind.Http, $"HTTP {status}", status, title: "Error", detail: detail, fieldErrors: fieldErrors);
}

internal sealed record RecordedCall(string Method, string Path, object? Body);
