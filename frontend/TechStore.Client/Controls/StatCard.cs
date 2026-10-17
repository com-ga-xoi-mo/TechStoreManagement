using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Controls;

/// <summary>
/// Metric card (title, value, trend badge, detail). Public API is unchanged from the XAML version.
/// Display rules come from <see cref="StatCardPresentation"/>; the tree binds to the control's own properties.
/// </summary>
public sealed class StatCard : UserControl
{
    private readonly TextBlock _title;
    private readonly TextBlock _value;
    private readonly TextBlock _unit;
    private readonly TextBlock _error;
    private readonly TextBlock _detail;

    public StatCard()
    {
        this.IsTabStop(false);

        _title = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .Text(x => x.Binding(() => Self.Title));

        _unit = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .VerticalAlignment(VerticalAlignment.Bottom)
            .Text(x => x.Binding(() => Self.UnitText).Convert((string unit) => StatCardPresentation.GetDisplayUnit(unit)));

        _detail = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .Text(x => x.Binding(() => Self.DetailText))
            .Visibility(x => x.Binding(() => Self.DetailText).Convert((string text) => ShowIf(StatCardPresentation.HasText(text))));

        _value = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.MetricText))
            .TextWrapping(TextWrapping.NoWrap)
            .Text(x => x.Binding(() => Self.ValueText).Convert((string value) => StatCardPresentation.GetDisplayValue(value)));

        var content = new StackPanel()
            .Spacing(8)
            .Visibility(x => x.Binding(() => Self.DisplayState).Convert((StatCardDisplayState s) => ShowIf(s == StatCardDisplayState.Content)))
            .Children(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(
                        _value,
                        _unit),
                new StatusBadge()
                    .Label(x => x.Binding(() => Self.TrendText))
                    .Tone(x => x.Binding(() => Self.Tone)),
                _detail);

        var loading = new ProgressRing()
            .Width(28)
            .Height(28)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .IsActive(x => x.Binding(() => Self.IsLoading))
            .Visibility(x => x.Binding(() => Self.DisplayState).Convert((StatCardDisplayState s) => ShowIf(s == StatCardDisplayState.Loading)));
        AutomationProperties.SetName(loading, "Đang tải số liệu");

        _error = new TextBlock()
            .Style(AppResources.GetStyle(AppControlStyles.FieldError))
            .Text(x => x.Binding(() => Self.ErrorText))
            .Visibility(x => x.Binding(() => Self.DisplayState).Convert((StatCardDisplayState s) => ShowIf(s == StatCardDisplayState.Error)));
        AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Polite);

        var icon = new Border()
            .Background(AppColors.Brushes.InfoFill)
            .CornerRadius(new CornerRadius(6))
            .Padding(new Thickness(6))
            .Visibility(x => x.Binding(() => Self.IconSource).Convert((IconSource? source) => ShowIf(source is not null)))
            .Child(new IconSourceElement()
                .Width(20)
                .Height(20)
                .IconSource(x => x.Binding(() => Self.IconSource)));

        Content = new Border()
            .DataContext(this)
            .Style(AppResources.GetStyle(AppControlStyles.Panel))
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Child(new StackPanel()
                .Spacing(8)
                .Children(
                    new Grid()
                        .ColumnSpacing(8)
                        .ColumnDefinitions(
                            new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                            new ColumnDefinition().Width(GridLength.Auto))
                        .Children(_title.Grid(column: 0), icon.Grid(column: 1)),
                    content,
                    loading,
                    _error));

        ApplyThemeColors();
        // Style setters win over a theme reference applied before the first load, so apply again once loaded.
        Loaded += (_, _) => ApplyThemeColors();
    }

    // Binding source for the inner tree (its DataContext is this control; the control's own DataContext stays the consumer's).
    private StatCard Self => this;

    private static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyThemeColors()
    {
        _title.Foreground(AppColors.Brushes.SecondaryText);
        _unit.Foreground(AppColors.Brushes.SecondaryText);
        _detail.Foreground(AppColors.Brushes.SecondaryText);
        _value.Foreground(AppColors.Brushes.Text);
        _error.Foreground(AppColors.Brushes.DangerText);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    public static readonly DependencyProperty UnitTextProperty = DependencyProperty.Register(
        nameof(UnitText), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public string UnitText { get => (string)GetValue(UnitTextProperty); set => SetValue(UnitTextProperty, value); }

    public static readonly DependencyProperty DetailTextProperty = DependencyProperty.Register(
        nameof(DetailText), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public string DetailText { get => (string)GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }

    public static readonly DependencyProperty TrendTextProperty = DependencyProperty.Register(
        nameof(TrendText), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public string TrendText { get => (string)GetValue(TrendTextProperty); set => SetValue(TrendTextProperty, value); }

    public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
        nameof(ErrorText), typeof(string), typeof(StatCard),
        new PropertyMetadata("", (d, _) => ((StatCard)d).UpdateDisplayState()));
    public string ErrorText { get => (string)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(StatCard),
        new PropertyMetadata(false, (d, _) => ((StatCard)d).UpdateDisplayState()));
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(IconSource), typeof(StatCard), new PropertyMetadata(null));
    public IconSource? IconSource { get => (IconSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(StatCard), new PropertyMetadata(StatusTone.Neutral));
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    // Derived presentation state (loading > error > content). Read-only for consumers; public so the
    // binding engine can resolve it on every target (non-public members are not bindable on WinAppSDK).
    public static readonly DependencyProperty DisplayStateProperty = DependencyProperty.Register(
        nameof(DisplayState), typeof(StatCardDisplayState), typeof(StatCard), new PropertyMetadata(StatCardDisplayState.Content));
    public StatCardDisplayState DisplayState
    {
        get => (StatCardDisplayState)GetValue(DisplayStateProperty);
        private set => SetValue(DisplayStateProperty, value);
    }

    private void UpdateDisplayState() => DisplayState = StatCardPresentation.GetState(IsLoading, ErrorText);
}
