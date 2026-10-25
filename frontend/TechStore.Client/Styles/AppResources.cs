using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Extensions.Markup;

namespace TechStore.Client.Styles;

/// <summary>
/// Builds the shared resource dictionary from the pure token tables (AppColors, AppTypography,
/// AppMetrics, AppIcons, AppControlStyles) and merges it into Application.Resources.
/// Same key names as the legacy Styles/*.xaml so XAML pages keep working.
/// </summary>
public static class AppResources
{
    /// <summary>
    /// Returns the registered Style object for a key. Use this (instead of <c>AppTypography.Styles.*</c>) when the same
    /// element also sets its own theme-bound property such as Foreground, so the style cannot be applied after it.
    /// </summary>
    public static Style GetStyle(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Style style
            ? style
            : throw new KeyNotFoundException($"Style '{key}' is not registered. Call AppResources.Register first.");

    private const string MergedMarker = "TechStoreTokensRegistered";

    /// <summary>Merge the tokens after XAML activation and before the first page is created.</summary>
    public static void Register(Application application)
    {
        var resources = application.Resources;
        if (resources.ContainsKey(MergedMarker)) return;
        resources[MergedMarker] = true;
        var dictionary = Build(resources);
        resources.MergedDictionaries.Add(dictionary);
        // Icons carry a ThemeResource foreground, so they are created once the colors are visible from Application.Resources.
        AddIcons(dictionary);
        // Same for the XAML-parsed styles: their ThemeResource references must be resolvable when parsed.
        AddThemedStyles(dictionary);
    }

    public static ResourceDictionary Build(ResourceDictionary host)
    {
        var dictionary = new ResourceDictionary();

        foreach (var (key, value) in AppMetrics.Doubles) dictionary[key] = value;
        foreach (var (key, value) in AppMetrics.CornerRadii) dictionary[key] = new CornerRadius(value);
        foreach (var (key, value) in AppMetrics.Thicknesses) dictionary[key] = new Thickness(value.Horizontal, value.Vertical, value.Horizontal, value.Vertical);

        var fonts = new Dictionary<string, FontFamily>();
        foreach (var (key, value) in AppTypography.FontFamilies)
        {
            fonts[key] = new FontFamily(value);
            dictionary[key] = fonts[key];
        }

        dictionary.ThemeDictionaries["Light"] = BuildColorTheme(AppColors.Light, host);
        dictionary.ThemeDictionaries["Dark"] = BuildColorTheme(AppColors.Dark, host);
        dictionary.ThemeDictionaries["HighContrast"] = BuildColorTheme(AppColors.HighContrast, host);

        AddControlStyles(dictionary, host, fonts);
        return dictionary;
    }

    private static ResourceDictionary BuildColorTheme(IReadOnlyDictionary<string, string> table, ResourceDictionary host)
    {
        var theme = new ResourceDictionary();
        foreach (var (key, value) in table)
        {
            theme[key] = value.StartsWith('#')
                ? new SolidColorBrush(ParseColor(value))
                : ResolveSystemBrush(host, value);
        }
        return theme;
    }

    private static Windows.UI.Color ParseColor(string hex) =>
        ColorHelper.FromArgb(
            255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));

    // HighContrast entries point at system color brushes. Resolve them from the host resources
    // (WinUI defines them); fall back to a neutral brush only if the platform does not expose them.
    private static Brush ResolveSystemBrush(ResourceDictionary host, string systemKey) =>
        host.TryGetValue(systemKey, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(systemKey.Contains("Text", StringComparison.Ordinal) ? Colors.White : Colors.Black);

    // Why XamlReader: a Setter whose value is a ThemeResource must be created by the XAML parser to follow a runtime
    // theme switch. Setters built by Markup (Style<T>().Setters(s => s.Background(ThemeResource.Get<Brush>(key))))
    // are resolved once and stay stale (see design.md D3). Only styles that carry a theme color are built here;
    // everything else stays in Markup. No .xaml file is involved.
    private const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static string TextStyleXaml(string key, string fontKey, double size, string weight, double lineHeight, string? basedOn = null, string foreground = AppColors.Text) =>
        $"<Style x:Key=\"{key}\" TargetType=\"TextBlock\"{(basedOn is null ? "" : $" BasedOn=\"{{StaticResource {basedOn}}}\"")}>" +
        (basedOn is null
            ? $"<Setter Property=\"FontFamily\" Value=\"{AppTypography.FontFamilies[fontKey]}\"/><Setter Property=\"FontSize\" Value=\"{size}\"/>" +
              $"<Setter Property=\"FontWeight\" Value=\"{weight}\"/><Setter Property=\"LineHeight\" Value=\"{lineHeight}\"/><Setter Property=\"TextWrapping\" Value=\"Wrap\"/>"
            : "") +
        $"<Setter Property=\"Foreground\" Value=\"{{ThemeResource {foreground}}}\"/></Style>";

    private static void AddThemedStyles(ResourceDictionary target)
    {
        var radius = AppMetrics.CornerRadii[AppMetrics.PanelRadius];
        // Thickness tokens are (horizontal, vertical): emit "h,v" so the style matches the token resources exactly.
        var paddingToken = AppMetrics.Thicknesses[AppMetrics.PanelPadding];
        var strokeToken = AppMetrics.Thicknesses[AppMetrics.StrokeThickness];
        var padding = FormattableString.Invariant($"{paddingToken.Horizontal},{paddingToken.Vertical}");
        var stroke = FormattableString.Invariant($"{strokeToken.Horizontal},{strokeToken.Vertical}");
        var controlRadius = AppMetrics.CornerRadii[AppMetrics.ControlRadius];
        var minHeight = AppMetrics.Doubles[AppMetrics.ControlMinHeight];
        var xaml =
            $"<ResourceDictionary {Ns}>" +
            TextStyleXaml(AppTypography.BodyText, AppTypography.RegularFont, 13, "Normal", 20) +
            TextStyleXaml(AppTypography.HeadingText, AppTypography.SemiBoldFont, 20, "SemiBold", 28) +
            TextStyleXaml(AppTypography.SectionText, AppTypography.SemiBoldFont, 14, "SemiBold", 20) +
            TextStyleXaml(AppTypography.CaptionText, AppTypography.MediumFont, 12, "Medium", 18) +
            TextStyleXaml(AppTypography.MonoText, AppTypography.MonoFont, 12, "Normal", 18) +
            $"<Style x:Key=\"{AppControlStyles.Panel}\" TargetType=\"Border\">" +
            $"<Setter Property=\"Background\" Value=\"{{ThemeResource {AppColors.Surface}}}\"/>" +
            $"<Setter Property=\"BorderBrush\" Value=\"{{ThemeResource {AppColors.Stroke}}}\"/>" +
            $"<Setter Property=\"BorderThickness\" Value=\"{stroke}\"/><Setter Property=\"CornerRadius\" Value=\"{radius}\"/><Setter Property=\"Padding\" Value=\"{padding}\"/></Style>" +
            $"<Style x:Key=\"{AppControlStyles.DangerButton}\" TargetType=\"Button\">" +
            $"<Setter Property=\"FontFamily\" Value=\"{AppTypography.FontFamilies[AppTypography.MediumFont]}\"/><Setter Property=\"FontSize\" Value=\"13\"/>" +
            $"<Setter Property=\"CornerRadius\" Value=\"{controlRadius}\"/><Setter Property=\"MinHeight\" Value=\"{minHeight}\"/><Setter Property=\"Padding\" Value=\"14,8,14,8\"/>" +
            $"<Setter Property=\"Foreground\" Value=\"{{ThemeResource {AppColors.DangerText}}}\"/><Setter Property=\"Background\" Value=\"{{ThemeResource {AppColors.DangerFill}}}\"/></Style>" +
            "</ResourceDictionary>";
        Merge(target, xaml);
        // These two use BasedOn caption, which must already be visible from Application.Resources, so they are parsed second.
        Merge(target, $"<ResourceDictionary {Ns}>" +
            TextStyleXaml(AppControlStyles.FieldError, "", 0, "", 0, AppTypography.CaptionText, AppColors.DangerText) +
            TextStyleXaml(AppControlStyles.TableHeader, "", 0, "", 0, AppTypography.CaptionText, AppColors.SecondaryText) +
            "</ResourceDictionary>");
    }

    private static void Merge(ResourceDictionary target, string xaml)
    {
        var parsed = (ResourceDictionary)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
        foreach (var (key, value) in parsed) target[key] = value;
    }

    private static void AddControlStyles(ResourceDictionary d, ResourceDictionary host, IReadOnlyDictionary<string, FontFamily> fonts)
    {
        var medium = fonts[AppTypography.MediumFont];
        var regular = fonts[AppTypography.RegularFont];
        var controlRadius = new CornerRadius(AppMetrics.CornerRadii[AppMetrics.ControlRadius]);
        var minHeight = AppMetrics.Doubles[AppMetrics.ControlMinHeight];

        Put(d, AppControlStyles.Button, new Style<Button>().Setters(s => s
            .FontFamily(medium).FontSize(13).CornerRadius(controlRadius).MinHeight(minHeight).Padding(new Thickness(14, 8, 14, 8))));

        var accent = host.TryGetValue("AccentButtonStyle", out var accentStyle) ? accentStyle as Style : null;
        Style primary = (Style)new Style<Button>().Setters(s => s
            .FontFamily(medium).FontSize(13).CornerRadius(controlRadius).MinHeight(minHeight).Padding(new Thickness(14, 8, 14, 8)));
        // Set BasedOn on the converted WinUI Style; the Markup builder's BasedOn crashes WinAppSDK at startup.
        if (accent is not null) primary.BasedOn = accent;
        d[AppControlStyles.PrimaryButton] = primary;

        Put(d, AppControlStyles.QuietButton, new Style<Button>().Setters(s => s
            .FontFamily(medium).FontSize(13).CornerRadius(controlRadius).MinHeight(minHeight).Padding(new Thickness(14, 8, 14, 8))
            .Background(new SolidColorBrush(Colors.Transparent))));

        Put(d, AppControlStyles.TextBox, new Style<TextBox>().Setters(s => s
            .FontFamily(regular).FontSize(14).MinHeight(minHeight).CornerRadius(controlRadius).HorizontalAlignment(HorizontalAlignment.Stretch)));
        Put(d, AppControlStyles.AutoSuggestBox, new Style<AutoSuggestBox>().Setters(s => s
            .FontFamily(regular).FontSize(14).MinHeight(minHeight).CornerRadius(controlRadius).HorizontalAlignment(HorizontalAlignment.Stretch)));
        Put(d, AppControlStyles.ComboBox, new Style<ComboBox>().Setters(s => s
            .FontFamily(regular).FontSize(14).MinHeight(minHeight).CornerRadius(controlRadius).HorizontalAlignment(HorizontalAlignment.Stretch)));

        Put(d, AppControlStyles.TableRow, new Style<ListViewItem>().Setters(s => s
            .MinHeight(AppMetrics.Doubles[AppMetrics.RowMinHeight])
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Padding(new Thickness(12, 8, 12, 8))));

        Put(d, AppTypography.MetricText, new Style<TextBlock>().Setters(s => s
            .FontFamily(fonts[AppTypography.SemiBoldFont])
            .FontSize(24)
            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
            .LineHeight(32)
            .TextWrapping(TextWrapping.Wrap)));
    }

    // Markup's Style<T> is a builder; the resource dictionary must hold the real Style.
    private static void Put<T>(ResourceDictionary d, string key, Uno.Extensions.Markup.Style<T> style) where T : DependencyObject => d[key] = (Style)style;

    // A PathIconSource whose Foreground is a ThemeResource reference can only be created through the
    // XAML parser (code cannot set a ThemeResource on a non-FrameworkElement). The parser is used for the
    // Foreground only; the path Data still goes through the single ToGeometry conversion below.
    private static void AddIcons(ResourceDictionary d)
    {
        foreach (var (key, data) in AppIcons.PathData)
        {
            var icon = (PathIconSource)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                "<PathIconSource xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                $"Foreground=\"{{ThemeResource {AppColors.InfoText}}}\" />");
            icon.Data = ToGeometry(data);
            d[key] = icon;
        }
    }

    /// <summary>Single place that turns SVG path data into a Geometry (Uno/Skia converter; WinAppSDK branch not verified).</summary>
    internal static Geometry ToGeometry(string pathData) =>
#if WINDOWS
        (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);
#else
        (Geometry?)new Uno.Media.GeometryConverter().ConvertFromString(pathData) ?? throw new FormatException("Invalid icon path data.");
#endif
}
