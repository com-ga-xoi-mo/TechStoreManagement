using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace TechStore.Client.Controls;

public sealed partial class StatCard : UserControl
{
    public StatCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Refresh();
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public static readonly DependencyProperty UnitTextProperty = DependencyProperty.Register(
        nameof(UnitText), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string UnitText { get => (string)GetValue(UnitTextProperty); set => SetValue(UnitTextProperty, value); }
    public static readonly DependencyProperty DetailTextProperty = DependencyProperty.Register(
        nameof(DetailText), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string DetailText { get => (string)GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }
    public static readonly DependencyProperty TrendTextProperty = DependencyProperty.Register(
        nameof(TrendText), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string TrendText { get => (string)GetValue(TrendTextProperty); set => SetValue(TrendTextProperty, value); }
    public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
        nameof(ErrorText), typeof(string), typeof(StatCard), new PropertyMetadata("", OnPropertyChanged));
    public string ErrorText { get => (string)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(StatCard), new PropertyMetadata(false, OnPropertyChanged));
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(IconSource), typeof(StatCard), new PropertyMetadata(null, OnPropertyChanged));
    public IconSource? IconSource { get => (IconSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(StatCard), new PropertyMetadata(StatusTone.Neutral, OnPropertyChanged));
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StatCard)sender).Refresh();

    private void Refresh()
    {
        if (ContentPanel is null) return;
        ContentPanel.Visibility = !IsLoading && string.IsNullOrWhiteSpace(ErrorText) ? Visibility.Visible : Visibility.Collapsed;
        LoadingRing.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingRing.IsActive = IsLoading;
        ErrorLabel.Visibility = !IsLoading && !string.IsNullOrWhiteSpace(ErrorText) ? Visibility.Visible : Visibility.Collapsed;
        ValueRun.Text = string.IsNullOrWhiteSpace(ValueText) ? "—" : ValueText;
        UnitRun.Text = string.IsNullOrWhiteSpace(UnitText) ? "" : " " + UnitText;
        DetailLabel.Visibility = Present(DetailText);
        TrendLabel.Visibility = Present(TrendText);
        MetricIcon.Visibility = IconSource is null ? Visibility.Collapsed : Visibility.Visible;

    }
    private static Visibility Present(string? value) => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

}
