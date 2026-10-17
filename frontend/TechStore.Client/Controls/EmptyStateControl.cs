using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Controls;

/// <summary>
/// Empty/placeholder state with an optional action. Public API is unchanged from the XAML version.
/// The action button is shown only when both ActionText and ActionCommand are present.
/// </summary>
public sealed class EmptyStateControl : UserControl
{
    private readonly Border _panel;
    private readonly TextBlock _description;

    public EmptyStateControl()
    {
        this.IsTabStop(false);

        _description = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .TextAlignment(TextAlignment.Center)
            .Text(x => x.Binding(() => Self.Description))
            .Visibility(x => x.Binding(() => Self.Description).Convert((string text) => ShowIf(EmptyStatePresentation.ShowDescription(text))));

        _panel = new Border()
            .DataContext(this)
            .Style(AppResources.GetStyle(AppControlStyles.Panel))
            .Child(new StackPanel()
                .Spacing(12)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .MaxWidth(440)
                .Children(
                    new IconSourceElement()
                        .Width(32)
                        .Height(32)
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .IconSource(x => x.Binding(() => Self.IconSource))
                        .Visibility(x => x.Binding(() => Self.IconSource).Convert((IconSource? source) => ShowIf(source is not null))),
                    new TextBlock()
                        .Style(AppResources.GetStyle(AppTypography.SectionText))
                        .TextAlignment(TextAlignment.Center)
                        .Text(x => x.Binding(() => Self.Title)),
                    _description,
                    new Button()
                        .Style(AppResources.GetStyle(AppControlStyles.Button))
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .Content(x => x.Binding(() => Self.ActionText))
                        .Command(x => x.Binding(() => Self.ActionCommand))
                        .Visibility(x => x.Binding(() => Self.HasAction).Convert((bool has) => ShowIf(has)))));

        Content = _panel;
        ApplyThemeColors();
        // Style setters win over a theme reference applied before the first load, so apply again once loaded.
        Loaded += (_, _) => ApplyThemeColors();
    }

    // Binding source for the inner tree (its DataContext is this control; the control's own DataContext stays the consumer's).
    private EmptyStateControl Self => this;

    private static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyThemeColors()
    {
        _panel.Background(AppColors.Brushes.Canvas);
        _description.Foreground(AppColors.Brushes.SecondaryText);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(""));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(""));
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText), typeof(string), typeof(EmptyStateControl),
        new PropertyMetadata("", (d, _) => ((EmptyStateControl)d).UpdateHasAction()));
    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(
        nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateControl),
        new PropertyMetadata(null, (d, _) => ((EmptyStateControl)d).UpdateHasAction()));
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(IconSource), typeof(EmptyStateControl), new PropertyMetadata(null));
    public IconSource? IconSource { get => (IconSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }

    // Derived presentation flag (action needs both a label and a command). Read-only for consumers; public so
    // the binding engine can resolve it on every target (non-public members are not bindable on WinAppSDK).
    public static readonly DependencyProperty HasActionProperty = DependencyProperty.Register(
        nameof(HasAction), typeof(bool), typeof(EmptyStateControl), new PropertyMetadata(false));
    public bool HasAction
    {
        get => (bool)GetValue(HasActionProperty);
        private set => SetValue(HasActionProperty, value);
    }

    private void UpdateHasAction() => HasAction = EmptyStatePresentation.ShowAction(ActionText, ActionCommand is not null);
}
