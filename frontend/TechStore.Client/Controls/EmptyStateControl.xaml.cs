using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace TechStore.Client.Controls;

public sealed partial class EmptyStateControl : UserControl
{
    public EmptyStateControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Refresh();
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyStateControl), new PropertyMetadata("", OnPropertyChanged));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(EmptyStateControl), new PropertyMetadata("", OnPropertyChanged));
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText), typeof(string), typeof(EmptyStateControl), new PropertyMetadata("", OnPropertyChanged));
    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(
        nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateControl), new PropertyMetadata(null, OnPropertyChanged));
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(IconSource), typeof(EmptyStateControl), new PropertyMetadata(null, OnPropertyChanged));
    public IconSource? IconSource { get => (IconSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((EmptyStateControl)sender).Refresh();

    private void Refresh()
    {
        if (ActionButton is null) return;
        ActionButton.Visibility = !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null ? Visibility.Visible : Visibility.Collapsed;
        DescriptionLabel.Visibility = string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;
        EmptyIcon.Visibility = IconSource is null ? Visibility.Collapsed : Visibility.Visible;
    }

}
