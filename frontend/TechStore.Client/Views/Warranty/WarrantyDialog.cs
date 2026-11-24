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
        var overlay = new Grid
        {
            Width = root.Size.Width,
            Height = root.Size.Height
        }.Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        overlay.Children.Add(new Border()
            .Background(AppColors.Brushes.Surface).CornerRadius(AppMetrics.Values.CardRadius)
            .Padding(20).MaxWidth(640).MaxHeight(Math.Max(280, root.Size.Height - 40))
            .HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center)
            .Child(new Grid().RowDefinitions("Auto, *, Auto").RowSpacing(12).Children(
                new TextBlock().Text(DialogTitle).Foreground(AppColors.Brushes.Text).Style(AppResources.GetStyle(AppTypography.SectionText)),
                new ScrollViewer().Grid(row: 1).Content(this),
                new Button().Grid(row: 2).Content("Đóng").Command(new RelayCommand(Hide)))));
        popup.Child = overlay;
        popup.Opened += async (_, _) => await ActivateAsync();
        popup.Closed += (_, _) =>
        {
            Deactivate();
            closed.TrySetResult();
        };
        try
        {
            popup.IsOpen = true;
            await closed.Task;
        }
        finally
        {
            _popup = null;
            popup.Child = null;
        }
    }

    public void Hide()
    {
        if (_popup != null) _popup.IsOpen = false;
    }
}
