using System.Net;
using System.Net.Http.Json;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.Session;

public sealed class SessionRefreshTests
{
    [Fact]
    public void RememberMeAndRenewal_PreserveAccountGenerationAndRejectStaleTokenExpiry()
    {
        var storage = new MemoryStorage();
        var store = new InMemorySessionStore(storage);
        Assert.True(store.TrySetSession(0, Pair("one"), true));
        Assert.NotNull(storage.Data);
        Assert.Equal("one", new InMemorySessionStore(storage).Current.AccessToken);
        var generation = store.Current.Generation;
        var events = 0;
        store.SessionChanged += (_, _) => events++;
        Assert.True(store.TrySetSession(generation, Pair("two"), true, renewal: true, expectedAccessToken: "one", expectedRefreshToken: "refresh-one"));
        Assert.Equal(generation, store.Current.Generation);
        Assert.Equal(0, events);
        Assert.False(store.TryExpire(generation, "one"));
        Assert.False(store.TrySetSession(generation, Pair("late"), true, renewal: true, expectedAccessToken: "one", expectedRefreshToken: "refresh-one"));
        store.Clear();
        Assert.False(store.TrySetSession(generation, Pair("stale"), true));
        Assert.True(store.TrySetSession(store.Current.Generation, Pair("private"), false));
        Assert.Null(storage.Data);
        Assert.Equal(SessionStatus.Anonymous, new InMemorySessionStore(storage).Current.Status);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task SimultaneousWrites_RefreshOnceAndSendEachOnce(string method)
    {
        var store = new InMemorySessionStore();
        store.SetSession("old", "refresh-old", "admin", "Admin", "Admin");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;
        var factory = new ClientFactory(new Handler(async (request, ct) =>
        {
            Interlocked.Increment(ref refreshCount);
            Assert.Equal("http://localhost:5176/api/auth/refresh", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Pair("new")) };
        }));
        var coordinator = new SessionRefreshCoordinator(factory, store, new());
        var writes = 0;
        using var client = new HttpClient(new BearerTokenHandler(store, coordinator)
        {
            InnerHandler = new Handler((request, _) =>
            {
                Interlocked.Increment(ref writes);
                Assert.Equal("new", request.Headers.Authorization!.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })
        });
        var one = client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "http://localhost:5176/api/orders"));
        await started.Task;
        var two = client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "http://localhost:5176/api/orders"));
        release.SetResult();
        await Task.WhenAll(one, two);
        Assert.Equal(1, refreshCount);
        Assert.Equal(2, writes);
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotCancelSharedRefreshOrOtherCaller()
    {
        var store = new InMemorySessionStore();
        store.SetSession("old", "refresh-old", "admin", "Admin", "Admin");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new SessionRefreshCoordinator(new ClientFactory(new Handler(async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Pair("new")) };
        })), store, new());
        using var cancel = new CancellationTokenSource();
        var first = coordinator.EnsureFreshAsync(cancel.Token);
        var second = coordinator.EnsureFreshAsync();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult();
        await second;
        Assert.Equal("new", store.Current.AccessToken);
    }

    [Fact]
    public async Task LateRefresh_DoesNotRestoreOldUserOrOverwriteNewSession()
    {
        var storage = new MemoryStorage();
        var store = new InMemorySessionStore(storage);
        store.SetSession("old", "refresh-old", "admin", "Admin", "Admin");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new SessionRefreshCoordinator(new ClientFactory(new Handler(async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Pair("late")) };
        })), store, new());
        var refresh = coordinator.EnsureFreshAsync();
        store.Clear();
        store.TrySetSession(store.Current.Generation, Pair("other"), true);
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => refresh);
        Assert.Equal("other", store.Current.AccessToken);
        Assert.Equal("other", storage.Data!.AccessToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrUncertainRefresh_DoesNotSendBusinessWriteOrRetry(bool networkFailure)
    {
        var store = new InMemorySessionStore();
        store.SetSession("old", "refresh-old", "admin", "Admin", "Admin");
        var refreshes = 0;
        var coordinator = new SessionRefreshCoordinator(new ClientFactory(new Handler((_, _) =>
        {
            refreshes++;
            if (networkFailure) throw new HttpRequestException("lost response");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        })), store, new());
        var writes = 0;
        using var client = new HttpClient(new BearerTokenHandler(store, coordinator)
        {
            InnerHandler = new Handler((_, _) => { writes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); })
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsync("http://localhost:5176/api/orders", new StringContent("{}")));
        Assert.Equal(1, refreshes);
        Assert.Equal(0, writes);
        Assert.Equal(SessionStatus.Expired, store.Current.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StorageDeletionFailure_StillClearsMemoryAndNotifiesTheShell(bool expire)
    {
        var storage = new MemoryStorage();
        var store = new InMemorySessionStore(storage);
        store.TrySetSession(0, Pair("secret"), true);
        storage.FailClear = true;
        var events = 0;
        store.SessionChanged += (_, _) => events++;
        Assert.Throws<IOException>(() =>
        {
            if (expire) store.TryExpire(store.Current.Generation);
            else store.Clear();
        });
        Assert.Equal(expire ? SessionStatus.Expired : SessionStatus.Anonymous, store.Current.Status);
        Assert.Null(store.Current.AccessToken);
        Assert.Equal(1, events);
    }

    [Fact]
    public void StorageSaveFailure_DoesNotPublishARememberedSession()
    {
        var storage = new MemoryStorage { FailSave = true };
        var store = new InMemorySessionStore(storage);
        Assert.Throws<IOException>(() => store.TrySetSession(0, Pair("secret"), true));
        Assert.Equal(SessionStatus.Anonymous, store.Current.Status);
        Assert.Null(storage.Data);
    }

    [Fact]
    public void FileStorage_InvalidWriteDestination_ReportsFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var storage = new FileSessionStorage(directory);
            Assert.Throws<UnauthorizedAccessException>(() => storage.Save(new("access", "refresh", "admin", "Admin", "Admin")));
        }
        finally { Directory.Delete(directory); }
    }

    private static LoginResponseDto Pair(string token) => new()
    {
        AccessToken = token, RefreshToken = "refresh-" + token, ExpiresIn = 3600,
        User = new() { Username = "admin", FullName = "Admin", Role = RoleType.Admin }
    };
    private sealed class MemoryStorage : ISessionStorage
    {
        public PersistedSessionData? Data { get; private set; }
        public bool FailSave { get; init; }
        public bool FailClear { get; set; }
        public PersistedSessionData? Load() => Data;
        public void Save(PersistedSessionData data) { if (FailSave) throw new IOException("disk unavailable"); Data = data; }
        public void Clear() { if (FailClear) throw new IOException("disk unavailable"); Data = null; }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
