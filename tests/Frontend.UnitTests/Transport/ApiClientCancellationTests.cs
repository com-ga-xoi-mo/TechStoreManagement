using System.Net;
using System.Text;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Transport;

public sealed class ApiClientCancellationTests
{
    [Fact]
    public async Task GetAsync_PreservesCallerCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHttpMessageHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => api.GetAsync<string>("products", cancellation.Token));

        // Assert
        Assert.IsNotType<ApiException>(error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_PreservesCancellationRequestedBeforeSend()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => api.GetAsync<string>("products", cancellation.Token));

        // Assert
        Assert.IsNotType<ApiException>(error);
        Assert.True(Assert.Single(handler.Requests).CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task GetAsync_ClassifiesTimeoutWhileWaitingForHeaders()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, ShortTimeoutOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Timeout, error.Kind);
        Assert.Null(error.StatusCode);
    }

    [Fact]
    public async Task GetAsync_ClassifiesTimeoutWhileReadingBodyAndDisposesResponse()
    {
        // Arrange
        var content = new BlockingReadContent();
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, ShortTimeoutOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Timeout, error.Kind);
        Assert.Null(error.StatusCode);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task GetAsync_PreservesCallerCancellationWhileReadingBody()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var content = new BlockingReadContent();
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, ShortTimeoutOptions());

        // Act
        var pending = api.GetAsync<string>("products", cancellation.Token);
        await content.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        // Assert
        Assert.IsNotType<ApiException>(error);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task GetAsync_ClassifiesTimeoutWhileReadingAnErrorBody()
    {
        // Arrange
        var content = new BlockingReadContent();
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, ShortTimeoutOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Timeout, error.Kind);
        Assert.Null(error.StatusCode);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task GetAsync_ClassifiesNetworkFailureWithoutHttpStatus()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("private network detail")));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Network, error.Kind);
        Assert.Null(error.StatusCode);
        Assert.DoesNotContain("private network detail", error.Message);
    }

    private static ApiClientOptions ShortTimeoutOptions() => new() { TimeoutSeconds = 0.1 };

    private sealed class BlockingReadContent : HttpContent
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new BlockingReadStream(ReadStarted));

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new BlockingReadStream(ReadStarted));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingReadStream(TaskCompletionSource readStarted) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            SignalAndWaitAsync(cancellationToken);

        private async Task<int> SignalAndWaitAsync(CancellationToken cancellationToken)
        {
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
