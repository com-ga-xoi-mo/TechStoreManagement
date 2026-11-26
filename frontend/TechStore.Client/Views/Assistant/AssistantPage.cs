using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Assistant;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Assistant;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Assistant;

public sealed class AssistantPage : Page, IRoutePage
{
    private readonly ScrollViewer _scroll;
    private readonly Button _jumpToLatest;
    private bool _atBottom = true;

    public AssistantViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public AssistantPage(AssistantViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        var messages = new ItemsControl().ItemsSource(x => x.Binding(() => ViewModel.Messages));
        messages.ItemTemplate<AssistantTurn>(item => new StackPanel().Spacing(4).Padding(8).Children(
            new TextBlock().Foreground(AppColors.Brushes.SecondaryText)
                .Text(x => x.Binding(() => item.IsUser).Convert((bool user) => user ? "Bạn" : "Trợ lý AI")),
            new TextBlock().Foreground(AppColors.Brushes.Text).TextWrapping(TextWrapping.Wrap)
                .IsTextSelectionEnabled(true).Text(x => x.Binding(() => item.Text))));
        _scroll = new ScrollViewer()
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Content(messages);
        _jumpToLatest = new Button()
            .Content("↓")
            .FontSize(18)
            .Width(40).Height(40).Padding(0)
            .CornerRadius(20)
            .HorizontalAlignment(HorizontalAlignment.Right)
            .VerticalAlignment(VerticalAlignment.Bottom)
            .Margin(0, 0, 16, 12)
            .Visibility(Visibility.Collapsed)
            .Command(new RelayCommand(() => ScrollToLatest(instant: false)));
        AutomationProperties.SetName(_jumpToLatest, "Xuống tin nhắn mới nhất");
        ToolTipService.SetToolTip(_jumpToLatest, "Xuống tin nhắn mới nhất");

        this.Background(AppColors.Brushes.Canvas).Content(
            new Grid().Padding(24).RowSpacing(12).RowDefinitions("Auto, *, Auto")
                .Children(
                    new TextBlock().Style(AppResources.GetStyle(AppTypography.HeadingText))
                        .Foreground(AppColors.Brushes.Text).Text("Trợ lý AI"),
                    new Grid().Grid(row: 1).Children(_scroll, _jumpToLatest),
                    new StackPanel().Spacing(8).Grid(row: 2).Children(
                        new TextBlock().Foreground(AppColors.Brushes.SecondaryText)
                            .Text("Đang trả lời...")
                            .Visibility(x => x.Binding(() => ViewModel.IsSending).Convert((bool value) => ShowIf(value))),
                        new TextBlock().Foreground(AppColors.Brushes.SecondaryText).Text(x => x.Binding(() => ViewModel.StatusMessage)),
                        new StackPanel().Spacing(8).Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool value) => ShowIf(value)))
                            .Children(new TextBlock().Foreground(AppColors.Brushes.Text).TextWrapping(TextWrapping.Wrap)
                                    .Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                                new Button().Content("Thử lại").Command(x => x.Binding(() => ViewModel.RetryCommand))),
                        new TextBox().Header("Câu hỏi") .AcceptsReturn(true).TextWrapping(TextWrapping.Wrap).MaxLength(2000).MinHeight(80)
                            .Text(x => x.Binding(() => ViewModel.Draft).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                            new Button().Style(AppControlStyles.Styles.PrimaryButton).Content("Gửi").Command(x => x.Binding(() => ViewModel.SendCommand)),
                            new Button().Content("Hủy").Command(x => x.Binding(() => ViewModel.CancelCommand))
                                .Visibility(x => x.Binding(() => ViewModel.IsSending).Convert((bool value) => ShowIf(value))),
                            new Button().Content("Xóa hội thoại").Command(x => x.Binding(() => ViewModel.ClearCommand))))));

        // Scroll position cannot be bound: documented code-behind exception (m4-gemini-assistant design D9).
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
        _scroll.ViewChanged += OnViewChanged;
        ScrollToLatest(instant: true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Messages.CollectionChanged -= OnMessagesChanged;
        _scroll.ViewChanged -= OnViewChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Follow new messages only when the user is already at the latest one; otherwise offer the "↓" button.
        if (_atBottom || e.Action == NotifyCollectionChangedAction.Reset) ScrollToLatest(instant: false);
        else DispatcherQueue.TryEnqueue(UpdateJumpButton);
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (e.IsIntermediate) return;
        _atBottom = ChatScroll.IsAtBottom(_scroll.VerticalOffset, _scroll.ScrollableHeight);
        UpdateJumpButton();
    }

    private void ScrollToLatest(bool instant)
    {
        _atBottom = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _scroll.UpdateLayout();
            _scroll.ChangeView(null, _scroll.ScrollableHeight, null, instant);
            UpdateJumpButton();
        });
    }

    private void UpdateJumpButton() =>
        _jumpToLatest.Visibility = !_atBottom && ChatScroll.ShowJumpToLatest(_scroll.VerticalOffset, _scroll.ScrollableHeight)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
