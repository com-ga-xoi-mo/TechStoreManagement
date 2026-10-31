using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Controls;

/// <summary>
/// Labeled search box with a clear button. Public API is unchanged from the XAML version.
/// The text box binds two-way to <see cref="Text"/>. Two intentional event handlers remain
/// (see design D2): QuerySubmitted (AutoSuggestBox has no submit command) and returning focus on clear.
/// </summary>
public sealed class SearchBoxControl : UserControl
{
    private readonly AutoSuggestBox _query;
    private readonly TextBlock _label;
    private readonly Button _clear;

    public SearchBoxControl()
    {
        this.IsTabStop(false);

        _label = new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .Text(x => x.Binding(() => Self.Label));

        _query = new AutoSuggestBox()
            .Style(AppResources.GetStyle(AppControlStyles.AutoSuggestBox))
            .QueryIcon(new SymbolIcon(Symbol.Find))
            .PlaceholderText(x => x.Binding(() => Self.PlaceholderText))
            .Text(x => x.Binding(() => Self.Text).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        AutomationProperties.SetAutomationId(_query, "GallerySearch");
        AutomationProperties.SetLabeledBy(_query, _label);
        AutomationProperties.SetName(_query, Label);
        // Exception 1 (D2): submit has no command on AutoSuggestBox.
        _query.QuerySubmitted += OnQuerySubmitted;

        _clear = new Button()
            .Style(AppResources.GetStyle(AppControlStyles.Button))
            .Content("Xóa")
            .Command(x => x.Binding(() => Self.ClearCommand));
        AutomationProperties.SetName(_clear, "Xóa nội dung tìm kiếm");
        // Exception 2 (D2): focus cannot be expressed as a binding. Runs before ClearCommand executes.
        _clear.Click += OnClearClick;

        Content = new StackPanel()
            .DataContext(this)
            .Spacing(6)
            .Children(
                _label,
                new Grid()
                    .ColumnSpacing(8)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(_query.Grid(column: 0), _clear.Grid(column: 1)));

        ApplyThemeColors();
        // Style setters win over a theme reference applied before the first load, so apply again once loaded.
        Loaded += (_, _) => ApplyThemeColors();
    }

    // Binding source for the inner tree (its DataContext is this control; the control's own DataContext stays the consumer's).
    private SearchBoxControl Self => this;

    internal SearchBoxControl WithInputHeight(double height)
    {
        _query.Height = height;
        _clear.Height = height;
        return this;
    }

    private void ApplyThemeColors() => _label.Foreground(AppColors.Brushes.Text);

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Text = sender.Text;
        if (IsEnabled && SearchCommand?.CanExecute(Text) == true) SearchCommand.Execute(Text);
    }

    private void OnClearClick(object sender, RoutedEventArgs args)
    {
        Text = "";
        _query.Focus(FocusState.Programmatic);
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SearchBoxControl),
        new PropertyMetadata("Tìm kiếm", (d, e) => AutomationProperties.SetName(((SearchBoxControl)d)._query, (string)e.NewValue)));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(SearchBoxControl), new PropertyMetadata("Tên sản phẩm, SKU hoặc IMEI"));
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchBoxControl), new PropertyMetadata(""));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(
        nameof(SearchCommand), typeof(ICommand), typeof(SearchBoxControl), new PropertyMetadata(null));
    public ICommand? SearchCommand { get => (ICommand?)GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }

    public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.Register(
        nameof(ClearCommand), typeof(ICommand), typeof(SearchBoxControl), new PropertyMetadata(null));
    public ICommand? ClearCommand { get => (ICommand?)GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
}
