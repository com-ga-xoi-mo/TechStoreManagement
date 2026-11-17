using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Models.Customers;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Customers;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Customers;

/// <summary>
/// Customer of a sale: search by name or phone, pick, clear back to a walk-in guest, or create one.
/// Host pages (order creation, POS) place it and read <see cref="CustomerPickerViewModel.SelectedCustomerId"/>.
/// </summary>
public sealed class CustomerPickerView : UserControl
{
    private bool _isShowingForm;

    public CustomerPickerViewModel ViewModel { get; }

    /// <summary>Opens the create form; the popup lifetime belongs to the View, the rules to the ViewModel.</summary>
    public IAsyncRelayCommand ShowCreateFormCommand { get; }

    public CustomerPickerView(CustomerPickerViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        ShowCreateFormCommand = new AsyncRelayCommand(ShowCreateFormAsync);

        var clear = new Button()
            .Style(AppControlStyles.Styles.Button)
            .Content("Bỏ chọn")
            .VerticalAlignment(VerticalAlignment.Center)
            .Command(x => x.Binding(() => ViewModel.ClearCommand))
            .Visibility(x => x.Binding(() => ViewModel.HasSelection).Convert((bool v) => ShowIf(v)));
        AutomationProperties.SetName(clear, "Bỏ chọn khách, bán cho khách lẻ");

        var create = new Button()
            .Style(AppControlStyles.Styles.PrimaryButton)
            .Content("＋  Khách mới")
            .HorizontalAlignment(HorizontalAlignment.Right)
            .Command(ShowCreateFormCommand);
        AutomationProperties.SetName(create, "Tạo khách hàng mới cho đơn này");

        Content = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new Grid()
                            .ColumnSpacing(12)
                            .ColumnDefinitions("*, Auto")
                            .Children(
                                new TextBlock()
                                    .Style(AppTypography.Styles.SectionText)
                                    .Text("Khách hàng")
                                    .VerticalAlignment(VerticalAlignment.Center),
                                create.Grid(column: 1)),
                        new SearchBoxControl()
                            .Label("Tìm khách")
                            .PlaceholderText("Tên hoặc số điện thoại")
                            .Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay))
                            .SearchCommand(x => x.Binding(() => ViewModel.SearchCommand))
                            .ClearCommand(x => x.Binding(() => ViewModel.ClearSearchCommand)),
                        new ProgressBar()
                            .IsIndeterminate(true)
                            .Visibility(x => x.Binding(() => ViewModel.IsSearching).Convert((bool v) => ShowIf(v))),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.Message))
                            .Visibility(x => x.Binding(() => ViewModel.HasMessage).Convert((bool v) => ShowIf(v))),
                        new ItemsControl()
                            .ItemsSource(x => x.Binding(() => ViewModel.Results))
                            .ItemTemplate<CustomerSummary>(BuildResult),
                        new Grid()
                            .Visibility(x => x.Binding(() => ViewModel.HasSelection).Convert((bool v) => ShowIf(v)))
                            .ColumnSpacing(12)
                            .ColumnDefinitions(
                                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                                new ColumnDefinition().Width(GridLength.Auto))
                            .Children(
                                new StackPanel()
                                    .Spacing(2)
                                    .Children(
                                        new TextBlock()
                                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                                            .Foreground(AppColors.Brushes.Text)
                                            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
                                            .TextTrimming(TextTrimming.CharacterEllipsis)
                                            .Text(x => x.Binding(() => ViewModel.SelectionLabel)),
                                        new TextBlock()
                                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                            .Foreground(AppColors.Brushes.SecondaryText)
                                            .Text(x => x.Binding(() => ViewModel.SelectionTierLabel)))
                                    .Grid(column: 0),
                                clear.Grid(column: 1))));
    }

    private UIElement BuildResult(CustomerSummary customer)
    {
        var select = new Button()
            .Style(AppControlStyles.Styles.Button)
            .Content("Chọn")
            .VerticalAlignment(VerticalAlignment.Center)
            // Inside the template the DataContext is the row: assign the command directly.
            .Command(ViewModel.SelectCommand)
            .CommandParameter(x => x.Binding(() => customer));
        AutomationProperties.SetName(select, "Chọn khách hàng này cho đơn");

        return new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(0, 8, 0, 8))
            .Child(
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new StackPanel()
                            .Spacing(2)
                            .Children(
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                                    .Foreground(AppColors.Brushes.Text)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(x => x.Binding(() => customer.FullName)),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .Text(x => x.Binding(() => customer.Phone)),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .Text(x => x.Binding(() => customer.Tier)
                                        .Convert((string tier) => CustomerTierPresentation.FromTier(tier).BadgeLabel)))
                            .Grid(column: 0),
                        select.Grid(column: 1)));
    }

    private async Task ShowCreateFormAsync()
    {
        if (_isShowingForm || XamlRoot is null) return;
        _isShowingForm = true;
        ViewModel.StartCreateCommand.Execute(null);
        var popup = new Popup { XamlRoot = XamlRoot, IsLightDismissEnabled = false };
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCommand = new RelayCommand(() =>
        {
            ViewModel.CancelCreateCommand.Execute(null);
            popup.IsOpen = false;
        });
        var saveCommand = new AsyncRelayCommand(async () =>
        {
            await ViewModel.SaveNewCommand.ExecuteAsync(null);
            // Saved, or the phone already belongs to a customer that is now listed: either way the form is done.
            if (!ViewModel.IsCreating) popup.IsOpen = false;
        });
        var form = new CustomerFormDialogView(ViewModel.Form, saveCommand, closeCommand)
        {
            MaxHeight = Math.Max(520, XamlRoot.Size.Height - 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var overlay = new Grid
        {
            Width = XamlRoot.Size.Width,
            Height = XamlRoot.Size.Height
        }.Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        overlay.Children.Add(form);
        popup.Child = overlay;
        popup.Opened += (_, _) => form.InitialFocusTarget.Focus(FocusState.Programmatic);
        popup.Closed += (_, _) => closed.TrySetResult(true);
        try
        {
            popup.IsOpen = true;
            await closed.Task;
        }
        finally
        {
            _isShowingForm = false;
        }
    }

    private static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
