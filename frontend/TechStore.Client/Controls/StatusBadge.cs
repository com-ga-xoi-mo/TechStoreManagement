using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Controls;

/// <summary>
/// Small status pill. Public API (Label, Tone) is unchanged from the XAML version.
/// The tree is built with C# Markup; colors come from theme resources chosen by <see cref="StatusBadgePresentation"/>.
/// </summary>
public sealed class StatusBadge : UserControl
{
    private readonly Border _badge;
    private readonly TextBlock _text;

    public StatusBadge()
    {
        this.IsTabStop(false);

        _text = new TextBlock()
            .Text(x => x.Binding(() => Self.Label));

        _badge = new Border()
            .DataContext(this)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .BorderThickness(new Thickness(1))
            .CornerRadius(AppMetrics.Values.BadgeRadius)
            .Padding(AppMetrics.Values.BadgePadding)
            .Child(_text);

        Content = _badge;
        ApplyTone();
        _text.Style(AppResources.GetStyle(AppTypography.CaptionText));
        ApplyVisibility();
        Loaded += (_, _) => ApplyTone();
    }

    // Binding source for the inner tree (its DataContext is this control, the control's own DataContext stays the consumer's).
    private StatusBadge Self => this;

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(StatusBadge),
        new PropertyMetadata("", (d, _) => ((StatusBadge)d).ApplyVisibility()));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(StatusBadge),
        new PropertyMetadata(StatusTone.Neutral, (d, _) => ((StatusBadge)d).ApplyTone()));

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    // A badge without a label takes no space (same as the XAML version, which collapsed the control).
    private void ApplyVisibility() =>
        Visibility = StatusBadgePresentation.IsVisible(Label) ? Visibility.Visible : Visibility.Collapsed;

    // Re-points the three theme-resource references at the keys of the current tone.
    private void ApplyTone()
    {
        var keys = StatusBadgePresentation.GetKeys(Tone);
        _badge.Background(ThemeResource.Get<Brush>(keys.Fill))
            .BorderBrush(ThemeResource.Get<Brush>(keys.Stroke));
        _text.Foreground(ThemeResource.Get<Brush>(keys.Text));
    }
}
