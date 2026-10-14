using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace TechStore.Client.Controls;

public sealed partial class StatusBadge : UserControl
{
    public StatusBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Refresh();
    }
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(StatusBadge), new PropertyMetadata("", OnPropertyChanged));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(StatusBadge), new PropertyMetadata(StatusTone.Neutral, OnPropertyChanged));
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StatusBadge)sender).Refresh();

    private void Refresh()
    {
        if (BadgeBorder is null) return;
        Visibility = string.IsNullOrWhiteSpace(Label) ? Visibility.Collapsed : Visibility.Visible;
        VisualStateManager.GoToState(this, Enum.IsDefined(Tone) ? Tone.ToString() : nameof(StatusTone.Neutral), false);
    }

}
