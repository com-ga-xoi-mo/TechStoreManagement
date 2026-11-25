using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Warranty;

public abstract class WarrantyDialog : UserControl
{
    private const double PreferredWidth = 760;
    private Popup? _popup;

    protected abstract string DialogTitle { get; }
    protected abstract Task ActivateAsync();
    protected abstract void Deactivate();

    public async Task ShowAsync(XamlRoot root, ElementTheme theme)
    {
        RequestedTheme = theme;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var popup = new Popup { XamlRoot = root, IsLightDismissEnabled = false };
        _popup = popup;
        var overlay = new Grid().Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        var panel = new Border()
            .Background(AppColors.Brushes.Surface).CornerRadius(AppMetrics.Values.CardRadius)
            .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(1)
            .HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center)
            .Child(new Grid().RowDefinitions("Auto, *, Auto").Children(
                new Border().Padding(new Thickness(20, 14, 20, 14))
                    .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(new Thickness(0, 0, 0, 1))
                    .Child(new TextBlock().Text(DialogTitle).Foreground(AppColors.Brushes.Text)
                        .Style(AppResources.GetStyle(AppTypography.SectionText)).TextWrapping(TextWrapping.Wrap)),
                new ScrollViewer().Grid(row: 1)
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                    .Content(new Grid().Padding(20).Children(this)),
                new Border().Grid(row: 2).Padding(new Thickness(20, 12, 20, 12))
                    .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(new Thickness(0, 1, 0, 0))
                    .Child(new Button().Content("Đóng").HorizontalAlignment(HorizontalAlignment.Right)
                        .Command(new RelayCommand(Hide)))));
        overlay.Children.Add(panel);

        void FitToRoot(XamlRoot sender, XamlRootChangedEventArgs? args)
        {
            overlay.Width = sender.Size.Width;
            overlay.Height = sender.Size.Height;
            panel.Width = Math.Clamp(overlay.Width - 24, 0, PreferredWidth);
            panel.MaxHeight = Math.Max(0, overlay.Height - 40);
        }

        FitToRoot(root, null);
        popup.Child = overlay;
        popup.Opened += async (_, _) => await ActivateAsync();
        popup.Closed += (_, _) =>
        {
            Deactivate();
            closed.TrySetResult();
        };
        root.Changed += FitToRoot;
        try
        {
            popup.IsOpen = true;
            await closed.Task;
        }
        finally
        {
            root.Changed -= FitToRoot;
            _popup = null;
            popup.Child = null;
        }
    }

    public void Hide()
    {
        if (_popup != null) _popup.IsOpen = false;
    }
}
