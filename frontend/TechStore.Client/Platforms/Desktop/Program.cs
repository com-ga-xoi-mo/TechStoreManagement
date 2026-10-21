using Uno.UI.Hosting;

namespace TechStore.Client;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--probe-theme") ||
            string.Equals(Environment.GetEnvironmentVariable("TECHSTORE_PROBE_THEME"), "1", StringComparison.OrdinalIgnoreCase))
        {
            Testing.ThemeBindingProbeHarness.Run(args);
            return;
        }

        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
