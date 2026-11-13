using System.Net;
using System.Net.Http.Json;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Identity;

public sealed class AuthRepositoryTests
{
    [Fact]
    public async Task Login_AfterLocalLogout_DoesNotRestoreSession()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new RecordingHttpMessageHandler((_, _) => gate.Task));
        var store = new InMemorySessionStore();
        var repository = new AuthRepository(new ApiClient(http, new()), store);
        var login = repository.LoginAsync(new LoginRequest { Username = "admin", Password = "Password@123", RememberMe = false });
        store.Clear();
        gate.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(Pair()) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
        Assert.Equal(SessionStatus.Anonymous, store.Current.Status);
    }

    [Fact]
    public async Task Logout_Offline_ClearsLocalSessionAndReportsFailure()
    {
        using var http = new HttpClient(new RecordingHttpMessageHandler((_, _) => throw new HttpRequestException("offline")));
        var store = new InMemorySessionStore();
        store.TrySetSession(0, Pair(), false);
        var repository = new AuthRepository(new ApiClient(http, new()), store);
        await Assert.ThrowsAnyAsync<Exception>(() => repository.LogoutAsync());
        Assert.Equal(SessionStatus.Anonymous, store.Current.Status);
        Assert.Null(store.Current.RefreshToken);
    }

    [Fact]
    public async Task Logout_LateResponse_DoesNotClearNewLogin()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new RecordingHttpMessageHandler((_, _) => gate.Task));
        var store = new InMemorySessionStore();
        store.TrySetSession(0, Pair(), false);
        var repository = new AuthRepository(new ApiClient(http, new()), store);
        var logout = repository.LogoutAsync();
        store.Clear();
        store.TrySetSession(store.Current.Generation, Pair("new-session"), false);
        gate.SetResult(new(HttpStatusCode.NoContent));
        await logout;
        Assert.Equal("new-session", store.Current.AccessToken);
    }

    [Fact]
    public async Task Profile_UpdatesCurrentMetadata_AndDiscardsLateResults()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new RecordingHttpMessageHandler((_, _) => gate.Task));
        var store = new InMemorySessionStore();
        store.TrySetSession(0, Pair(), false);
        var repository = new AuthRepository(new ApiClient(http, new()), store);
        var profile = new UserProfileDto { Username = "admin", FullName = "Updated name", Role = RoleType.Admin };
        var pending = repository.GetProfileAsync();
        gate.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(profile) });
        await pending;
        Assert.Equal("Updated name", store.Current.FullName);
        var generation = store.Current.Generation;
        store.Clear();
        Assert.False(store.TryUpdateProfile(generation, profile));
        Assert.Null(store.Current.FullName);
    }

    [Fact]
    public async Task Profile_ResponseAfterAccountReplacement_DoesNotOverwriteNewProfile()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new RecordingHttpMessageHandler((_, _) => gate.Task));
        var store = new InMemorySessionStore();
        store.TrySetSession(0, Pair(), false);
        var pending = new AuthRepository(new ApiClient(http, new()), store).GetProfileAsync();
        store.Clear();
        var replacement = Pair("replacement");
        replacement.User.FullName = "New account";
        replacement.User.Role = RoleType.Manager;
        store.TrySetSession(store.Current.Generation, replacement, false);

        gate.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(new UserProfileDto
        {
            Username = "old-admin", FullName = "Late profile", Role = RoleType.Admin
        }) });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("replacement", store.Current.AccessToken);
        Assert.Equal("New account", store.Current.FullName);
        Assert.Equal("Manager", store.Current.Role);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RememberedStartup_RefreshesLegacyRecordBeforeMe_AndUsesCurrentProfile(bool rejected)
    {
        var storage = new MemoryStorage();
        var store = new InMemorySessionStore(storage);
        var requests = new List<string>();
        var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            requests.Add(path);
            if (path == "/api/auth/refresh")
            {
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Pair("renewed")) });
            }
            Assert.Equal("renewed", request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(rejected ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new UserProfileDto { Username = "admin", FullName = "Current profile", Role = RoleType.Admin })
            });
        });
        var refresh = new SessionRefreshCoordinator(new ClientFactory(handler), store, new());
        using var http = new HttpClient(new BearerTokenHandler(store, refresh) { InnerHandler = handler });
        var repository = new AuthRepository(new ApiClient(http, new()), store);

        if (rejected)
        {
            await Assert.ThrowsAsync<ApiException>(() => repository.GetProfileAsync());
            Assert.Equal(SessionStatus.Expired, store.Current.Status);
            Assert.Null(storage.Data);
        }
        else
        {
            await repository.GetProfileAsync();
            Assert.Equal("Current profile", store.Current.FullName);
            Assert.Equal("Current profile", storage.Data!.FullName);
        }
        Assert.Equal(new[] { "/api/auth/refresh", "/api/auth/me" }, requests);
    }

    private sealed class MemoryStorage : ISessionStorage
    {
        public PersistedSessionData? Data { get; private set; } = new("legacy", "legacy-refresh", "admin", "Old profile", "Admin");
        public PersistedSessionData? Load() => Data;
        public void Save(PersistedSessionData data) => Data = data;
        public void Clear() => Data = null;
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static LoginResponseDto Pair(string token = "access") => new()
    {
        AccessToken = token, RefreshToken = "refresh", ExpiresIn = 900,
        User = new() { Id = Guid.NewGuid(), Username = "admin", FullName = "Admin", Role = RoleType.Admin }
    };
}
