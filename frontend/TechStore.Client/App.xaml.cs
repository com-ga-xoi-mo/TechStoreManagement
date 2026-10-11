using System;
using Microsoft.Extensions.Logging;
using Uno.Resizetizer;

namespace TechStore.Client;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        ConfigureServices();
        this.InitializeComponent();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    private static void ConfigureServices()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Core Services
        services.AddSingleton<Services.Session.ICurrentSession, Services.Session.CurrentSessionProvider>();
        services.AddSingleton<Services.Session.IAuthModeProvider>(
            new TechStore.Client.Services.Session.AuthModeProvider(
                TechStore.Client.Services.Session.SessionConfig.CurrentMode));
        services.AddSingleton<Services.Navigation.INavigationService, Services.Navigation.NavigationService>();
        services.AddSingleton<Services.Navigation.IRootNavigationService, Services.Navigation.RootNavigationService>();
        services.AddSingleton<Services.Dialog.IDialogService, Services.Dialog.DialogService>();
        services.AddSingleton<Services.Dialog.IProductDetailDialogService, Services.Dialog.ProductDetailDialogService>();
        services.AddSingleton<Services.Dialog.IProductCreateDialogService, Services.Dialog.ProductCreateDialogService>();
        services.AddSingleton<Services.Dialog.IProductEditDialogService, Services.Dialog.ProductEditDialogService>();
        services.AddSingleton<Services.Dialog.IStockDetailDialogService, Services.Dialog.StockDetailDialogService>();
        services.AddSingleton<Services.Dialog.IStockAdjustmentDialogService, Services.Dialog.StockAdjustmentDialogService>();
        services.AddSingleton<Services.Inventory.IInventoryChangeNotifier, Services.Inventory.InventoryChangeNotifier>();
        services.AddSingleton<Services.Navigation.IInventoryNavigationContext, Services.Navigation.InventoryNavigationContext>();
        services.AddSingleton<Services.Navigation.IInventoryNavigationService, Services.Navigation.InventoryNavigationService>();

        // Setup AuthRepository conditionally based on Mode
        if (TechStore.Client.Services.Session.SessionConfig.CurrentMode == TechStore.Client.Services.Session.AuthMode.ExplicitPreview)
        {
            services.AddSingleton<Repositories.Identity.IAuthRepository, Repositories.Identity.FakeAuthRepository>();
        }
        else
        {
            services.AddSingleton<Repositories.Identity.IAuthRepository, Repositories.Identity.UnavailableAuthRepository>();
        }

        if (TechStore.Client.Services.Session.SessionConfig.CurrentMode == TechStore.Client.Services.Session.AuthMode.ExplicitPreview)
        {
            services.AddSingleton<Repositories.Analytics.IAnalyticsRepository>(
                new Repositories.Analytics.FakeAnalyticsRepository(
                    Repositories.Analytics.DashboardPreviewSettings.CurrentScenario));
            var productRepository = new Repositories.Catalog.FakeProductRepository(
                scenario: Repositories.Catalog.CatalogPreviewSettings.CurrentScenario,
                detailScenario: Repositories.Catalog.ProductDetailPreviewSettings.CurrentScenario,
                createScenario: Repositories.Catalog.ProductCreatePreviewSettings.CurrentScenario,
                editScenario: Repositories.Catalog.ProductEditPreviewSettings.CurrentScenario);
            services.AddSingleton(productRepository);
            services.AddSingleton<Repositories.Catalog.IProductRepository>(productRepository);
            services.AddSingleton<Repositories.Catalog.ICategoryRepository>(
                new Repositories.Catalog.FakeCategoryRepository(
                    Repositories.Catalog.CategoryTreePreviewSettings.CurrentScenario));
            services.AddSingleton<Repositories.Inventory.IInventoryRepository>(
                new Repositories.Inventory.FakeInventoryRepository(
                    Repositories.Inventory.StockListPreviewSettings.CurrentScenario,
                    Repositories.Inventory.StockDetailPreviewSettings.CurrentScenario,
                    Repositories.Inventory.StockAdjustmentPreviewSettings.CurrentScenario));
        }
        else
        {
            services.AddSingleton<Repositories.Analytics.IAnalyticsRepository, Repositories.Analytics.UnavailableAnalyticsRepository>();
            services.AddSingleton<Repositories.Catalog.IProductRepository, Repositories.Catalog.UnavailableProductRepository>();
            services.AddSingleton<Repositories.Catalog.ICategoryRepository, Repositories.Catalog.UnavailableCategoryRepository>();
            services.AddSingleton<Repositories.Inventory.IInventoryRepository, Repositories.Inventory.UnavailableInventoryRepository>();
        }

        // ViewModels
        services.AddTransient<ViewModels.Identity.LoginViewModel>();
        services.AddTransient<ViewModels.Dashboard.DashboardViewModel>();
        services.AddTransient<ViewModels.Theme.ThemePreviewViewModel>();
        services.AddTransient<ViewModels.Shell.ShellViewModel>();
        services.AddTransient<ViewModels.Placeholder.PlaceholderViewModel>();
        services.AddTransient<ViewModels.Catalog.ProductListViewModel>();
        services.AddTransient<ViewModels.Catalog.ProductDetailViewModel>();
        services.AddTransient<ViewModels.Catalog.ProductCreateViewModel>();
        services.AddTransient<ViewModels.Catalog.ProductEditViewModel>();
        services.AddTransient<ViewModels.Catalog.CategoryListViewModel>();
        services.AddTransient<ViewModels.Inventory.StockListViewModel>();
        services.AddTransient<ViewModels.Inventory.StockDetailViewModel>();
        services.AddTransient<ViewModels.Inventory.StockAdjustmentViewModel>();
        services.AddTransient<ViewModels.Inventory.InventoryViewModel>();

        Services = services.BuildServiceProvider();
    }

    public Window? MainWindow { get; private set; }

    public Window? GetMainWindow() => MainWindow;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
#if DEBUG
        MainWindow.UseStudio();
#endif

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(Views.Identity.LoginPage), args.Arguments);
        }

        MainWindow.SetWindowIcon();
        MainWindow.Activate();
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
