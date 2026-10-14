using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechStore.Client;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Composition;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void PrimaryHandler_DisablesRedirectsAndCookies()
    {
        // Arrange / Act
        using var handler = ServiceCollectionExtensions.CreatePrimaryHttpMessageHandler();

        // Assert
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    [Fact]
    public void AddFrontendFoundation_LoggingFiltersHttpClientInformationLogs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFrontendFoundation(new ConfigurationManager());
        services.AddLogging(builder => builder.AddProvider(new EnabledLoggerProvider()));
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("System.Net.Http.HttpClient.Api.LogicalHandler");

        // Act / Assert
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void AddFrontendFoundation_UsesProvidedLoggerFactory()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceCollection();
        services.AddFrontendFoundation(new ConfigurationManager(), loggerFactory: loggerFactory);
        using var provider = services.BuildServiceProvider();

        // Act
        var resolved = provider.GetRequiredService<ILoggerFactory>();

        // Assert
        Assert.Same(loggerFactory, resolved);
    }

    [Fact]
    public void ServiceProviderLifetime_DisposesOwnedServicesExactlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<DisposalProbe>();
        var provider = services.BuildServiceProvider();
        var owner = new ServiceProviderLifetime(provider);
        var probe = (DisposalProbe)owner.Services.GetRequiredService<DisposalProbe>();

        // Act
        owner.Dispose();
        owner.Dispose();

        // Assert
        Assert.Equal(1, probe.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => owner.Services);
    }

    [Fact]
    public async Task ServiceProviderLifetime_AwaitsAsyncDisposalExactlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<AsyncDisposalProbe>();
        var provider = services.BuildServiceProvider();
        var owner = new ServiceProviderLifetime(provider);
        var probe = (AsyncDisposalProbe)owner.Services.GetRequiredService<AsyncDisposalProbe>();

        // Act
        await owner.DisposeAsync();
        await owner.DisposeAsync();

        // Assert
        Assert.Equal(1, probe.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => owner.Services);
    }

    [Fact]
    public async Task AddFrontendFoundation_ResolvesSharedSessionAndTransientTypedClient()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":1}")
            }));
        var services = new ServiceCollection();
        services.AddFrontendFoundation(new ConfigurationManager(),
            builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Act
        var session1 = provider.GetRequiredService<ISessionStore>();
        var session2 = provider.GetRequiredService<ISessionStore>();
        var client1 = provider.GetRequiredService<IApiClient>();
        var client2 = provider.GetRequiredService<IApiClient>();
        session1.SetAccessToken("test-token");
        var result = await client1.GetAsync<ResponseDto>("products/1");

        // Assert
        Assert.Same(session1, session2);
        Assert.NotSame(client1, client2);
        Assert.Equal(1, result.Id);
        Assert.Equal("Bearer", Assert.Single(handler.Requests).Authorization?.Scheme);
        Assert.Equal("test-token", handler.Requests[0].Authorization?.Parameter);
    }

    [Fact]
    public void AddFrontendFoundation_UsesValidatedConfiguredOptions()
    {
        // Arrange
        var configuration = new ConfigurationManager
        {
            [ApiClientOptions.BaseUrlEnvironmentVariable] = "https://localhost:7207/api",
            [ApiClientOptions.TimeoutEnvironmentVariable] = "15"
        };
        var services = new ServiceCollection();
        services.AddFrontendFoundation(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Act
        var options = provider.GetRequiredService<ApiClientOptions>();

        // Assert
        Assert.Equal("https://localhost:7207/api/", options.Validate().AbsoluteUri);
        Assert.Equal(15, options.TimeoutSeconds);
    }

    private sealed record ResponseDto(int Id);

    private sealed class DisposalProbe : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class AsyncDisposalProbe : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EnabledLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new EnabledLogger();
        public void Dispose() { }

        private sealed class EnabledLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) { }
        }
    }
}
