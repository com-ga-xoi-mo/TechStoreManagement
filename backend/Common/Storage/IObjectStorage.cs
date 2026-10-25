namespace TechStore.Api.Common.Storage;

public interface IObjectStorage
{
    /// <summary>Stores bytes and returns their public URL; throws ObjectStorageUnavailableException on storage failure.</summary>
    Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Best-effort delete; storage errors are logged by key only and return false.</summary>
    Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default);
}
