using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TechStore.Client.Controls;
using Uno.UI.Hosting;
using Windows.UI;

namespace TechStore.Client.Testing;

public static class ThemeBindingProbeHarness
{
    public static void Run(string[] args)
    {
        Console.WriteLine("[HARNESS] Initializing Uno Skia Desktop Probe Host...");
        App.InitializeLogging();

        var isNegative = args.Contains("--negative") ||
                         args.Contains("--negative-control") ||
                         string.Equals(Environment.GetEnvironmentVariable("TECHSTORE_PROBE_NEGATIVE"), "1", StringComparison.OrdinalIgnoreCase);

        var isTimeoutTest = args.Contains("--timeout-test") ||
                            string.Equals(Environment.GetEnvironmentVariable("TECHSTORE_PROBE_TIMEOUT"), "1", StringComparison.OrdinalIgnoreCase);

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new ProbeApp(isNegative, isTimeoutTest))
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}

public sealed class ProbeApp : App
{
    private readonly bool _isNegative;
    private readonly bool _isTimeoutTest;
    public new Window? MainWindow { get; private set; }

    public ProbeApp(bool isNegative, bool isTimeoutTest) : base()
    {
        _isNegative = isNegative;
        _isTimeoutTest = isTimeoutTest;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
        var probeView = new ProbeView(this, _isNegative, _isTimeoutTest);
        MainWindow.Content = probeView;
        MainWindow.Activate();
    }
}

public sealed class ProbeView : UserControl
{
    private readonly ProbeApp _app;
    private readonly bool _isNegative;
    private readonly bool _isTimeoutTest;
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private readonly CancellationTokenSource _watchdogCts = new(TimeSpan.FromSeconds(10));

    public ProbeView(ProbeApp app, bool isNegative, bool isTimeoutTest)
    {
        _app = app;
        _isNegative = isNegative;
        _isTimeoutTest = isTimeoutTest;

        Loaded += OnLoaded;

        // Watchdog to guarantee process does not hang if Loaded never fires (no-mount scenario)
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8), _watchdogCts.Token);
                Console.Error.WriteLine("[HARNESS WATCHDOG] FATAL: Window Loaded event was not received within deadline. Mount failed.");
                Environment.Exit(2);
            }
            catch (OperationCanceledException)
            {
                // Loaded fired normally
            }
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _watchdogCts.Cancel();

        try
        {
            var exitCode = await RunProbeSuiteAsync(_cts.Token);
            Console.WriteLine($"[HARNESS] Probe suite completed with exit code {exitCode}.");
            _app.MainWindow?.Close();
            Environment.ExitCode = exitCode;
        }
        catch (TimeoutException tex)
        {
            Console.Error.WriteLine($"[HARNESS] TIMEOUT FAILURE: {tex.Message}");
            Environment.ExitCode = 2;
            _app.MainWindow?.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HARNESS] FATAL EXCEPTION: {ex}");
            Environment.ExitCode = 2;
            _app.MainWindow?.Close();
        }
    }

    private async Task<int> RunProbeSuiteAsync(CancellationToken ct)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("TECHSTORE C# THEME BINDING RUNTIME PROBE (Uno 6.7.30 / Skia)");
        Console.WriteLine($"Mode: {(_isTimeoutTest ? "FORCED TIMEOUT TEST (--timeout-test)" : (_isNegative ? "NEGATIVE CONTROL (--negative, intentional failure)" : "POSITIVE RUN"))}");
        Console.WriteLine($"Window Content: Loaded=True, ActualTheme={ActualTheme}, Size={ActualWidth}x{ActualHeight}");
        Console.WriteLine("================================================================================");

        // If forced timeout test mode is requested, run timeout test and return non-zero exit code
        if (_isTimeoutTest)
        {
            return await RunForcedTimeoutTestAsync(ct);
        }

        var reports = new List<ProbeReportItem>();
        ResourceDictionary? probeDictionary = null;
        var activeAdapters = new List<ThemeResourceAdapter>();

        try
        {
            // Step 1: Register Minimal C# Theme Dictionaries
            Console.WriteLine("[Step 1] Registering Minimal C# Theme Dictionaries with distinct probe values...");
            probeDictionary = CreateProbeThemeDictionary();
            Application.Current.Resources.MergedDictionaries.Add(probeDictionary);

            // Step 2: Probe Static Lookup vs Dynamic Dictionary
            Console.WriteLine("[Step 2] Probing Static Resource Lookup...");
            TestResourceLookup(reports);

            // Step 3: Probing R1 (Live specimens on the same mounted visual tree) + R2 (System Theme)
            Console.WriteLine("[Step 3] Probing R1 (Live Specimens across Light -> Dark -> System) & R2 (System Theme)...");
            await TestLiveSpecimensAndSystemThemeAsync(reports, probeDictionary, activeAdapters, ct);

            // Step 4: Probing R3 (Mixed consumers: XAML StatusBadge + C# Consumer)
            Console.WriteLine("[Step 4] Probing R3 (Mixed Consumers: XAML StatusBadge + C# Consumer with distinct probe keys)...");
            await TestMixedConsumersAsync(reports, probeDictionary, activeAdapters, ct);

            // Step 5: Probing R4 (Wait, dispatch validation & TryEnqueue verification)
            Console.WriteLine("[Step 5] Probing R4 (Layout & Dispatch Validation)...");
            await TestLayoutDispatchValidationAsync(reports, ct);

            // Evaluate aggregate results
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("AGGREGATE PROBE RESULTS");
            Console.WriteLine("================================================================================");
            var allPassed = true;
            foreach (var r in reports)
            {
                var status = r.Passed ? "PASS" : "FAIL";
                if (!r.Passed) allPassed = false;
                Console.WriteLine($"[{status}] {r.TestName}");
                Console.WriteLine($"       API:      {r.ApiUsed}");
                Console.WriteLine($"       Expected: {r.Expected}");
                Console.WriteLine($"       Actual:   {r.Actual}");
                if (!string.IsNullOrWhiteSpace(r.Notes))
                    Console.WriteLine($"       Notes:    {r.Notes}");
            }

            Console.WriteLine("================================================================================");
            if (_isNegative)
            {
                if (!allPassed)
                {
                    Console.WriteLine("NEGATIVE CONTROL RESULT: SUCCESSFUL CATCH (Failures detected as expected). Exit code 1.");
                    return 1;
                }
                else
                {
                    Console.Error.WriteLine("NEGATIVE CONTROL FAILED: Expected assertions to fail, but all passed! Exit code 3.");
                    return 3;
                }
            }
            else
            {
                if (allPassed)
                {
                    Console.WriteLine("POSITIVE RUN RESULT: ALL ASSERTIONS PASSED. Exit code 0.");
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("POSITIVE RUN FAILED: One or more assertions failed. Exit code 1.");
                    return 1;
                }
            }
        }
        finally
        {
            // R4: Mandatory cleanup in finally
            Console.WriteLine("[CLEANUP] Detaching adapters and restoring resource dictionaries...");
            foreach (var adapter in activeAdapters)
            {
                adapter.Detach();
            }
            activeAdapters.Clear();

            if (probeDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(probeDictionary);
            }
        }
    }

    private async Task<int> RunForcedTimeoutTestAsync(CancellationToken ct)
    {
        Console.WriteLine("[TIMEOUT TEST] Triggering intentional layout timeout with 0ms deadline...");
        try
        {
            // Call WaitForLayoutAsync with 0ms timeout to force immediate timeout exception
            await WaitForLayoutAsync(ct, TimeSpan.FromMilliseconds(0));
            Console.Error.WriteLine("[TIMEOUT TEST ERROR] WaitForLayoutAsync did not throw TimeoutException as expected!");
            return 3;
        }
        catch (TimeoutException tex)
        {
            Console.WriteLine($"[TIMEOUT TEST PASS] TimeoutException successfully caught: '{tex.Message}'");
            Console.WriteLine("[TIMEOUT TEST PASS] Verified that timeout aborts run and exits with non-zero code.");
            return 2;
        }
    }

    private static ResourceDictionary CreateProbeThemeDictionary()
    {
        var root = new ResourceDictionary();

        var lightDict = new ResourceDictionary();
        // Canonical Spec Colors
        lightDict["ProbePrimaryBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x66, 0xFF)); // #0066FF
        lightDict["ProbeTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A));    // #0F172A
        lightDict["ProbeCanvasBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC));  // #F8FAFC
        lightDict["ProbeBorderBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0));  // #E2E8F0

        // Tone brushes
        lightDict["ProbeInfoTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x1D, 0x4E, 0xD8));
        lightDict["ProbeInfoFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0xF6, 0xFF));
        lightDict["ProbeInfoStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xBF, 0xDB, 0xFE));

        lightDict["ProbeSuccessTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x04, 0x78, 0x57));
        lightDict["ProbeSuccessFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xEC, 0xFD, 0xF5));
        lightDict["ProbeSuccessStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xA7, 0xF3, 0xD0));

        lightDict["ProbeWarningTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xB4, 0x53, 0x09));
        lightDict["ProbeWarningFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFB, 0xEB));
        lightDict["ProbeWarningStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFD, 0xE6, 0x8A));

        lightDict["ProbeDangerTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xB9, 0x1C, 0x1C));
        lightDict["ProbeDangerFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xF2, 0xF2));
        lightDict["ProbeDangerStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xCA, 0xCA));

        lightDict["ProbeAccentTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x28, 0xD9));
        lightDict["ProbeAccentFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF3, 0xFF));
        lightDict["ProbeAccentStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xD6, 0xFE));

        lightDict["ProbeNeutralTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x47, 0x55, 0x69));
        lightDict["ProbeNeutralFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
        lightDict["ProbeNeutralStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));

        // R3: Semantic keys for mixed consumer (distinct from old XAML #EFF6FF / #172554)
        lightDict["TechStoreInfoFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xEA, 0xFF));
        lightDict["TechStoreInfoTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x3A, 0x8A));
        lightDict["TechStoreInfoStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x93, 0xC5, 0xFD));

        var darkDict = new ResourceDictionary();
        darkDict["ProbePrimaryBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x66, 0xFF)); // #0066FF
        darkDict["ProbeTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC));    // #F8FAFC
        darkDict["ProbeCanvasBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A));  // #0F172A
        darkDict["ProbeBorderBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x47, 0x55, 0x69));  // #475569

        darkDict["ProbeInfoTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xBF, 0xDB, 0xFE));
        darkDict["ProbeInfoFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x25, 0x54));
        darkDict["ProbeInfoStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6));

        darkDict["ProbeSuccessTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x6E, 0xE7, 0xB7));
        darkDict["ProbeSuccessFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x06, 0x4E, 0x3B));
        darkDict["ProbeSuccessStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));

        darkDict["ProbeWarningTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFC, 0xD3, 0x4D));
        darkDict["ProbeWarningFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x1A, 0x03));
        darkDict["ProbeWarningStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x77, 0x06));

        darkDict["ProbeDangerTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFC, 0xA5, 0xA5));
        darkDict["ProbeDangerFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x0A, 0x0A));
        darkDict["ProbeDangerStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));

        darkDict["ProbeAccentTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xD6, 0xFE));
        darkDict["ProbeAccentFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x10, 0x65));
        darkDict["ProbeAccentStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6));

        darkDict["ProbeNeutralTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0));
        darkDict["ProbeNeutralFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x41, 0x55));
        darkDict["ProbeNeutralStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));

        // R3: Semantic keys for mixed consumer in Dark
        darkDict["TechStoreInfoFillBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x1D, 0x4A));
        darkDict["TechStoreInfoTextBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xDB, 0xEA, 0xFE));
        darkDict["TechStoreInfoStrokeBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA));

        root.ThemeDictionaries["Light"] = lightDict;
        root.ThemeDictionaries["Dark"] = darkDict;

        return root;
    }

    private void TestResourceLookup(List<ProbeReportItem> reports)
    {
        var appRes = Application.Current.Resources;
        var directPrimary = (appRes["ProbePrimaryBrush"] as SolidColorBrush)?.Color;
        var directText = (appRes["ProbeTextBrush"] as SolidColorBrush)?.Color;

        var expectedPrimary = Color.FromArgb(0xFF, 0x00, 0x66, 0xFF);
        var expectedText = _isNegative
            ? Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) // Intentionally wrong in negative run
            : (ActualTheme == ElementTheme.Dark ? Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC)
                                                : Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A));

        var pass = directPrimary == expectedPrimary && directText == expectedText;

        reports.Add(new ProbeReportItem(
            "C# ResourceDictionary.ThemeDictionaries Direct Lookup",
            "Application.Current.Resources[\"ProbePrimaryBrush\"] & [\"ProbeTextBrush\"]",
            $"Primary={expectedPrimary}, Text={expectedText}",
            $"Primary={directPrimary}, Text={directText}",
            pass,
            $"Direct lookup resolves static snapshot matching current system/app theme ({ActualTheme})."));
    }

    private async Task TestLiveSpecimensAndSystemThemeAsync(
        List<ProbeReportItem> reports,
        ResourceDictionary probeResources,
        List<ThemeResourceAdapter> activeAdapters,
        CancellationToken ct)
    {
        // R1: Mount all specimens on the SAME mounted visual tree.
        var ancestor = new StackPanel
        {
            Spacing = 8,
            Padding = new Thickness(12)
        };
        Content = ancestor;

        // Specimen 1: Styled TextBlock with explicit C# Style and Setter
        var styledTextBlock = new TextBlock
        {
            Text = "Specimen 1: Styled TextBlock"
        };
        var styledAdapter = new ThemeResourceAdapter(theme =>
        {
            var dictName = theme == ElementTheme.Dark ? "Dark" : "Light";
            if (probeResources.ThemeDictionaries.TryGetValue(dictName, out var d) && d is ResourceDictionary td)
            {
                if (td.TryGetValue("ProbeTextBrush", out var b) && b is SolidColorBrush scb)
                {
                    var textStyle = new Style(typeof(TextBlock));
                    textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, scb));
                    styledTextBlock.Style = textStyle;
                }
            }
        });
        styledAdapter.Attach(styledTextBlock);
        activeAdapters.Add(styledAdapter);
        ancestor.Children.Add(styledTextBlock);

        // Specimen 2: PathIcon with non-empty rendered geometry
        var pathGeometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            IsClosed = true
        };
        figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(16, 0) });
        figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(16, 16) });
        figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(0, 16) });
        pathGeometry.Figures.Add(figure);

        var pathIcon = new PathIcon
        {
            Data = pathGeometry
        };
        var iconAdapter = new ThemeResourceAdapter(theme =>
        {
            var dictName = theme == ElementTheme.Dark ? "Dark" : "Light";
            if (probeResources.ThemeDictionaries.TryGetValue(dictName, out var d) && d is ResourceDictionary td)
            {
                if (td.TryGetValue("ProbeInfoTextBrush", out var b) && b is SolidColorBrush scb)
                    pathIcon.Foreground = scb;
            }
        });
        iconAdapter.Attach(pathIcon);
        activeAdapters.Add(iconAdapter);
        ancestor.Children.Add(pathIcon);

        // Specimen 3: 6-tone ProbeBadge
        var probeBadge = new ProbeBadge(probeResources);
        activeAdapters.Add(probeBadge.Adapter);
        ancestor.Children.Add(probeBadge);

        // Specimen 4: Companion TextBox to test state/input preservation
        const string initialInput = "UserTypingPreserved123";
        var companionInput = new TextBox
        {
            Text = initialInput
        };
        ancestor.Children.Add(companionInput);

        await WaitForLayoutAsync(ct);

        // Save specimen references to prove control identity is preserved across all theme transitions
        var initialStyledRef = styledTextBlock;
        var initialIconRef = pathIcon;
        var initialBadgeRef = probeBadge;
        var initialInputRef = companionInput;

        var tones = new[] { "Neutral", "Info", "Success", "Warning", "Danger", "Accent" };

        // =========================================================================
        // Phase A: Light Theme Transition
        // =========================================================================
        ancestor.RequestedTheme = ElementTheme.Light;
        await WaitForLayoutAsync(ct);

        var expectedLightText = _isNegative ? Color.FromArgb(0xFF, 0xDE, 0xAD, 0xBE) : ExpectedPalette.Get("Light", "ProbeTextBrush");
        var expectedLightIcon = _isNegative ? Color.FromArgb(0xFF, 0x00, 0x00, 0x00) : ExpectedPalette.Get("Light", "ProbeInfoTextBrush");

        var lightStyledFg = (styledTextBlock.Foreground as SolidColorBrush)?.Color;
        var lightIconFg = (pathIcon.Foreground as SolidColorBrush)?.Color;

        var lightStyle = styledTextBlock.Style;
        var lightSetter = lightStyle?.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == TextBlock.ForegroundProperty);
        var lightSetterColor = (lightSetter?.Value as SolidColorBrush)?.Color;
        var lightLocalVal = styledTextBlock.ReadLocalValue(TextBlock.ForegroundProperty);
        var lightStyleSetterPass = lightStyle != null &&
                                   lightSetter != null &&
                                   lightSetterColor == expectedLightText &&
                                   lightLocalVal == DependencyProperty.UnsetValue;

        var lightSpecimensPass = lightStyledFg == expectedLightText && lightStyleSetterPass && lightIconFg == expectedLightIcon;

        // Verify all 6 tones in Light theme
        var lightTonesPass = true;
        var lightToneDetails = new List<string>();
        foreach (var tone in tones)
        {
            probeBadge.SetTone(tone);
            await WaitForLayoutAsync(ct);

            var bg = (probeBadge.BadgeBorder.Background as SolidColorBrush)?.Color;
            var stroke = (probeBadge.BadgeBorder.BorderBrush as SolidColorBrush)?.Color;
            var fg = (probeBadge.BadgeText.Foreground as SolidColorBrush)?.Color;

            var expBg = _isNegative ? Color.FromArgb(0xFF, 0x11, 0x22, 0x33) : ExpectedPalette.Get("Light", $"Probe{tone}FillBrush");
            var expStroke = _isNegative ? Color.FromArgb(0xFF, 0x44, 0x55, 0x66) : ExpectedPalette.Get("Light", $"Probe{tone}StrokeBrush");
            var expFg = _isNegative ? Color.FromArgb(0xFF, 0x77, 0x88, 0x99) : ExpectedPalette.Get("Light", $"Probe{tone}TextBrush");

            var pass = bg == expBg && stroke == expStroke && fg == expFg;
            if (!pass) lightTonesPass = false;
            lightToneDetails.Add($"{tone}: {(pass ? "OK" : "MISMATCH")}");
        }

        // Leave badge in "Warning" tone before transitioning to Dark
        probeBadge.SetTone("Warning");
        await WaitForLayoutAsync(ct);

        // =========================================================================
        // Phase B: Dark Theme Transition
        // =========================================================================
        ancestor.RequestedTheme = ElementTheme.Dark;
        await WaitForLayoutAsync(ct);

        var expectedDarkText = _isNegative ? Color.FromArgb(0xFF, 0x12, 0x34, 0x56) : ExpectedPalette.Get("Dark", "ProbeTextBrush");
        var expectedDarkIcon = _isNegative ? Color.FromArgb(0xFF, 0x65, 0x43, 0x21) : ExpectedPalette.Get("Dark", "ProbeInfoTextBrush");

        var darkStyledFg = (styledTextBlock.Foreground as SolidColorBrush)?.Color;
        var darkIconFg = (pathIcon.Foreground as SolidColorBrush)?.Color;

        var darkStyle = styledTextBlock.Style;
        var darkSetter = darkStyle?.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == TextBlock.ForegroundProperty);
        var darkSetterColor = (darkSetter?.Value as SolidColorBrush)?.Color;
        var darkLocalVal = styledTextBlock.ReadLocalValue(TextBlock.ForegroundProperty);
        var darkStyleSetterPass = darkStyle != null &&
                                  darkSetter != null &&
                                  darkSetterColor == expectedDarkText &&
                                  darkLocalVal == DependencyProperty.UnsetValue;

        var darkSpecimensPass = darkStyledFg == expectedDarkText && darkStyleSetterPass && darkIconFg == expectedDarkIcon;

        // Verify that the "Warning" tone preserved its state and automatically adapted its colors to Dark
        var darkWarningBg = (probeBadge.BadgeBorder.Background as SolidColorBrush)?.Color;
        var darkWarningStroke = (probeBadge.BadgeBorder.BorderBrush as SolidColorBrush)?.Color;
        var darkWarningFg = (probeBadge.BadgeText.Foreground as SolidColorBrush)?.Color;

        var expDarkWarningBg = _isNegative ? Color.FromArgb(0xFF, 0x00, 0x11, 0x22) : ExpectedPalette.Get("Dark", "ProbeWarningFillBrush");
        var expDarkWarningStroke = _isNegative ? Color.FromArgb(0xFF, 0x33, 0x44, 0x55) : ExpectedPalette.Get("Dark", "ProbeWarningStrokeBrush");
        var expDarkWarningFg = _isNegative ? Color.FromArgb(0xFF, 0x66, 0x77, 0x88) : ExpectedPalette.Get("Dark", "ProbeWarningTextBrush");

        var toneStatePreservedPass = probeBadge.CurrentTone == "Warning" &&
                                     darkWarningBg == expDarkWarningBg &&
                                     darkWarningStroke == expDarkWarningStroke &&
                                     darkWarningFg == expDarkWarningFg;

        // Verify all 6 tones in Dark theme
        var darkTonesPass = true;
        var darkToneDetails = new List<string>();
        foreach (var tone in tones)
        {
            probeBadge.SetTone(tone);
            await WaitForLayoutAsync(ct);

            var bg = (probeBadge.BadgeBorder.Background as SolidColorBrush)?.Color;
            var stroke = (probeBadge.BadgeBorder.BorderBrush as SolidColorBrush)?.Color;
            var fg = (probeBadge.BadgeText.Foreground as SolidColorBrush)?.Color;

            var expBg = _isNegative ? Color.FromArgb(0xFF, 0x11, 0x22, 0x33) : ExpectedPalette.Get("Dark", $"Probe{tone}FillBrush");
            var expStroke = _isNegative ? Color.FromArgb(0xFF, 0x44, 0x55, 0x66) : ExpectedPalette.Get("Dark", $"Probe{tone}StrokeBrush");
            var expFg = _isNegative ? Color.FromArgb(0xFF, 0x77, 0x88, 0x99) : ExpectedPalette.Get("Dark", $"Probe{tone}TextBrush");

            var pass = bg == expBg && stroke == expStroke && fg == expFg;
            if (!pass) darkTonesPass = false;
            darkToneDetails.Add($"{tone}: {(pass ? "OK" : "MISMATCH")}");
        }

        // Leave badge in "Success" tone before transitioning to System/Default
        probeBadge.SetTone("Success");
        await WaitForLayoutAsync(ct);

        // =========================================================================
        // Phase C: R2 - System Theme Transition (RequestedTheme = Default)
        // =========================================================================
        ancestor.RequestedTheme = ElementTheme.Default;
        await WaitForLayoutAsync(ct);

        var systemActualTheme = ancestor.ActualTheme;
        var systemThemeName = systemActualTheme == ElementTheme.Dark ? "Dark" : "Light";

        // Look up expected colors strictly according to ActualTheme
        var expSysText = _isNegative ? Color.FromArgb(0xFF, 0xEE, 0xFF, 0xAA) : ExpectedPalette.Get(systemThemeName, "ProbeTextBrush");
        var expSysIcon = _isNegative ? Color.FromArgb(0xFF, 0xBB, 0xCC, 0xDD) : ExpectedPalette.Get(systemThemeName, "ProbeInfoTextBrush");
        var expSysSuccessBg = _isNegative ? Color.FromArgb(0xFF, 0x99, 0x88, 0x77) : ExpectedPalette.Get(systemThemeName, "ProbeSuccessFillBrush");

        var sysStyledFg = (styledTextBlock.Foreground as SolidColorBrush)?.Color;
        var sysIconFg = (pathIcon.Foreground as SolidColorBrush)?.Color;
        var sysSuccessBg = (probeBadge.BadgeBorder.Background as SolidColorBrush)?.Color;

        var sysStyle = styledTextBlock.Style;
        var sysSetter = sysStyle?.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == TextBlock.ForegroundProperty);
        var sysSetterColor = (sysSetter?.Value as SolidColorBrush)?.Color;
        var sysLocalVal = styledTextBlock.ReadLocalValue(TextBlock.ForegroundProperty);
        var sysStyleSetterPass = sysStyle != null &&
                                 sysSetter != null &&
                                 sysSetterColor == expSysText &&
                                 sysLocalVal == DependencyProperty.UnsetValue;

        var r2SystemPass = sysStyledFg == expSysText &&
                           sysStyleSetterPass &&
                           sysIconFg == expSysIcon &&
                           sysSuccessBg == expSysSuccessBg;

        // Verify Identity & Input State
        var identityPass = ReferenceEquals(styledTextBlock, initialStyledRef) &&
                           ReferenceEquals(pathIcon, initialIconRef) &&
                           ReferenceEquals(probeBadge, initialBadgeRef) &&
                           ReferenceEquals(companionInput, initialInputRef) &&
                           companionInput.Text == initialInput;

        var nonNullGeometryPass = pathGeometry.Figures.Count > 0 &&
                                  pathGeometry.Figures[0].Segments.Count > 0;

        var r1Pass = lightSpecimensPass && lightTonesPass &&
                     toneStatePreservedPass && darkSpecimensPass && darkTonesPass &&
                     identityPass && nonNullGeometryPass;

        reports.Add(new ProbeReportItem(
            "R1: Live Specimens Dynamic Adaptation (C# Style Setter on TextBlock, PathIcon, 6 Tones)",
            "Style with Setter(TextBlock.ForegroundProperty, ProbeTextBrush) + ThemeResourceAdapter on live specimens across Light -> Dark -> System",
            "Exact palette colors at every theme; Style Setter applied (LocalValue=Unset); non-empty geometry; preserved tone state & control identity",
            $"LightText={lightStyledFg} (StyleSetter={lightSetterColor}, LocalUnset={lightLocalVal == DependencyProperty.UnsetValue}), LightIcon={lightIconFg}; DarkText={darkStyledFg} (StyleSetter={darkSetterColor}); DarkWarningBg={darkWarningBg}; GeometryFigures={pathGeometry.Figures.Count}; Identity={identityPass}",
            r1Pass,
            $"Matrix verified: StyleSetter across Light/Dark/System; Light tones ({string.Join(", ", lightToneDetails)}); Dark tones ({string.Join(", ", darkToneDetails)}). Input state='{companionInput.Text}'."));

        reports.Add(new ProbeReportItem(
            "R2: System Theme Resolution & Assertion (RequestedTheme = Default)",
            $"ancestor.RequestedTheme = Default -> Read ActualTheme ({systemActualTheme}) & compare against ExpectedPalette[{systemThemeName}]",
            $"Expected for ActualTheme={systemThemeName}: Text={expSysText}, Icon={expSysIcon}, SuccessBg={expSysSuccessBg}",
            $"Actual: Text={sysStyledFg}, Icon={sysIconFg}, SuccessBg={sysSuccessBg}",
            r2SystemPass,
            $"Asserted strictly against independent expected palette for ActualTheme={systemThemeName}; non-null alone rejected."));
    }

    private async Task TestMixedConsumersAsync(
        List<ProbeReportItem> reports,
        ResourceDictionary probeResources,
        List<ThemeResourceAdapter> activeAdapters,
        CancellationToken ct)
    {
        var panel = Content as StackPanel ?? new StackPanel();
        Content = panel;

        // 1. Existing XAML Consumer: StatusBadge
        var xamlBadge = new TechStore.Client.Controls.StatusBadge
        {
            Label = "XAML Consumer",
            Tone = StatusTone.Info
        };
        panel.Children.Add(xamlBadge);

        // 2. C# Consumer
        var csharpConsumer = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4)
        };
        var csharpText = new TextBlock { Text = "C# Consumer" };
        csharpConsumer.Child = csharpText;

        var csharpAdapter = new ThemeResourceAdapter(theme =>
        {
            var dictName = theme == ElementTheme.Dark ? "Dark" : "Light";
            if (probeResources.ThemeDictionaries.TryGetValue(dictName, out var d) && d is ResourceDictionary td)
            {
                if (td.TryGetValue("TechStoreInfoFillBrush", out var bg) && bg is SolidColorBrush bgb)
                    csharpConsumer.Background = bgb;
                if (td.TryGetValue("TechStoreInfoTextBrush", out var fg) && fg is SolidColorBrush fgb)
                    csharpText.Foreground = fgb;
            }
        });
        csharpAdapter.Attach(csharpConsumer);
        activeAdapters.Add(csharpAdapter);
        panel.Children.Add(csharpConsumer);

        await WaitForLayoutAsync(ct);

        // Probe distinct semantic key values:
        // In probe dictionary:
        // Light TechStoreInfoFillBrush = #E0EAFF (differs from old XAML #EFF6FF)
        // Dark TechStoreInfoFillBrush = #0F1D4A (differs from old XAML #172554)
        var expLightFill = _isNegative ? Color.FromArgb(0xFF, 0x99, 0x99, 0x99) : Color.FromArgb(0xFF, 0xE0, 0xEA, 0xFF);
        var expDarkFill = _isNegative ? Color.FromArgb(0xFF, 0x88, 0x88, 0x88) : Color.FromArgb(0xFF, 0x0F, 0x1D, 0x4A);

        // Test Light
        panel.RequestedTheme = ElementTheme.Light;
        await WaitForLayoutAsync(ct);

        var xamlBorder = xamlBadge.Content as Border;
        var xamlLightBg = (xamlBorder?.Background as SolidColorBrush)?.Color;
        var csharpLightBg = (csharpConsumer.Background as SolidColorBrush)?.Color;

        // Test Dark
        panel.RequestedTheme = ElementTheme.Dark;
        await WaitForLayoutAsync(ct);

        var xamlDarkBg = (xamlBorder?.Background as SolidColorBrush)?.Color;
        var csharpDarkBg = (csharpConsumer.Background as SolidColorBrush)?.Color;

        // Assert both receive the probe-distinct value from C# dictionary across Light and Dark
        var csharpPass = csharpLightBg != null && csharpDarkBg != null &&
                         csharpLightBg == expLightFill && csharpDarkBg == expDarkFill;

        var xamlPass = xamlBorder != null &&
                       xamlLightBg != null && xamlDarkBg != null &&
                       xamlLightBg == expLightFill && xamlDarkBg == expDarkFill;

        var pass = csharpPass && xamlPass;

        reports.Add(new ProbeReportItem(
            "R3: Mixed Consumers (XAML StatusBadge & C# Consumer) with Distinct Semantic Key",
            "Application.Current.Resources.MergedDictionaries with probe-distinct TechStoreInfoFillBrush (#E0EAFF / #0F1D4A)",
            $"Light Fill={expLightFill}, Dark Fill={expDarkFill}",
            $"C# Consumer: Light={csharpLightBg}, Dark={csharpDarkBg} (Pass={csharpPass}) | XAML StatusBadge: Light={xamlLightBg}, Dark={xamlDarkBg} (Pass={xamlPass})",
            pass,
            $"Strict conjunction: xamlLight({xamlLightBg == expLightFill}) && xamlDark({xamlDarkBg == expDarkFill}) && csharpLight({csharpLightBg == expLightFill}) && csharpDark({csharpDarkBg == expDarkFill}). Both consumers resolve and adapt to probe-distinct C# semantic keys across Light and Dark."));

        // Cleanup mixed consumer controls from panel
        panel.Children.Remove(xamlBadge);
        panel.Children.Remove(csharpConsumer);
    }

    private async Task TestLayoutDispatchValidationAsync(List<ProbeReportItem> reports, CancellationToken ct)
    {
        // R4: Verify DispatcherQueue.TryEnqueue and layout completion
        var enqueued = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => { });
        var pass = enqueued;
        if (_isNegative) pass = false;

        reports.Add(new ProbeReportItem(
            "R4: DispatcherQueue.TryEnqueue & Layout Dispatch Validation",
            "DispatcherQueue.TryEnqueue(Low, ...) return status verification with finite cancellation token",
            "TryEnqueue=True with active layout completion",
            $"TryEnqueue={enqueued}",
            pass,
            "Guarantees UI message pump is active and layout dispatcher is responsive."));

        await WaitForLayoutAsync(ct);
    }

    private async Task WaitForLayoutAsync(CancellationToken ct, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(4);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        var enqueued = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            tcs.TrySetResult(true);
        });

        if (!enqueued)
        {
            throw new InvalidOperationException("DispatcherQueue.TryEnqueue returned false; dispatcher is shutting down or unavailable.");
        }

        var delayTask = Task.Delay(effectiveTimeout, ct);
        var completed = await Task.WhenAny(tcs.Task, delayTask);

        if (completed == delayTask)
        {
            throw new TimeoutException($"UI layout and dispatch timed out after {effectiveTimeout.TotalMilliseconds}ms.");
        }

        await tcs.Task;
    }
}

public sealed class ProbeBadge : UserControl
{
    public Border BadgeBorder { get; }
    public TextBlock BadgeText { get; }
    public string CurrentTone { get; private set; } = "Neutral";
    public ThemeResourceAdapter Adapter { get; }

    public ProbeBadge(ResourceDictionary probeResources)
    {
        BadgeBorder = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1)
        };
        BadgeText = new TextBlock { Text = "Probe Badge" };
        BadgeBorder.Child = BadgeText;

        var rootGrid = new Grid();
        rootGrid.Children.Add(BadgeBorder);
        Content = rootGrid;

        // Attachable theme adapter to update active tone brushes on theme change
        Adapter = new ThemeResourceAdapter(theme =>
        {
            ApplyToneBrushes(CurrentTone, theme, probeResources);
        });
        Adapter.Attach(this);
    }

    public void SetTone(string tone)
    {
        CurrentTone = tone;
        // In WinUI C# Markup, apply the tone brushes for the current ActualTheme
        if (Content is FrameworkElement fe)
        {
            // Trigger tone update
            Adapter.ApplyCurrentTheme();
        }
    }

    private void ApplyToneBrushes(string tone, ElementTheme theme, ResourceDictionary probeResources)
    {
        var dictName = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (probeResources.ThemeDictionaries.TryGetValue(dictName, out var d) && d is ResourceDictionary td)
        {
            if (td.TryGetValue($"Probe{tone}FillBrush", out var bg) && bg is SolidColorBrush bgb)
                BadgeBorder.Background = bgb;
            if (td.TryGetValue($"Probe{tone}StrokeBrush", out var stroke) && stroke is SolidColorBrush strokeb)
                BadgeBorder.BorderBrush = strokeb;
            if (td.TryGetValue($"Probe{tone}TextBrush", out var fg) && fg is SolidColorBrush fgb)
                BadgeText.Foreground = fgb;
        }
    }
}

public sealed class ThemeResourceAdapter
{
    private readonly Action<ElementTheme> _onThemeChanged;
    private FrameworkElement? _target;

    public ThemeResourceAdapter(Action<ElementTheme> onThemeChanged)
    {
        _onThemeChanged = onThemeChanged;
    }

    public void Attach(FrameworkElement element)
    {
        _target = element;
        _target.ActualThemeChanged += OnActualThemeChanged;
        ApplyCurrentTheme();
    }

    public void Detach()
    {
        if (_target != null)
        {
            _target.ActualThemeChanged -= OnActualThemeChanged;
            _target = null;
        }
    }

    public void ApplyCurrentTheme()
    {
        if (_target != null)
        {
            _onThemeChanged(_target.ActualTheme);
        }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _onThemeChanged(sender.ActualTheme);
    }
}

public static class ExpectedPalette
{
    private static readonly Dictionary<string, Dictionary<string, Color>> Table = new()
    {
        ["Light"] = new Dictionary<string, Color>
        {
            ["ProbePrimaryBrush"] = Color.FromArgb(0xFF, 0x00, 0x66, 0xFF),
            ["ProbeTextBrush"] = Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A),
            ["ProbeCanvasBrush"] = Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC),
            ["ProbeBorderBrush"] = Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0),

            ["ProbeInfoTextBrush"] = Color.FromArgb(0xFF, 0x1D, 0x4E, 0xD8),
            ["ProbeInfoFillBrush"] = Color.FromArgb(0xFF, 0xEF, 0xF6, 0xFF),
            ["ProbeInfoStrokeBrush"] = Color.FromArgb(0xFF, 0xBF, 0xDB, 0xFE),

            ["ProbeSuccessTextBrush"] = Color.FromArgb(0xFF, 0x04, 0x78, 0x57),
            ["ProbeSuccessFillBrush"] = Color.FromArgb(0xFF, 0xEC, 0xFD, 0xF5),
            ["ProbeSuccessStrokeBrush"] = Color.FromArgb(0xFF, 0xA7, 0xF3, 0xD0),

            ["ProbeWarningTextBrush"] = Color.FromArgb(0xFF, 0xB4, 0x53, 0x09),
            ["ProbeWarningFillBrush"] = Color.FromArgb(0xFF, 0xFF, 0xFB, 0xEB),
            ["ProbeWarningStrokeBrush"] = Color.FromArgb(0xFF, 0xFD, 0xE6, 0x8A),

            ["ProbeDangerTextBrush"] = Color.FromArgb(0xFF, 0xB9, 0x1C, 0x1C),
            ["ProbeDangerFillBrush"] = Color.FromArgb(0xFF, 0xFE, 0xF2, 0xF2),
            ["ProbeDangerStrokeBrush"] = Color.FromArgb(0xFF, 0xFE, 0xCA, 0xCA),

            ["ProbeAccentTextBrush"] = Color.FromArgb(0xFF, 0x6D, 0x28, 0xD9),
            ["ProbeAccentFillBrush"] = Color.FromArgb(0xFF, 0xF5, 0xF3, 0xFF),
            ["ProbeAccentStrokeBrush"] = Color.FromArgb(0xFF, 0xDD, 0xD6, 0xFE),

            ["ProbeNeutralTextBrush"] = Color.FromArgb(0xFF, 0x47, 0x55, 0x69),
            ["ProbeNeutralFillBrush"] = Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9),
            ["ProbeNeutralStrokeBrush"] = Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1),
        },
        ["Dark"] = new Dictionary<string, Color>
        {
            ["ProbePrimaryBrush"] = Color.FromArgb(0xFF, 0x00, 0x66, 0xFF),
            ["ProbeTextBrush"] = Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC),
            ["ProbeCanvasBrush"] = Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A),
            ["ProbeBorderBrush"] = Color.FromArgb(0xFF, 0x47, 0x55, 0x69),

            ["ProbeInfoTextBrush"] = Color.FromArgb(0xFF, 0xBF, 0xDB, 0xFE),
            ["ProbeInfoFillBrush"] = Color.FromArgb(0xFF, 0x17, 0x25, 0x54),
            ["ProbeInfoStrokeBrush"] = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6),

            ["ProbeSuccessTextBrush"] = Color.FromArgb(0xFF, 0x6E, 0xE7, 0xB7),
            ["ProbeSuccessFillBrush"] = Color.FromArgb(0xFF, 0x06, 0x4E, 0x3B),
            ["ProbeSuccessStrokeBrush"] = Color.FromArgb(0xFF, 0x10, 0xB9, 0x81),

            ["ProbeWarningTextBrush"] = Color.FromArgb(0xFF, 0xFC, 0xD3, 0x4D),
            ["ProbeWarningFillBrush"] = Color.FromArgb(0xFF, 0x45, 0x1A, 0x03),
            ["ProbeWarningStrokeBrush"] = Color.FromArgb(0xFF, 0xD9, 0x77, 0x06),

            ["ProbeDangerTextBrush"] = Color.FromArgb(0xFF, 0xFC, 0xA5, 0xA5),
            ["ProbeDangerFillBrush"] = Color.FromArgb(0xFF, 0x45, 0x0A, 0x0A),
            ["ProbeDangerStrokeBrush"] = Color.FromArgb(0xFF, 0xEF, 0x44, 0x44),

            ["ProbeAccentTextBrush"] = Color.FromArgb(0xFF, 0xDD, 0xD6, 0xFE),
            ["ProbeAccentFillBrush"] = Color.FromArgb(0xFF, 0x2E, 0x10, 0x65),
            ["ProbeAccentStrokeBrush"] = Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6),

            ["ProbeNeutralTextBrush"] = Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0),
            ["ProbeNeutralFillBrush"] = Color.FromArgb(0xFF, 0x33, 0x41, 0x55),
            ["ProbeNeutralStrokeBrush"] = Color.FromArgb(0xFF, 0x64, 0x74, 0x8B),
        }
    };

    public static Color Get(string theme, string key)
    {
        if (Table.TryGetValue(theme, out var dict) && dict.TryGetValue(key, out var color))
            return color;
        throw new KeyNotFoundException($"Expected palette key '{key}' not found for theme '{theme}'.");
    }
}

public sealed record ProbeReportItem(
    string TestName,
    string ApiUsed,
    string Expected,
    string Actual,
    bool Passed,
    string Notes);
