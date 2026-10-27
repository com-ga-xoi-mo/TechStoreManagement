using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uno.Resizetizer;
using TechStore.Client.Services.Api;
using TechStore.Client.Styles;
using TechStore.Client.Services.Session;
using TechStore.Client.Views.Development;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Window;
using TechStore.Client.Views.Identity;
using TechStore.Client.Views.Shell;

namespace TechStore.Client;

public partial class App : Application
{
    private static ILoggerFactory? _loggerFactory;
    private ServiceProviderLifetime? _applicationServices;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        InitializeLogging();
    }

    public IServiceProvider? Services => _applicationServices?.Services;

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WinAppSDK must finish activation before Application.Resources can be read safely.
        // Register C# tokens here, before resolving services or creating any page.
        AppResources.Register(this);
        try
        {
            _applicationServices ??= new ServiceProviderLifetime(CreateServiceProvider());
            _applicationServices.Services.GetRequiredService<ApiClientOptions>().Validate();
        }
        catch (Exception exception)
        {
            _applicationServices?.Dispose();
            _applicationServices = null;
            if (_loggerFactory is not null)
            {
                FrontendStartupDiagnostics.LogConfigurationFailure(
                    _loggerFactory.CreateLogger<App>(),
                    exception);
            }
            Console.Error.WriteLine(FrontendStartupDiagnostics.CreateConfigurationFailureMessage(exception));
            throw;
        }

        MainWindow = new Window();
        _applicationServices.Services.GetRequiredService<ImageFilePicker>().Host = MainWindow;
        _applicationServices.Services.GetRequiredService<CsvFileSaver>().Host = MainWindow;
        MainWindow.Closed += OnMainWindowClosed;

        if (MainWindow.Content is null)
        {
            var sessionStore = _applicationServices.Services.GetRequiredService<ISessionStore>();
            sessionStore.SessionChanged += (s, e) =>
            {
                if (MainWindow is not null)
                {
                    if (e.Status == SessionStatus.Active)
                    {
                        MainWindow.Content = _applicationServices.Services.GetRequiredService<ShellPage>();
                    }
                    else
                    {
                        MainWindow.Content = _applicationServices.Services.GetRequiredService<LoginPage>();
                    }
                }
            };

            if (_applicationServices.Services.GetRequiredService<GalleryOptions>().Enabled)
            {
                _applicationServices.Services.GetRequiredService<MarkupReferenceNavigator>().Host = MainWindow;
                MainWindow.Content = _applicationServices.Services.GetRequiredService<ComponentGalleryPage>();
            }
            else
            {
#if DEBUG
                _applicationServices.Services.GetRequiredService<ShellRoutePageFactory>().VerifyAllRoutesResolve();
#endif
                if (sessionStore.Current.Status == SessionStatus.Active)
                {
                    MainWindow.Content = _applicationServices.Services.GetRequiredService<ShellPage>();
                }
                else
                {
                    MainWindow.Content = _applicationServices.Services.GetRequiredService<LoginPage>();
                }
            }
        }

        MainWindow.SetWindowIcon();
        // Ensure the current window is active
        MainWindow.Activate();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddFrontendFoundation(configuration, loggerFactory: _loggerFactory);
        services.AddFrontendUi(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
#if DEBUG
            ValidateOnBuild = true,
            ValidateScopes = true
#else
            ValidateOnBuild = false,
            ValidateScopes = false
#endif
        });
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        var applicationServices = Interlocked.Exchange(ref _applicationServices, null);
        if (applicationServices?.Services.GetService<IWindowLifetime>() is { } windowLifetime)
            windowLifetime.Close();
        if (MainWindow?.Content is ShellPage shellPage)
            shellPage.CloseShell();

        try
        {
            if (applicationServices is not null)
                await applicationServices.DisposeAsync();
        }
        catch (Exception exception)
        {
            _loggerFactory?.CreateLogger<App>().LogError(exception, "Failed to finish frontend shutdown cleanly.");
        }
        finally
        {
            var loggerFactory = Interlocked.Exchange(ref _loggerFactory, null);
            global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = NullLoggerFactory.Instance;
            loggerFactory?.Dispose();
        }
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
        if (_loggerFactory is not null)
        {
            return;
        }

        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        _loggerFactory = LoggerFactory.Create(builder =>
        {
#if DEBUG
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

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

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = _loggerFactory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
    }
}
