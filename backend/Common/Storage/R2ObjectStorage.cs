using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace TechStore.Api.Common.Storage;

public sealed class R2ObjectStorage(ObjectStorageOptions options, ILogger<R2ObjectStorage> logger) : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client = new(options.R2.AccessKeyId, options.R2.SecretAccessKey, new AmazonS3Config
    {
        ServiceURL = $"https://{options.R2.AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
    });

    public async Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        ObjectStorageKey.EnsureValid(key);
        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            var request = new PutObjectRequest
            {
                BucketName = options.R2.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };
            request.Headers.CacheControl = "public, max-age=31536000, immutable";
            await _client.PutObjectAsync(request, cancellationToken);
            return PublicUrlFor(options.R2.PublicBaseUrl, key);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or HttpRequestException or TimeoutException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ObjectStorageUnavailableException("Image storage is unavailable.", ex);
        }
    }

    public async Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ObjectStorageKey.EnsureValid(key);
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = options.R2.BucketName, Key = key }, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            logger.LogWarning("Could not delete stored image {Key}", key);
            return false;
        }
    }

    internal static string PublicUrlFor(string publicBaseUrl, string key) => publicBaseUrl.TrimEnd('/') + "/" + key;

    public void Dispose() => _client.Dispose();
}
