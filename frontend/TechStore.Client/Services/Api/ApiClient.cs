using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace TechStore.Client.Services.Api;

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ApiClientOptions _options;
    private readonly ILogger<ApiClient> _logger;
    private readonly Uri _baseUri;

    public ApiClient(HttpClient httpClient, ApiClientOptions options)
        : this(httpClient, options, NullLogger<ApiClient>.Instance)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ApiClient(HttpClient httpClient, ApiClientOptions options, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _baseUri = options.Validate();
    }

    public Task<TResponse> GetAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        SendForBodyAsync<TResponse>(HttpMethod.Get, relativePath, content: null, cancellationToken);

    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendForBodyAsync<TResponse>(
            HttpMethod.Post,
            relativePath,
            JsonContent.Create(request, options: JsonOptions),
            cancellationToken);
    }

    public Task<TResponse> PutAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendForBodyAsync<TResponse>(
            HttpMethod.Put,
            relativePath,
            JsonContent.Create(request, options: JsonOptions),
            cancellationToken);
    }

    public Task<TResponse> PatchAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendForBodyAsync<TResponse>(
            HttpMethod.Patch,
            relativePath,
            JsonContent.Create(request, options: JsonOptions),
            cancellationToken);
    }

    public Task PostAsync<TRequest>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendWithoutResponseAsync(HttpMethod.Post, relativePath, JsonContent.Create(request, options: JsonOptions), cancellationToken);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Delete, relativePath, content: null, cancellationToken);

    public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Patch, relativePath, content: null, cancellationToken);

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var requestMessage = CreateRequest(method, relativePath, content);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await SendAsync(requestMessage, linked.Token, cancellationToken, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateHttpExceptionAsync(
                    response,
                    linked.Token,
                    cancellationToken,
                    timeout.Token);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            _logger.LogWarning("API request timed out.");
            throw TimeoutException(exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError("API transport failed before receiving an HTTP response.");
            throw new ApiException(ApiErrorKind.Network, "The API could not be reached.", innerException: exception);
        }
    }

    private async Task<TResponse> SendForBodyAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using (content)
        using (var request = CreateRequest(method, relativePath, content))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds)))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
        using (var response = await SendAsync(request, linked.Token, cancellationToken, timeout.Token))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateHttpExceptionAsync(
                    response,
                    linked.Token,
                    cancellationToken,
                    timeout.Token);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
                var result = await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, linked.Token);
                if (result is null)
                {
                    _logger.LogWarning("API response body was JSON null with HTTP status {StatusCode}.", (int)response.StatusCode);
                    throw InvalidResponse(response.StatusCode);
                }

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
            {
                _logger.LogWarning("API response body read timed out with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw TimeoutException(exception);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning("API response body was invalid JSON with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw InvalidResponse(response.StatusCode, exception);
            }
            catch (NotSupportedException exception)
            {
                _logger.LogWarning("API response body type was unsupported with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw InvalidResponse(response.StatusCode, exception);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError("API response body could not be read with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw new ApiException(ApiErrorKind.Network, "The API response could not be read.", innerException: exception);
            }
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, HttpContent? content)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.StartsWith("//", StringComparison.Ordinal)
            || relativePath.StartsWith('\\')
            || Uri.TryCreate(relativePath, UriKind.Absolute, out _))
        {
            throw new ArgumentException("API paths must be relative to the configured API base URL.", nameof(relativePath));
        }

        var requestUri = new Uri(_baseUri, relativePath);
        if (!SameOrigin(_baseUri, requestUri))
        {
            throw new ArgumentException("API paths must remain on the configured API origin.", nameof(relativePath));
        }

        return new HttpRequestMessage(method, requestUri) { Content = content };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken linkedToken,
        CancellationToken callerToken,
        CancellationToken timeoutToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedToken);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutToken.IsCancellationRequested)
        {
            _logger.LogWarning("API request timed out before receiving response headers.");
            throw TimeoutException(exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError("API transport failed before receiving an HTTP response.");
            throw new ApiException(ApiErrorKind.Network, "The API could not be reached.", innerException: exception);
        }
    }

    private async Task<ApiException> CreateHttpExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        CancellationToken callerToken,
        CancellationToken timeoutToken)
    {
        ApiProblemDetails? problem = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            problem = ApiProblemDetails.TryParse(document.RootElement);
        }
        catch (JsonException)
        {
            // Preserve the HTTP error when the response body is not valid ProblemDetails JSON.
        }
        catch (NotSupportedException)
        {
            // A non-JSON content stream still reports the original HTTP status.
        }
        catch (HttpRequestException)
        {
            // A failed error-body read still reports the original HTTP status.
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutToken.IsCancellationRequested)
        {
            _logger.LogWarning("API error response body read timed out with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw TimeoutException(exception);
        }

        _logger.LogWarning(
            "API request failed with HTTP status {StatusCode}, error kind {ErrorKind}, trace {TraceId}.",
            (int)response.StatusCode,
            ApiErrorKind.Http,
            problem?.TraceId);
        return ApiException.Http((int)response.StatusCode, problem);
    }

    private static ApiException InvalidResponse(HttpStatusCode statusCode, Exception? innerException = null) =>
        new(ApiErrorKind.InvalidResponse,
            "The API returned an empty or invalid response body.",
            (int)statusCode,
            innerException: innerException);

    private static ApiException TimeoutException(Exception innerException) =>
        new(ApiErrorKind.Timeout, "The API request timed out.", innerException: innerException);

    private static bool SameOrigin(Uri left, Uri right) =>
        left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase)
        && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
