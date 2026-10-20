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

    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task PatchAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<TResponse> PutAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default);
}
