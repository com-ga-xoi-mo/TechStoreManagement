using System.Text.RegularExpressions;

namespace TechStore.Api.Common.Storage;

public static class ObjectStorageKey
{
    public static void EnsureValid(string key)
    {
        if (string.IsNullOrEmpty(key) || !Regex.IsMatch(key, @"\A[a-z0-9]+(/[a-z0-9]+)*\.(jpg|png|webp)\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid storage key.", nameof(key));
    }
}
