using TechStore.Client.ViewModels.Development;
namespace TechStore.Client.UnitTests.Development;

public sealed class ComponentGalleryViewModelTests
{
    [Fact]
    public void Search_UnicodeQuery_PreservesExactTextAndCountsOnce()
    {
        var model = new ComponentGalleryViewModel { SearchText = " điện thoại & phụ kiện " };
        Assert.Equal(0, model.SubmitCount);
        model.SearchCommand.Execute(model.SearchText);
        Assert.Equal(" điện thoại & phụ kiện ", model.LastQuery);
        Assert.Equal(1, model.SubmitCount);
    }

    [Fact]
    public void ClearSearch_QuerySubmitted_ClearsWithoutAnotherSubmit()
    {
        var model = new ComponentGalleryViewModel { SearchText = "máy tính" };
        model.SearchCommand.Execute(model.SearchText);
        model.ClearSearchCommand.Execute(null);
        Assert.Equal("", model.SearchText);
        Assert.Equal(1, model.ClearCount);
        Assert.Equal(1, model.SubmitCount);
        Assert.Equal("máy tính", model.LastQuery);
    }

    [Fact]
    public void CommandAvailability_Changed_NotifiesConsumer()
    {
        var model = new ComponentGalleryViewModel();
        var notifications = 0;
        model.SearchCommand.CanExecuteChanged += (_, _) => notifications++;
        model.CanSearch = false;
        model.CanAct = false;
        Assert.False(model.SearchCommand.CanExecute("test"));
        Assert.False(model.EmptyActionCommand.CanExecute(null));
        Assert.Equal(1, notifications);
        model.CanSearch = true;
        Assert.True(model.SearchCommand.CanExecute(""));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void MetricStates_LoadingErrorRetry_ReachesContentWithoutInventingValue()
    {
        var model = new ComponentGalleryViewModel { MetricValue = "" };
        model.ShowLoadingCommand.Execute(null);
        Assert.True(model.IsLoading);
        Assert.Empty(model.MetricError);
        model.ShowErrorCommand.Execute(null);
        Assert.False(model.IsLoading);
        Assert.NotEmpty(model.MetricError);
        model.ShowContentCommand.Execute(null);
        Assert.False(model.IsLoading);
        Assert.Empty(model.MetricError);
        Assert.Empty(model.MetricValue);
    }

    [Fact]
    public void Theme_Changes_PreservesQueryAndActionState()
    {
        var model = new ComponentGalleryViewModel { SearchText = "Nguyễn Thị Mỹ" };
        model.EmptyActionCommand.Execute(null);
        model.ThemeIndex = 2;
        model.ThemeIndex = 0;
        Assert.Equal("Nguyễn Thị Mỹ", model.SearchText);
        Assert.Equal(1, model.ActionCount);
    }

    [Fact]
    public void NewInstance_AfterEditedSession_RestoresIndependentFixtures()
    {
        var previous = new ComponentGalleryViewModel { SearchText = "private", MetricValue = "0" };
        previous.EmptyActionCommand.Execute(null);
        var fresh = new ComponentGalleryViewModel();
        Assert.Empty(fresh.SearchText);
        Assert.Equal(0, fresh.ActionCount);
        Assert.Equal("482.650.000", fresh.MetricValue);
        Assert.NotSame(previous.Rows, fresh.Rows);
    }

    [Theory]
    [InlineData("0912abcxyz")]
    [InlineData("1234567890")]
    [InlineData("０９１２３４５６７８")]
    [InlineData("")]
    public void Validation_InvalidThenCorrected_ClearsError(string invalid)
    {
        var model = new ComponentGalleryViewModel { Phone = invalid };
        model.ValidatePhoneCommand.Execute(null);
        Assert.NotEmpty(model.PhoneError);
        model.Phone = "0912345678";
        Assert.Empty(model.PhoneError);
    }
}
