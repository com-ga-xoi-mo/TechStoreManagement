namespace TechStore.Api.Common.Storage;

public sealed class LocalObjectStorage(ObjectStorageOptions options, string contentRootPath, ILogger<LocalObjectStorage> logger) : IObjectStorage
{
    public async Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        ObjectStorageKey.EnsureValid(key);
        try
        {
            var path = Path.Combine(contentRootPath, options.Local.RootPath, key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await stream.WriteAsync(content, cancellationToken);
            return options.Local.RequestPath.TrimEnd('/') + "/" + key;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ObjectStorageUnavailableException("Image storage is unavailable.", ex);
        }
    }

    public Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ObjectStorageKey.EnsureValid(key);
        try
        {
            var path = Path.Combine(contentRootPath, options.Local.RootPath, key);
            if (File.Exists(path)) File.Delete(path);
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            logger.LogWarning("Could not delete stored image {Key}", key);
            return Task.FromResult(false);
        }
    }
}
