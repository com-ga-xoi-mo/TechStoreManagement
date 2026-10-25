using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Client.Views.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Files;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Theme;
using TechStore.Client.Services.Window;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Client.ViewModels.Development;
using TechStore.Client.ViewModels.Identity;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Settings;
using TechStore.Client.ViewModels.Shell;
using TechStore.Client.Views.Catalog;
using TechStore.Client.Views.Customers;
using TechStore.Client.Views.Dashboard;
using TechStore.Client.Views.Development;
using TechStore.Client.Views.Identity;
using TechStore.Client.Views.Orders;
using TechStore.Client.Views.Settings;
using TechStore.Client.Views.Shell;

namespace TechStore.Client;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendUi(this IServiceCollection services, IConfiguration configuration)
    {
        var galleryOptions = GalleryOptions.FromConfiguration(configuration);
        services.AddSingleton(galleryOptions);
        services.AddDashboardAnalytics(DashboardPreviewOptions.FromConfiguration(configuration));
        services.AddSingleton<IWindowLifetime, WindowLifetime>();
        services.AddSingleton<IThemePreferenceService, ThemePreferenceService>();
        services.AddSingleton<ContentDialogPresenter>();
        services.AddSingleton<IDialogPresenter>(provider => provider.GetRequiredService<ContentDialogPresenter>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());

        services.AddSingleton<ShellDestinationPreparer>();
        services.AddSingleton<IDestinationPreparer>(provider => provider.GetRequiredService<ShellDestinationPreparer>());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellRoutePageFactory>();
        services.AddTransient<ShellNavigationAdapter>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<ShellPage>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<ProductDetailViewModel>();
        services.AddTransient<ProductEditorViewModel>();
        services.AddTransient<OrderListViewModel>();
        services.AddTransient<OrderCreateViewModel>();
        services.AddTransient<CustomerListViewModel>();
        services.AddTransient<CustomerFormViewModel>();
        services.AddTransient<CustomerDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LoginViewModel>();

        services.AddSingleton<CategoryDialogRequests>();
        services.AddSingleton<ICategoryDialogRequests>(provider => provider.GetRequiredService<CategoryDialogRequests>());
        services.AddTransient<CategoryManagementViewModel>();
        services.AddTransient<CategoryManagementPage>();
        services.AddTransient<InventoryStockViewModel>();
        services.AddTransient<InventoryStockPage>();
        services.AddTransient<SerialImeiViewModel>();
        services.AddTransient<SerialImeiPage>();

        services.AddTransient<DashboardPage>();
        services.AddTransient<ProductListPage>();
        services.AddTransient<ProductDetailPage>();
        services.AddTransient<ProductEditorPage>();
        services.AddTransient<OrderListPage>();
        services.AddTransient<OrderCreatePage>();
        services.AddSingleton<IOrderDetailPresenter, OrderDetailDialogPresenter>();
        services.AddTransient<CustomerListPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<LoginPage>();

        services.AddTransient<ComponentGalleryViewModel>();
        services.AddTransient<ShellDiagnosticViewModel>();
        services.AddTransient<ComponentGalleryPage>();
        if (galleryOptions.Enabled)
        {
            // The C# Markup reference specimen is reachable only from the gallery.
            services.AddSingleton<MarkupReferenceNavigator>();
            services.AddSingleton<IMarkupReferenceNavigator>(provider => provider.GetRequiredService<MarkupReferenceNavigator>());
            services.AddTransient<MarkupReferenceViewModel>();
            services.AddTransient<MarkupReferencePage>();
        }
        services.AddSingleton<ImageFilePicker>();
        services.AddSingleton<IImageFilePicker>(provider => provider.GetRequiredService<ImageFilePicker>());
        services.AddSingleton<CsvFileSaver>();
        services.AddSingleton<ICsvFileSaver>(provider => provider.GetRequiredService<CsvFileSaver>());
        services.AddTransient<IProductImageRepository, ProductImageRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IProductCommandRepository, ProductRepository>();
        services.AddTransient<IProductSpecRepository, ProductRepository>();
        services.AddTransient<IProductBrandRepository, ProductRepository>();
        services.AddTransient<ICategoryRepository, CategoryRepository>();
        services.AddTransient<ICategoryWorkspaceRepository, CategoryRepository>();
        services.AddTransient<IInventoryRepository, InventoryRepository>();
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddTransient<IOrderDeviceRepository, OrderDeviceRepository>();
        services.AddTransient<IAuthRepository, AuthRepository>();
        services.AddTransient<IStoreSettingsRepository, StoreSettingsRepository>();
        return services;
    }
}
