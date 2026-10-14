using TechStore.Client.Services.Session;

namespace TechStore.Client.UnitTests.Session;

public sealed class SessionStoreTests
{
    [Fact]
    public void Store_StartsAnonymousAndMutationsAdvanceGeneration()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var events = new List<SessionChangedEventArgs>();
        store.SessionChanged += (_, args) => events.Add(args);

        // Act
        var initial = store.Current;
        store.SetAccessToken("token-a");
        var active = store.Current;
        store.Clear();
        var cleared = store.Current;

        // Assert
        Assert.Equal(SessionStatus.Anonymous, initial.Status);
        Assert.Equal(0, initial.Generation);
        Assert.Equal(SessionStatus.Active, active.Status);
        Assert.Equal("token-a", active.AccessToken);
        Assert.Equal(1, active.Generation);
        Assert.Equal(SessionStatus.Anonymous, cleared.Status);
        Assert.Null(cleared.AccessToken);
        Assert.Equal(2, cleared.Generation);
        Assert.Equal(new[] { SessionStatus.Active, SessionStatus.Anonymous }, events.Select(item => item.Status));
        Assert.All(events, item => Assert.Null(typeof(SessionChangedEventArgs).GetProperty("AccessToken")));
    }

    [Fact]
    public void TryExpire_OnlyExpiresMatchingActiveGenerationOnce()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var notificationCount = 0;
        store.SessionChanged += (_, _) => notificationCount++;
        store.SetAccessToken("token-a");
        var generation = store.Current.Generation;

        // Act
        var expired = store.TryExpire(generation);
        var repeated = store.TryExpire(generation);

        // Assert
        Assert.True(expired);
        Assert.False(repeated);
        Assert.Equal(SessionStatus.Expired, store.Current.Status);
        Assert.Null(store.Current.AccessToken);
        Assert.Equal(2, notificationCount);
    }

    [Fact]
    public void TryExpire_DoesNotChangeReplacedOrClearedSession()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var staleGeneration = store.Current.Generation;
        store.SetAccessToken("token-b");

        // Act
        var changed = store.TryExpire(staleGeneration);

        // Assert
        Assert.False(changed);
        Assert.Equal(SessionStatus.Active, store.Current.Status);
        Assert.Equal("token-b", store.Current.AccessToken);
    }

    [Fact]
    public void NewStoreStartsWithoutPersistedToken()
    {
        // Arrange
        var first = new InMemorySessionStore();
        first.SetAccessToken("token-a");

        // Act
        var restarted = new InMemorySessionStore();

        // Assert
        Assert.Equal(SessionStatus.Anonymous, restarted.Current.Status);
        Assert.Null(restarted.Current.AccessToken);
    }
}
