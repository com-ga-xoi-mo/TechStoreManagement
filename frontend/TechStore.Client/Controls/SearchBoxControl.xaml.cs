using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace TechStore.Client.Controls;

public sealed partial class SearchBoxControl : UserControl
{
    public SearchBoxControl()
    {
        InitializeComponent();
        Refresh();
    }
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SearchBoxControl), new PropertyMetadata("Tìm kiếm", OnPropertyChanged));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(SearchBoxControl), new PropertyMetadata("Tên sản phẩm, SKU hoặc IMEI", OnPropertyChanged));
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchBoxControl), new PropertyMetadata("", OnPropertyChanged));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(
        nameof(SearchCommand), typeof(ICommand), typeof(SearchBoxControl), new PropertyMetadata(null, OnPropertyChanged));
    public ICommand? SearchCommand { get => (ICommand?)GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }
    public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.Register(
        nameof(ClearCommand), typeof(ICommand), typeof(SearchBoxControl), new PropertyMetadata(null, OnPropertyChanged));
    public ICommand? ClearCommand { get => (ICommand?)GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
    private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((SearchBoxControl)sender).Refresh();

    private void Refresh()
    {
        if (QueryBox is null) return;
        if (QueryBox.Text != Text) QueryBox.Text = Text ?? "";
        AutomationProperties.SetName(QueryBox, Label);
        AutomationProperties.SetLabeledBy(QueryBox, SearchLabel);
    }
    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Text = sender.Text;
    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Text = sender.Text;
        if (IsEnabled && SearchCommand?.CanExecute(Text) == true) SearchCommand.Execute(Text);
    }
    private void OnClear(object sender, RoutedEventArgs args)
    {
        if (!IsEnabled || ClearCommand?.CanExecute(null) == false) return;
        Text = "";
        ClearCommand?.Execute(null);
        QueryBox.Focus(FocusState.Programmatic);
    }

}
