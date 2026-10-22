using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductDetailActionsTests
{
    [Fact]
    public async Task SalesStaff_CanManageIsFalse_AndToggleStatusCannotExecute()
    {
        // Arrange
        var session = CatalogSamples.Session("SalesStaff");
        var commands = new RecordingProductCommands();
        var dialogs = new ScriptedDialogService();
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Assert
        Assert.False(vm.CanManage);
        Assert.False(vm.ToggleStatusCommand.CanExecute(null));
        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Admin_ActiveProduct_ToggleStatusConfirmsAndDeactivates()
    {
        // Arrange
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands();
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader
        {
            OnGetById = id => Task.FromResult<ProductDetail?>(CatalogSamples.Detail(id, isActive: true))
        };
        var nav = new NavigationService(new AllowAllPreparer());
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Assert initial state
        Assert.Equal("Ngừng kinh doanh", vm.ToggleStatusText);

        // Act
        await vm.ToggleStatusCommand.ExecuteAsync(null);

        // Assert
        var change = Assert.Single(commands.StatusChanges);
        Assert.Equal(CatalogSamples.ProductId, change.Id);
        Assert.False(change.IsActive);
        Assert.NotNull(vm.Product);
        Assert.False(vm.Product.IsActive);
        Assert.Equal("Mở bán", vm.ToggleStatusText);
    }

    [Fact]
    public async Task ToggleStatus_WhenConfirmationDeclined_MakesNoCall()
    {
        // Arrange
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands();
        var dialogs = new ScriptedDialogService { Result = DialogResult.Cancelled };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Act
        await vm.ToggleStatusCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(commands.StatusChanges);
        Assert.True(vm.Product!.IsActive);
    }

    [Fact]
    public async Task ToggleStatus_Status409Conflict_SetsActionErrorAndKeepsProductUnchanged()
    {
        // Arrange
        var conflict = RecordingApiClient.Http(409, "Product has active reservations.");
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands { OnSetStatus = (_, _) => throw conflict };
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Act
        await vm.ToggleStatusCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Product has active reservations.", vm.ActionError);
        Assert.True(vm.HasActionError);
        Assert.True(vm.Product!.IsActive);
    }

    [Fact]
    public async Task DeleteCommand_ConfirmedAndSuccessful_DeletesAndNavigatesToProductListWithReturnQuery()
    {
        // Arrange
        var query = new ProductQuery { Query = "test" };
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands();
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        nav.Navigate(AppRoute.ProductDetail(CatalogSamples.ProductId, query));
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, query, null);
        await vm.ActivateAsync();

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        var deletedId = Assert.Single(commands.Deleted);
        Assert.Equal(CatalogSamples.ProductId, deletedId);
        Assert.Equal(AppRouteKind.ProductList, nav.CurrentRoute.Kind);
        Assert.Equal("test", nav.CurrentRoute.ReturnQuery?.Query);
    }

    [Fact]
    public async Task DeleteCommand_Status409Conflict_SetsActionErrorWithDeactivateHintAndStaysOnDetail()
    {
        // Arrange
        var conflict = RecordingApiClient.Http(409, "Product is referenced in existing orders.");
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands { OnDelete = _ => throw conflict };
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        nav.Navigate(AppRoute.ProductDetail(CatalogSamples.ProductId));
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Product is referenced in existing orders.", vm.ActionError);
        Assert.Contains("Ngừng kinh doanh", vm.ActionError);
        Assert.Equal(AppRouteKind.ProductDetail, nav.CurrentRoute.Kind);
    }

    [Fact]
    public async Task DeleteCommand_Status404_SetsNotFoundMessage()
    {
        // Arrange
        var notFound = RecordingApiClient.Http(404);
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands { OnDelete = _ => throw notFound };
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        nav.Navigate(AppRoute.ProductDetail(CatalogSamples.ProductId));
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Sản phẩm không còn tồn tại.", vm.ActionError);
        Assert.Equal(AppRouteKind.ProductDetail, nav.CurrentRoute.Kind);
    }

    [Fact]
    public async Task EditCommand_NavigatesToProductEditWithProductIdAndReturnQuery()
    {
        // Arrange
        var query = new ProductQuery { Query = "find" };
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands();
        var dialogs = new ScriptedDialogService();
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        nav.Navigate(AppRoute.ProductDetail(CatalogSamples.ProductId, query));
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, query, null);
        await vm.ActivateAsync();

        // Act
        vm.EditCommand.Execute(null);

        // Assert
        Assert.Equal(AppRouteKind.ProductEdit, nav.CurrentRoute.Kind);
        Assert.Equal(CatalogSamples.ProductId, nav.CurrentRoute.ProductId);
        Assert.Equal("find", nav.CurrentRoute.ReturnQuery?.Query);
    }

    [Fact]
    public async Task ToggleStatus_WhileBusy_SecondExecutionMakesNoCall()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ProductDetail>();
        var session = CatalogSamples.Session("Admin");
        var commands = new RecordingProductCommands { OnSetStatus = (_, _) => tcs.Task };
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };
        var reader = new StubProductReader();
        var nav = new NavigationService(new AllowAllPreparer());
        var vm = new ProductDetailViewModel(reader, nav, session, commands, dialogs, new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();

        // Act
        var firstTask = vm.ToggleStatusCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.ToggleStatusCommand.CanExecute(null));

        await vm.ToggleStatusCommand.ExecuteAsync(null); // Second execution while busy

        Assert.Single(commands.StatusChanges);

        tcs.SetResult(CatalogSamples.Detail(CatalogSamples.ProductId, isActive: false));
        await firstTask;

        Assert.False(vm.IsBusy);
        Assert.True(vm.ToggleStatusCommand.CanExecute(null));
    }
}
