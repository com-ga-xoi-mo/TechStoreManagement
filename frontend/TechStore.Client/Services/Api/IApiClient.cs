namespace TechStore.Client.Services.Api;

public interface IApiClient
{
    Task<TResponse> GetAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task PostAsync<TRequest>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task<TResponse> PostFileAsync<TResponse>(
        string relativePath,
        ApiFileContent file,
        IReadOnlyDictionary<string, string>? fields = null,
        CancellationToken cancellationToken = default);

    Task<TResponse> PutAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task<TResponse> PatchAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
