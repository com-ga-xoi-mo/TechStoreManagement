using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;

namespace TechStore.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendFoundation(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureApiClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(_ => ApiClientOptions.FromConfiguration(configuration));
        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        services.AddTransient<BearerTokenHandler>();
        services.AddLogging(builder =>
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));
        if (loggerFactory is not null)
        {
            services.AddSingleton(loggerFactory);
            services.AddSingleton<ILoggerFactory>(loggerFactory);
        }

        var clientBuilder = services
            .AddHttpClient<IApiClient, ApiClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<ApiClientOptions>();
                client.BaseAddress = options.Validate();
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHttpMessageHandler)
            .AddHttpMessageHandler<BearerTokenHandler>();

        configureApiClient?.Invoke(clientBuilder);
        return services;
    }

    internal static HttpClientHandler CreatePrimaryHttpMessageHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };
}
