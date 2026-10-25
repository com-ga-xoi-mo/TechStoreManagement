namespace TechStore.Api.Common.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";
    public string Provider { get; set; } = "Local";
    public LocalStorageOptions Local { get; set; } = new();
    public R2StorageOptions R2 { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (!string.Equals(Provider, "Local", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Provider, "R2", StringComparison.OrdinalIgnoreCase))
            errors.Add("ObjectStorage:Provider");
        if (string.Equals(Provider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            Require(R2.AccountId, nameof(R2.AccountId));
            Require(R2.AccessKeyId, nameof(R2.AccessKeyId));
            Require(R2.SecretAccessKey, nameof(R2.SecretAccessKey));
            Require(R2.BucketName, nameof(R2.BucketName));
            Require(R2.PublicBaseUrl, nameof(R2.PublicBaseUrl));
        }
        return errors;

        void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"ObjectStorage:R2:{name}");
        }
    }

    public sealed class LocalStorageOptions
    {
        public string RootPath { get; set; } = "uploads";
        public string RequestPath { get; set; } = "/uploads";
    }

    public sealed class R2StorageOptions
    {
        public string AccountId { get; set; } = "";
        public string AccessKeyId { get; set; } = "";
        public string SecretAccessKey { get; set; } = "";
        public string BucketName { get; set; } = "";
        public string PublicBaseUrl { get; set; } = "";
    }
}
