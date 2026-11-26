using TechStore.Client.Models.Assistant;
using TechStore.Client.Repositories.Assistant;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Assistant;

namespace TechStore.Client.UnitTests.ViewModels;

public sealed class AssistantViewModelTests
{
    private static AssistantViewModel Create(FakeRepository repository, string role = "SalesStaff")
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "user", "User", role);
        return new AssistantViewModel(repository, session, new AssistantConversation());
    }

    [Fact]
    public async Task ReopenedPage_KeepsConversationUntilSessionChanges()
    {
        // Arrange
        var repository = new FakeRepository();
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var conversation = new AssistantConversation();
        var first = new AssistantViewModel(repository, session, conversation);
        first.Draft = "question";
        await first.SendCommand.ExecuteAsync(null);

        // Act: switching tabs builds a new page and ViewModel
        var reopened = new AssistantViewModel(repository, session, conversation);
        reopened.Draft = "follow-up";
        await reopened.SendCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(5, reopened.Messages.Count);
        Assert.Contains(repository.Requests.Last(), turn => turn.Text == "question");

        // Act: another account signs in
        session.SetSession("token2", "refresh2", "cashier1", "Cashier", "SalesStaff");
        var nextSession = new AssistantViewModel(repository, session, conversation);

        // Assert
        Assert.Single(nextSession.Messages);
        Assert.False(nextSession.Messages[0].IsUser);
        Assert.DoesNotContain("doanh thu", nextSession.Messages[0].Text);
    }

    [Fact]
    public async Task LeavingWhileWaiting_ReopenedPageShowsPendingThenReply()
    {
        // Arrange
        var repository = new FakeRepository { Gate = new() };
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "cashier1", "Cashier", "SalesStaff");
        var conversation = new AssistantConversation(session);
        var first = new AssistantViewModel(repository, session, conversation);
        first.Draft = "question";
        var pending = first.SendCommand.ExecuteAsync(null);

        // Act: the user switches tabs, then comes back
        first.Deactivate();
        var reopened = new AssistantViewModel(repository, session, conversation);

        // Assert: still waiting, question visible, cancel available
        Assert.True(reopened.IsSending);
        Assert.Equal("question", reopened.Messages.Last().Text);
        Assert.True(reopened.CancelCommand.CanExecute(null));

        // Act: the server answers
        repository.Gate.SetResult();
        await pending;

        // Assert
        Assert.False(reopened.IsSending);
        Assert.Equal("answer", reopened.Messages.Last().Text);
        Assert.False(reopened.SendCommand.CanExecute(null)); // Draft is empty
    }

    [Fact]
    public async Task SignOutWhileWaiting_CancelsAndNextSessionStartsEmpty()
    {
        // Arrange
        var repository = new FakeRepository { Block = true };
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "admin", "Admin", "Admin");
        var conversation = new AssistantConversation(session);
        var vm = new AssistantViewModel(repository, session, conversation);
        vm.Draft = "secret question";
        var pending = vm.SendCommand.ExecuteAsync(null);

        // Act
        session.SetSession("token2", "refresh2", "cashier1", "Cashier", "SalesStaff");
        await pending;
        var next = new AssistantViewModel(repository, session, conversation);

        // Assert
        Assert.False(next.IsSending);
        Assert.Single(next.Messages);
        Assert.DoesNotContain(next.Messages, turn => turn.Text == "secret question");
    }

    [Fact]
    public async Task Send_RecordsConversationAndBoundsHistory()
    {
        var repository = new FakeRepository();
        var vm = Create(repository);
        Assert.False(vm.SendCommand.CanExecute(null));
        for (var i = 0; i < 12; i++)
        {
            vm.Draft = $"question {i}";
            Assert.True(vm.SendCommand.CanExecute(null));
            await vm.SendCommand.ExecuteAsync(null);
        }
        Assert.InRange(repository.Requests.Last().Count, 1, 20);
        Assert.True(repository.Requests.Last().First().IsUser);
        Assert.Equal("question 11", repository.Requests.Last().Last().Text);
        Assert.Equal("answer", vm.Messages.Last().Text);
        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_ExcludesPendingPromptFromNextRequest()
    {
        var repository = new FakeRepository { Block = true };
        var vm = Create(repository);
        vm.Draft = "cancelled";
        var pending = vm.SendCommand.ExecuteAsync(null);
        Assert.True(vm.IsSending);
        Assert.False(vm.SendCommand.CanExecute(null));
        vm.CancelCommand.Execute(null);
        await pending;
        Assert.Equal("Đã hủy", vm.StatusMessage);
        repository.Block = false;
        vm.Draft = "next";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.DoesNotContain(repository.Requests.Last(), turn => turn.Text == "cancelled");
    }

    [Fact]
    public async Task Failure_ShowsServerDetailAndRetryResends()
    {
        var repository = new FakeRepository { Fail = true };
        var vm = Create(repository);
        vm.Draft = "question";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.Equal("Chưa cấu hình", vm.ErrorMessage);
        Assert.True(vm.RetryCommand.CanExecute(null));
        vm.Draft = "next draft";
        repository.Fail = false;
        await vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal("next draft", vm.Draft);
        Assert.Equal("question", repository.Requests.Last().Last().Text);
        Assert.Equal("answer", vm.Messages.Last().Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepeatedQuestion_CancelOrFailureRemovesOnlyPendingTurn(bool cancel)
    {
        var repository = new FakeRepository();
        var vm = Create(repository);
        vm.Draft = "same question";
        await vm.SendCommand.ExecuteAsync(null);
        var original = vm.Messages[1];
        repository.Block = cancel;
        repository.Fail = !cancel;
        vm.Draft = "same question";
        var pending = vm.SendCommand.ExecuteAsync(null);
        if (cancel) vm.CancelCommand.Execute(null);
        await pending;
        Assert.Equal(3, vm.Messages.Count);
        Assert.Same(original, vm.Messages[1]);
        Assert.Equal("answer", vm.Messages[2].Text);
    }

    [Theory]
    [InlineData(ApiErrorKind.Network, "Không thể kết nối máy chủ. Vui lòng thử lại.")]
    [InlineData(ApiErrorKind.Timeout, "Trợ lý phản hồi quá lâu. Vui lòng thử lại.")]
    public async Task TransportFailure_UsesVietnameseMessage(ApiErrorKind kind, string expected)
    {
        var repository = new FakeRepository { ErrorKind = kind };
        var vm = Create(repository);
        vm.Draft = "question";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.Equal(expected, vm.ErrorMessage);
    }

    [Fact]
    public async Task ClearAndNewInstance_DoNotRetainConversation()
    {
        var repo = new FakeRepository();
        var first = Create(repo, "Admin");
        first.Draft = "secret";
        await first.SendCommand.ExecuteAsync(null);
        first.ClearCommand.Execute(null);
        Assert.Single(first.Messages);
        var second = Create(repo, "SalesStaff");
        Assert.Single(second.Messages);
        Assert.DoesNotContain(second.Messages, message => message.Text == "secret");
        Assert.NotEqual(first.Messages[0].Text, second.Messages[0].Text);
    }

    private sealed class FakeRepository : IAssistantRepository
    {
        public bool Block { get; set; }
        public bool Fail { get; set; }
        public ApiErrorKind? ErrorKind { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public List<IReadOnlyList<AssistantTurn>> Requests { get; } = [];
        public async Task<AssistantAnswer> SendAsync(IReadOnlyList<AssistantTurn> history, CancellationToken cancellationToken)
        {
            Requests.Add(history.ToArray());
            if (Block) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (Gate is { } gate) await gate.Task.WaitAsync(cancellationToken);
            if (ErrorKind is { } kind) throw new ApiException(kind, "English transport error");
            if (Fail) throw new ApiException(ApiErrorKind.Http, "error", 503, detail: "Chưa cấu hình");
            return new AssistantAnswer("answer", [], null);
        }
    }
}
