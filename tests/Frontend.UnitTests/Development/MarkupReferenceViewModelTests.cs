using FluentAssertions;
using TechStore.Client.Services.Development;
using TechStore.Client.ViewModels.Development;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.Development;

public sealed class MarkupReferenceViewModelTests
{
    private sealed class FakeNavigator : IMarkupReferenceNavigator
    {
        public int ShowReferenceCount { get; private set; }
        public int ShowGalleryCount { get; private set; }
        public void ShowReference() => ShowReferenceCount++;
        public void ShowGallery() => ShowGalleryCount++;
    }

    private static (MarkupReferenceViewModel Model, FakeNavigator Navigator) Create()
    {
        var navigator = new FakeNavigator();
        return (new MarkupReferenceViewModel(navigator), navigator);
    }

    [Fact]
    public void Search_UnicodeQuery_RecordsExactTextAndCountsOnce()
    {
        // Arrange
        var (model, _) = Create();
        model.Query = "điện thoại & phụ kiện";

        // Act
        model.SearchCommand.Execute(model.Query);

        // Assert
        model.LastSubmitted.Should().Be("điện thoại & phụ kiện");
        model.SubmitCount.Should().Be(1);
    }

    [Fact]
    public void Reset_AfterSubmit_ClearsQueryAndSubmittedValue()
    {
        // Arrange
        var (model, _) = Create();
        model.Query = "máy tính";
        model.SearchCommand.Execute(model.Query);

        // Act
        model.ResetCommand.Execute(null);

        // Assert
        model.Query.Should().BeEmpty();
        model.LastSubmitted.Should().Be(MarkupReferenceViewModel.NoSubmissionText);
        model.SubmitCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_Default_StartsInContentState()
    {
        // Arrange & Act
        var (model, _) = Create();

        // Assert
        model.IsContent.Should().BeTrue();
        model.ThemeIndex.Should().Be(0);
    }

    [Fact]
    public void StateCommands_EachCommand_ExposesExactlyOneState()
    {
        // Arrange
        var (model, _) = Create();
        var commands = new (Action Run, Func<bool> IsActive)[]
        {
            (() => model.ShowLoadingCommand.Execute(null), () => model.IsLoading),
            (() => model.ShowEmptyCommand.Execute(null), () => model.IsEmpty),
            (() => model.ShowErrorCommand.Execute(null), () => model.IsError),
            (() => model.ShowContentCommand.Execute(null), () => model.IsContent)
        };

        foreach (var (run, isActive) in commands)
        {
            // Act
            run();

            // Assert
            isActive().Should().BeTrue();
            new[] { model.IsLoading, model.IsEmpty, model.IsError, model.IsContent }
                .Count(x => x).Should().Be(1);
        }
    }

    [Fact]
    public void ShowError_SampleError_SetsMessageAndEnablesRetry()
    {
        // Arrange
        var (model, _) = Create();

        // Act
        model.ShowErrorCommand.Execute(null);

        // Assert
        model.ErrorMessage.Should().Be(MarkupReferenceViewModel.SampleErrorMessage);
        model.RetryCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Retry_AfterError_ReturnsToContentAndClearsMessage()
    {
        // Arrange
        var (model, _) = Create();
        model.ShowErrorCommand.Execute(null);

        // Act
        await model.RetryCommand.ExecuteAsync(null);

        // Assert
        model.IsContent.Should().BeTrue();
        model.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ThemeIndex_Changed_KeepsQueryAndState()
    {
        // Arrange
        var (model, _) = Create();
        model.Query = "iphone";
        model.SearchCommand.Execute(model.Query);
        model.ShowEmptyCommand.Execute(null);

        // Act
        model.ThemeIndex = 2;
        model.ThemeIndex = 1;

        // Assert
        model.Query.Should().Be("iphone");
        model.SubmitCount.Should().Be(1);
        model.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Back_Executed_ReturnsToGallery()
    {
        // Arrange
        var (model, navigator) = Create();

        // Act
        model.BackCommand.Execute(null);

        // Assert
        navigator.ShowGalleryCount.Should().Be(1);
    }
}
