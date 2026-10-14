using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace TechStore.Client.Services.Api;

public sealed class ApiClientOptions
{
    public const string BaseUrlEnvironmentVariable = "TECHSTORE_API_BASE_URL";
    public const string TimeoutEnvironmentVariable = "TECHSTORE_API_TIMEOUT_SECONDS";
    public const string DevelopmentBaseUrl = "http://localhost:5176/";
    public const double DefaultTimeoutSeconds = 30;
    private const double MaxHttpClientTimeoutSeconds = (int.MaxValue - 1) / 1000d;

    public string BaseUrl { get; set; } = DevelopmentBaseUrl;

    public double TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public static ApiClientOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new ApiClientOptions
        {
            BaseUrl = configuration[BaseUrlEnvironmentVariable] ?? DevelopmentBaseUrl
        };

        var timeoutValue = configuration[TimeoutEnvironmentVariable];
        if (timeoutValue is not null)
        {
            if (!double.TryParse(timeoutValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeout))
            {
                throw new InvalidOperationException(
                    $"Invalid API configuration: {TimeoutEnvironmentVariable} must be a positive number.");
            }

            options.TimeoutSeconds = timeout;
        }

        options.Validate();
        return options;
    }

    public Uri Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(baseUri.Host)
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException(
                $"Invalid API configuration: {BaseUrlEnvironmentVariable} must be an absolute HTTP or HTTPS URL without user information, query, or fragment.");
        }

        if (!double.IsFinite(TimeoutSeconds)
            || TimeoutSeconds <= 0
            || TimeoutSeconds > MaxHttpClientTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"Invalid API configuration: {TimeoutEnvironmentVariable} must be positive and within the supported HTTP timeout range.");
        }

        var normalizedBaseUrl = baseUri.AbsoluteUri.TrimEnd('/') + "/";
        return new Uri(normalizedBaseUrl, UriKind.Absolute);
    }
}
