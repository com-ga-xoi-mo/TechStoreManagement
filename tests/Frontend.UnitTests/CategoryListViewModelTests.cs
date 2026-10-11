using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class CategoryListViewModelTests
{
    [Fact]
    public async Task ViewModel_DoesNotLoadInConstructor_AndShowsPreviewSnapshotAfterRequest()
    {
        var repository = new RecordingCategoryRepository();
        var viewModel = new CategoryListViewModel(repository);

        Assert.Equal(0, repository.TreeCalls);
        Assert.False(viewModel.IsLoading);

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(1, repository.TreeCalls);
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.HasLoaded);
        Assert.False(viewModel.HasError);
        Assert.Equal(3, viewModel.CategoryCount);
        Assert.Equal("Dữ liệu danh mục mẫu · Preview", viewModel.PreviewMessage);
        Assert.Equal(3, viewModel.VisibleRows.Count);
    }

    [Fact]
    public async Task ViewModel_SuccessfulEmptySnapshotIsNotAnError()
    {
        var repository = new RecordingCategoryRepository
        {
            Snapshot = new CategoryTreeSnapshot([], IsPreviewData: true)
        };
        var viewModel = new CategoryListViewModel(repository);

        await viewModel.LoadIfNeededAsync();

        Assert.True(viewModel.HasLoaded);
        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.VisibleRows);
    }

    [Fact]
    public async Task ViewModel_FailureIsNotEmpty_AndRetryCanSucceed()
    {
        var repository = new RecordingCategoryRepository
        {
            Failure = new InvalidOperationException("network unavailable")
        };
        var viewModel = new CategoryListViewModel(repository);

        await viewModel.LoadIfNeededAsync();

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsEmpty);
        Assert.False(viewModel.HasLoaded);
        Assert.NotNull(viewModel.ErrorMessage);

        repository.Failure = null;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, repository.TreeCalls);
        Assert.True(viewModel.HasLoaded);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task ViewModel_CancellationIsNotReportedAsError()
    {
        var completion = new TaskCompletionSource<CategoryTreeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingCategoryRepository { PendingSnapshot = completion.Task };
        var viewModel = new CategoryListViewModel(repository);

        var loadTask = viewModel.LoadIfNeededAsync();
        Assert.True(viewModel.IsLoading);

        viewModel.CancelPendingLoad();
        await loadTask;

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasLoaded);
    }

    [Fact]
    public async Task ViewModel_SearchingForChildKeepsAndExpandsItsAncestor()
    {
        var viewModel = new CategoryListViewModel(new FakeCategoryRepository());
        await viewModel.LoadIfNeededAsync();

        viewModel.SearchText = "iPhone";

        Assert.Equal(1, viewModel.MatchedCategoryCount);
        Assert.Equal(2, viewModel.VisibleRows.Count);
        Assert.Equal("phones", viewModel.VisibleRows[0].Id);
        Assert.True(viewModel.VisibleRows[0].IsContextOnly);
        Assert.True(viewModel.VisibleRows[0].IsExpanded);
        Assert.Equal("ios", viewModel.VisibleRows[1].Id);
        Assert.Equal("Điện thoại thông minh", viewModel.VisibleRows[1].ParentLabel);
        Assert.Contains("Cấp 1", viewModel.VisibleRows[0].AccessibilityLabel);
        Assert.Contains("Đang mở rộng", viewModel.VisibleRows[0].AccessibilityLabel);
        Assert.Contains("Cấp 2", viewModel.VisibleRows[1].AccessibilityLabel);
    }

    [Fact]
    public async Task ViewModel_InactiveFilterKeepsAncestorAndOnlyCountsMatchingCategories()
    {
        var viewModel = new CategoryListViewModel(new FakeCategoryRepository());
        await viewModel.LoadIfNeededAsync();

        viewModel.SelectedStatusFilter = "Đã ẩn";

        Assert.Equal(1, viewModel.MatchedCategoryCount);
        Assert.Equal(2, viewModel.VisibleRows.Count);
        Assert.Equal("accessories", viewModel.VisibleRows[0].Id);
        Assert.True(viewModel.VisibleRows[0].IsContextOnly);
        Assert.Equal("audio", viewModel.VisibleRows[1].Id);
        Assert.False(viewModel.VisibleRows[1].IsActive);
    }

    [Fact]
    public async Task ViewModel_ExpandCollapseChangesVisibleDescendantsButNotSourceCounts()
    {
        var viewModel = new CategoryListViewModel(new FakeCategoryRepository());
        await viewModel.LoadIfNeededAsync();
        var phoneRoot = Assert.Single(viewModel.VisibleRows, row => row.Id == "phones");

        Assert.NotNull(phoneRoot.ToggleExpansionCommand);
        phoneRoot.ToggleExpansionCommand!.Execute(null);

        Assert.Equal(17, viewModel.VisibleRows.Count);
        Assert.Equal(642, phoneRoot.ProductCount);
        Assert.Equal(24, viewModel.MatchedCategoryCount);
        Assert.DoesNotContain(viewModel.VisibleRows, row => row.ParentLabel == "Điện thoại thông minh");
    }

    [Fact]
    public async Task ViewModel_FilterWithNoMatchesShowsFilteredEmptyState()
    {
        var viewModel = new CategoryListViewModel(new FakeCategoryRepository());
        await viewModel.LoadIfNeededAsync();

        viewModel.SearchText = "không tồn tại";

        Assert.True(viewModel.IsEmpty);
        Assert.Equal("Không tìm thấy danh mục phù hợp.", viewModel.EmptyMessage);
        Assert.Equal(0, viewModel.MatchedCategoryCount);
        Assert.Equal(24, viewModel.CategoryCount);
    }

    private sealed class RecordingCategoryRepository : ICategoryRepository
    {
        public int TreeCalls { get; private set; }
        public CategoryTreeSnapshot Snapshot { get; set; } = new(
        [
            new CategoryNode("root", "Danh mục", null, "Mô tả", 2, true,
            [new CategoryNode("child", "Danh mục con", "root", null, 2, true, [])]),
            new CategoryNode("other", "Danh mục khác", null, null, 0, false, [])
        ], IsPreviewData: true);
        public Exception? Failure { get; set; }
        public Task<CategoryTreeSnapshot>? PendingSnapshot { get; set; }

        public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<CategoryOption>>([]);
        }

        public async Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TreeCalls++;
            if (Failure is not null)
            {
                throw Failure;
            }

            if (PendingSnapshot is not null)
            {
                return await PendingSnapshot.WaitAsync(cancellationToken);
            }

            return Snapshot;
        }
    }
}
