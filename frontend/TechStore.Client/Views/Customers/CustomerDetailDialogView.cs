using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Models.Customers;
using TechStore.Client.ViewModels.Customers;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Customers;

public sealed class CustomerDetailDialogView : UserControl
{
    private const string Surface = "TechStoreSurfaceBrush";
    private const string Subtle = "TechStoreSubtleBrush";
    private const string Text = "TechStoreTextBrush";
    private const string SecondaryText = "TechStoreSecondaryTextBrush";
    private const string Stroke = "TechStoreStrokeBrush";
    private const string Primary = "TechStorePrimaryBrush";
    private const string InfoFill = "TechStoreInfoFillBrush";
    private const string InfoText = "TechStoreInfoTextBrush";
    private const string WarningFill = "TechStoreWarningFillBrush";
    private const string WarningText = "TechStoreWarningTextBrush";
    private const string SuccessFill = "TechStoreSuccessFillBrush";
    private const string SuccessText = "TechStoreSuccessTextBrush";
    private const string DangerFill = "TechStoreDangerFillBrush";
    private const string DangerText = "TechStoreDangerTextBrush";

    public CustomerDetailViewModel ViewModel { get; }
    public IRelayCommand DeactivateCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public Control InitialFocusTarget { get; }

    public CustomerDetailDialogView(
        CustomerDetailViewModel viewModel,
        IRelayCommand deactivateCommand,
        IRelayCommand closeCommand)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DeactivateCommand = deactivateCommand ?? throw new ArgumentNullException(nameof(deactivateCommand));
        CloseCommand = closeCommand ?? throw new ArgumentNullException(nameof(closeCommand));
        DataContext = ViewModel;

        var closeTopButton = ActionButton("\uE711", "Đóng hồ sơ khách hàng", CloseCommand);
        closeTopButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape });
        InitialFocusTarget = closeTopButton;

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        content.Children.Add(BuildHeader(closeTopButton));

        var scrollingBody = new StackPanel { Spacing = 0 };
        scrollingBody.Children.Add(BuildMetrics());
        scrollingBody.Children.Add(BuildActivityError());
        scrollingBody.Children.Add(BuildHistoryTabs());
        scrollingBody.Children.Add(BuildLoadState());
        scrollingBody.Children.Add(BuildHistoryTables());
        var scrollViewer = new ScrollViewer
        {
            Content = scrollingBody,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scrollViewer, 1);
        content.Children.Add(scrollViewer);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        content.Children.Add(footer);

        Content = new Border
        {
            Width = 760,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = content
        }.Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private UIElement BuildHeader(Button closeTopButton)
    {
        var avatar = BuildTierAvatar();

        var nameLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLine.Children.Add(new TextBlock
        {
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        }.Foreground(ThemeResource.Get<Brush>(Text))
            .Text(x => x.Binding(() => ViewModel.FullName)));
        nameLine.Children.Add(BuildTierBadge());

        var contactLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        contactLine.Children.Add(DetailInfo("\uE717", new TextBlock().Text(x => x.Binding(() => ViewModel.Phone))));
        contactLine.Children.Add(DetailInfo("\uE715", new TextBlock().Text(x => x.Binding(() => ViewModel.EmailLabel))));

        var facts = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        facts.Children.Add(nameLine);
        facts.Children.Add(new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
            .Foreground(ThemeResource.Get<Brush>(WarningText))
            .Text(x => x.Binding(() => ViewModel.LoyaltyPointsLabel)));
        facts.Children.Add(contactLine);
        facts.Children.Add(DetailInfo("\uE707", new TextBlock().Text(x => x.Binding(() => ViewModel.Address)))
            .Visibility(x => x.Binding(() => ViewModel.HasAddress)
                .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed)));

        var profile = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { avatar, facts }
        };

        var deactivate = LabeledButton("\uE74D", "Vô hiệu hóa", DangerText, DangerFill, DeactivateCommand)
            .Visibility(x => x.Binding(() => ViewModel.CanDeactivate)
                .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed));
        AutomationProperties.SetName(deactivate, "Vô hiệu hóa khách hàng");
        var activate = LabeledButton("\uE73E", "Kích hoạt lại", SuccessText, SuccessFill)
            .Visibility(x => x.Binding(() => ViewModel.CanActivate)
                .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed))
            .Command(x => x.Binding(() => ViewModel.ActivateCustomerCommand));
        AutomationProperties.SetName(activate, "Kích hoạt lại khách hàng");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { deactivate, activate, closeTopButton }
        };
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 16
        };
        grid.Children.Add(profile);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        return new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20),
            Child = grid
        }.Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private UIElement BuildTierAvatar()
    {
        var variants = new Grid { Width = 64, Height = 64 };
        foreach (var tier in CustomerTierPresentation.All)
        {
            var avatarText = new TextBlock
            {
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
                .Foreground(ThemeResource.Get<Brush>(tier.TextResourceKey))
                .Text(x => x.Binding(() => ViewModel.FullName).Convert(GetInitials));
            variants.Children.Add(new Border
            {
                Width = 64,
                Height = 64,
                CornerRadius = new CornerRadius(14),
                Child = avatarText
            }
                .Background(ThemeResource.Get<Brush>(tier.FillResourceKey))
                .Visibility(x => x.Binding(() => ViewModel.TierCode)
                    .Convert((string value) => value == tier.Tier ? Visibility.Visible : Visibility.Collapsed)));
        }

        return variants;
    }

    private UIElement BuildTierBadge()
    {
        var variants = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var tier in CustomerTierPresentation.All)
        {
            variants.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = tier.BadgeLabel,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                }.Foreground(ThemeResource.Get<Brush>(tier.TextResourceKey))
            }
                .Background(ThemeResource.Get<Brush>(tier.FillResourceKey))
                .Visibility(x => x.Binding(() => ViewModel.TierCode)
                    .Convert((string value) => value == tier.Tier ? Visibility.Visible : Visibility.Collapsed)));
        }

        return variants;
    }

    private UIElement BuildMetrics()
    {
        var cards = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 12
        };
        cards.Children.Add(MetricCard(
            "Tổng chi tiêu",
            new TextBlock().Text(x => x.Binding(() => ViewModel.TotalSpentLabel)),
            new TextBlock { Text = "Giá trị các đơn hoàn tất" }, "\uE8C7", InfoFill, Primary));
        var orderCard = MetricCard(
            "Tổng đơn hàng",
            new TextBlock().Text(x => x.Binding(() => ViewModel.OrderCountLabel)),
            new TextBlock().Text(x => x.Binding(() => ViewModel.OrderCountCaption)), "\uE7BF", Subtle, Text);
        Grid.SetColumn(orderCard, 1);
        cards.Children.Add(orderCard);
        var signupCard = MetricCard(
            "Ngày đăng ký",
            new TextBlock().Text(x => x.Binding(() => ViewModel.RegistrationDateLabel)),
            new TextBlock().Text(x => x.Binding(() => ViewModel.MembershipCaption)), "\uE787", WarningFill, WarningText);
        Grid.SetColumn(signupCard, 2);
        cards.Children.Add(signupCard);
        return new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 16, 20, 16),
            Child = cards
        }.Background(ThemeResource.Get<Brush>(Subtle))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private static Border MetricCard(string title, TextBlock value, TextBlock caption, string glyph, string fill, string foreground)
    {
        value.FontSize = 19;
        value.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        value.Foreground(ThemeResource.Get<Brush>(foreground));
        caption.FontSize = 11;
        caption.Foreground(ThemeResource.Get<Brush>(SecondaryText));
        var labels = new StackPanel { Spacing = 4 };
        labels.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
            .Foreground(ThemeResource.Get<Brush>(SecondaryText)));
        labels.Children.Add(value);
        labels.Children.Add(caption);
        var icon = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            Child = Icon(glyph, 20, foreground),
            VerticalAlignment = VerticalAlignment.Center
        }.Background(ThemeResource.Get<Brush>(fill));
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        content.Children.Add(labels);
        Grid.SetColumn(icon, 1);
        content.Children.Add(icon);
        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = content
        }.Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private UIElement BuildActivityError() => new Border
    {
        Padding = new Thickness(20, 10, 20, 10),
        Child = new TextBlock { FontSize = 12 }
            .Foreground(ThemeResource.Get<Brush>(DangerText))
            .Text(x => x.Binding(() => ViewModel.ActivityErrorMessage))
    }.Background(ThemeResource.Get<Brush>(DangerFill))
        .Visibility(x => x.Binding(() => ViewModel.HasActivityError)
        .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed));

    private UIElement BuildHistoryTabs()
    {
        var orders = HistoryTab("Lịch sử mua hàng", () => ViewModel.OrderHistoryCountLabel,
            () => ViewModel.IsOrdersTabSelected, () => ViewModel.ShowOrdersTabCommand);
        var devices = HistoryTab("Thiết bị / IMEI", () => ViewModel.DeviceCountLabel,
            () => ViewModel.IsDevicesTabSelected, () => ViewModel.ShowDevicesTabCommand);
        var points = HistoryTab("Lịch sử điểm", () => ViewModel.PointTransactionCountLabel,
            () => ViewModel.IsPointsTabSelected, () => ViewModel.ShowPointsTabCommand);
        return new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 0, 16, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { orders, devices, points }
            }
        }.Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private Button HistoryTab(
        string title,
        System.Linq.Expressions.Expression<Func<string>> count,
        System.Linq.Expressions.Expression<Func<bool>> selected,
        System.Linq.Expressions.Expression<Func<IRelayCommand>> command)
    {
        var badge = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(7, 2, 7, 2),
            Child = new TextBlock { FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
                .Foreground(ThemeResource.Get<Brush>(SecondaryText)).Text(x => x.Binding(MemberName(count)))
        }.Background(ThemeResource.Get<Brush>(Subtle));
        var button = new Button
        {
            Padding = new Thickness(12, 10, 12, 9),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 12 }.Foreground(ThemeResource.Get<Brush>(SecondaryText)),
                    badge
                }
            }
        }.Background(ThemeResource.Get<Brush>(Surface))
            .BorderBrush(ThemeResource.Get<Brush>(Primary));
        button.BorderThickness(x => x.Binding(MemberName(selected))
            .Convert((bool value) => value ? new Thickness(0, 0, 0, 2) : new Thickness(0)));
        button.Command(x => x.Binding(MemberName(command)));
        return button;
    }

    private UIElement BuildLoadState()
    {
        var ring = new ProgressRing { Width = 24, Height = 24 }
            .IsActive(x => x.Binding(() => ViewModel.IsLoading))
            .Visibility(x => x.Binding(() => ViewModel.IsLoading)
                .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed));
        var status = new TextBlock { FontSize = 13 }
            .Foreground(ThemeResource.Get<Brush>(SecondaryText))
            .Text(x => x.Binding(() => ViewModel.ErrorMessage)
                .Convert((string value) => string.IsNullOrWhiteSpace(value) ? "Đang tải hồ sơ khách hàng…" : value));
        var retry = new Button { Content = "Thử tải lại" }
            .Visibility(x => x.Binding(() => ViewModel.IsError)
                .Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed))
            .Command(x => x.Binding(() => ViewModel.ReloadCommand));
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 56,
            Children = { ring, status, retry }
        }.Visibility(x => x.Binding(() => ViewModel.HasProfile)
            .Convert((bool value) => value ? Visibility.Collapsed : Visibility.Visible));
    }

    private UIElement BuildHistoryTables()
    {
        var history = new StackPanel { Spacing = 0, Width = 760, MinWidth = 760 };
        history.Children.Add(BuildOrdersHistory());
        history.Children.Add(BuildDevicesHistory());
        history.Children.Add(BuildPointsHistory());
        return new ScrollViewer
        {
            Content = history,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private StackPanel BuildOrdersHistory()
    {
        var panel = HistoryPanel(() => ViewModel.IsOrdersTabSelected);
        AddHeader(panel, ["MÃ ĐƠN & NGÀY", "SẢN PHẨM CHÍNH", "THÀNH TIỀN", "TRẠNG THÁI"], [1.2, 2, 1.2, 1], [2], [3]);
        panel.Children.Add(new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.Orders))
            .ItemTemplate<CustomerOrder>(BuildOrderRow));
        panel.Children.Add(EmptyMessage("Khách hàng chưa có đơn hàng.", () => ViewModel.HasNoOrders));
        panel.Children.Add(Pager(
            () => ViewModel.OrdersPageLabel,
            () => ViewModel.CanPreviousOrdersPage,
            () => ViewModel.PreviousOrdersPageCommand,
            () => ViewModel.CanNextOrdersPage,
            () => ViewModel.NextOrdersPageCommand));
        return panel;
    }

    private StackPanel BuildDevicesHistory()
    {
        var panel = HistoryPanel(() => ViewModel.IsDevicesTabSelected);
        AddHeader(panel, ["THIẾT BỊ / TÊN MÁY", "SỐ SERIAL / IMEI", "NGÀY MUA & ĐƠN HÀNG", "BẢO HÀNH", "TRẠNG THÁI THIẾT BỊ"], [1.45, 1.3, 1.2, 1.0, 1.25]);
        panel.Children.Add(new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.Devices))
            .ItemTemplate<CustomerPurchasedItem>(BuildDeviceRow));
        panel.Children.Add(EmptyMessage("Chưa có thiết bị đã mua.", () => ViewModel.HasNoDevices));
        panel.Children.Add(Pager(
            () => ViewModel.DevicesPageLabel,
            () => ViewModel.CanPreviousDevicesPage,
            () => ViewModel.PreviousDevicesPageCommand,
            () => ViewModel.CanNextDevicesPage,
            () => ViewModel.NextDevicesPageCommand));
        return panel;
    }

    private StackPanel BuildPointsHistory()
    {
        var panel = HistoryPanel(() => ViewModel.IsPointsTabSelected);
        AddHeader(panel, ["NGÀY GIAO DỊCH", "LÝ DO", "ĐIỂM THAY ĐỔI", "SỐ DƯ SAU"], [1.2, 2, 1, 1], [2, 3]);
        panel.Children.Add(new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.PointTransactions))
            .ItemTemplate<CustomerLoyaltyTransaction>(BuildPointRow));
        panel.Children.Add(EmptyMessage("Chưa có giao dịch điểm.", () => ViewModel.HasNoPointTransactions));
        panel.Children.Add(Pager(
            () => ViewModel.PointsPageLabel,
            () => ViewModel.CanPreviousPointsPage,
            () => ViewModel.PreviousPointsPageCommand,
            () => ViewModel.CanNextPointsPage,
            () => ViewModel.NextPointsPageCommand));
        return panel;
    }

    private static StackPanel HistoryPanel(System.Linq.Expressions.Expression<Func<bool>> visible) =>
        new StackPanel { Spacing = 0 }
            .Visibility(x => x.Binding(MemberName(visible)).Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed));

    private static Border BuildOrderRow(CustomerOrder order)
    {
        var orderInfo = Lines(
            BoundText(() => order.OrderCode, 13, Primary, true),
            BoundText(() => order.CreatedAt, FormatDateTime, 11, SecondaryText));
        var product = Lines(
            BoundText(() => order.DisplayProductName, 13, Text, true),
            BoundText(() => order.PrimaryCategoryName, value => value ?? string.Empty, 11, SecondaryText),
            BoundText(() => order.ItemCount, value => value > 1 ? $"Cùng {value - 1} sản phẩm khác" : string.Empty, 11, SecondaryText));
        var amount = BoundText(() => order.TotalAmount, FormatCurrency, 13, Text, true);
        amount.TextAlignment = TextAlignment.Right;
        var payment = BoundText(() => order.PaymentMethod, FormatPaymentMethod, 11, SecondaryText);
        payment.TextAlignment = TextAlignment.Right;
        var status = ToneBadge(() => order.Status, FormatOrderStatus, () => order.StatusTone);
        return Row([orderInfo, product, Lines(amount, payment), status], [1.2, 2, 1.2, 1]);
    }

    private static Border BuildDeviceRow(CustomerPurchasedItem item)
    {
        var details = Lines(
            BoundText(() => item.ProductName, 13, Text, true),
            BoundText(() => item.VariantName, value => value ?? string.Empty, 11, SecondaryText),
            BoundText(() => item.Sku, value => $"SKU {value}", 10, SecondaryText));
        var icon = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon { FontSize = 16 }
                .Foreground(ThemeResource.Get<Brush>(Primary))
                .Glyph(x => x.Binding(() => item.ProductIconGlyph))
        }.Background(ThemeResource.Get<Brush>(Subtle));
        var product = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 8
        };
        product.Children.Add(icon);
        Grid.SetColumn(details, 1);
        product.Children.Add(details);
        var serial = BoundText(() => item.SerialLabel, 11, SecondaryText)
            .Visibility(x => x.Binding(() => item.SerialLabel)
                .Convert((string value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible));
        var identifiers = Lines(
            BoundText(() => item.ImeiLabel, 12, Text, true),
            serial);
        var purchase = Lines(
            BoundText(() => item.OrderCode, 12, Primary, true),
            BoundText(() => item.CompletedAt, FormatOptionalDate, 11, SecondaryText));
        var warrantyStart = BoundText(() => item.WarrantyStartLabel, 10, SecondaryText)
            .Visibility(x => x.Binding(() => item.WarrantyStartLabel)
                .Convert((string value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible));
        var warranty = Lines(
            ToneText(() => item.WarrantyLabel, () => item.WarrantyTone),
            warrantyStart);
        var status = ToneBadge(() => item.DeviceStatusLabel, value => value, () => item.DeviceStatusTone);
        return Row([product, identifiers, purchase, warranty, status], [1.45, 1.3, 1.2, 1.0, 1.25]);
    }

    private static Border BuildPointRow(CustomerLoyaltyTransaction item)
    {
        var created = BoundText(() => item.CreatedAt, FormatDateTime, 12, Text);
        var reason = Lines(
            BoundText(() => item.Reason, 12, Text, true),
            BoundText(() => item.OrderId, value => value is null ? string.Empty : $"Đơn {value}", 10, SecondaryText));
        var change = BoundText(() => item.PointsChange, value => $"{value:+#;-#;0} điểm", 12, SuccessText, true);
        change.TextAlignment = TextAlignment.Right;
        var balance = BoundText(() => item.BalanceAfter,
            value => $"{value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} điểm", 12, Text);
        balance.TextAlignment = TextAlignment.Right;
        return Row([created, reason, change, balance], [1.2, 2, 1, 1]);
    }

    private static void AddHeader(
        StackPanel target,
        string[] labels,
        double[] weights,
        int[]? rightAligned = null,
        int[]? centerAligned = null)
    {
        var cells = labels.Select(label => (UIElement)new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        }.Foreground(ThemeResource.Get<Brush>(SecondaryText))).ToArray();
        if (rightAligned is not null)
            foreach (var index in rightAligned)
                if (cells[index] is TextBlock text)
                    text.TextAlignment = TextAlignment.Right;
        if (centerAligned is not null)
            foreach (var index in centerAligned)
                if (cells[index] is TextBlock text)
                    text.TextAlignment = TextAlignment.Center;
        target.Children.Add(Row(cells, weights, true));
    }

    private static Border Row(UIElement[] cells, double[] weights, bool header = false)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            MinHeight = header ? 40 : 62
        }.Background(ThemeResource.Get<Brush>(header ? Subtle : Surface));
        foreach (var weight in weights)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] is FrameworkElement element)
                element.VerticalAlignment = VerticalAlignment.Center;
            var cell = new Border
            {
                Padding = new Thickness(8, header ? 8 : 10, 8, header ? 8 : 10),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = cells[index]
            }.BorderBrush(ThemeResource.Get<Brush>(Stroke));
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
        }
        return new Border
        {
            Padding = new Thickness(12, 0, 12, 0),
            Child = grid
        }.Background(ThemeResource.Get<Brush>(header ? Subtle : Surface));
    }

    private static Border EmptyMessage(string message, System.Linq.Expressions.Expression<Func<bool>> visible) =>
        new Border
        {
            Padding = new Thickness(20),
            Child = new TextBlock { Text = message, FontSize = 12 }
                .Foreground(ThemeResource.Get<Brush>(SecondaryText))
        }.Visibility(x => x.Binding(MemberName(visible)).Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed));

    private static StackPanel Pager(
        System.Linq.Expressions.Expression<Func<string>> label,
        System.Linq.Expressions.Expression<Func<bool>> canPrevious,
        System.Linq.Expressions.Expression<Func<IAsyncRelayCommand>> previous,
        System.Linq.Expressions.Expression<Func<bool>> canNext,
        System.Linq.Expressions.Expression<Func<IAsyncRelayCommand>> next)
    {
        var previousButton = new Button { Content = "Trước" }
            .IsEnabled(x => x.Binding(MemberName(canPrevious))).Command(x => x.Binding(MemberName(previous)));
        var nextButton = new Button { Content = "Sau" }
            .IsEnabled(x => x.Binding(MemberName(canNext))).Command(x => x.Binding(MemberName(next)));
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(12),
            Children =
            {
                previousButton,
                new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                    .Foreground(ThemeResource.Get<Brush>(SecondaryText)).Text(x => x.Binding(MemberName(label))),
                nextButton
            }
        };
    }

    private Border BuildFooter()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        grid.Children.Add(new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center }
            .Foreground(ThemeResource.Get<Brush>(SecondaryText))
            .Text(x => x.Binding(() => ViewModel.ActivityStatusLabel)));
        var close = new Button { Content = "Đóng", MinWidth = 100, Height = 38 }.Command(CloseCommand);
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);
        return new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(18, 12, 20, 12),
            Child = grid
        }.Background(ThemeResource.Get<Brush>(Subtle))
            .BorderBrush(ThemeResource.Get<Brush>(Stroke));
    }

    private static StackPanel DetailInfo(string glyph, TextBlock value)
    {
        value.FontSize = 12;
        value.Foreground(ThemeResource.Get<Brush>(SecondaryText));
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Icon(glyph, 13, SecondaryText), value }
        };
    }

    private static Button LabeledButton(string glyph, string label, string foreground, string background, IRelayCommand? command = null)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    Icon(glyph, 14, foreground),
                    new TextBlock { Text = label, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
                        .Foreground(ThemeResource.Get<Brush>(foreground))
                }
            },
            Padding = new Thickness(10, 7, 10, 7),
            BorderThickness = new Thickness(0)
        }.Background(ThemeResource.Get<Brush>(background));
        if (command is not null) button.Command = command;
        return button;
    }

    private static Button ActionButton(string glyph, string automationName, IRelayCommand command)
    {
        var button = new Button
        {
            Content = Icon(glyph, 16, Text),
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Command = command
        }.Background(ThemeResource.Get<Brush>(Subtle));
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private static Border Badge(TextBlock text, string background) => new Border
    {
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(8, 4, 8, 4),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Child = text
    }.Background(ThemeResource.Get<Brush>(background));

    private static Grid ToneBadge(
        System.Linq.Expressions.Expression<Func<string>> label,
        Func<string, string> format,
        System.Linq.Expressions.Expression<Func<string>> tone)
    {
        var variants = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (name, foreground, background) in ToneVariants())
        {
            var badge = Badge(BoundText(label, format, 11, foreground, true), background);
            variants.Children.Add(badge.Visibility(x => x.Binding(MemberName(tone))
                .Convert((string value) => value == name ? Visibility.Visible : Visibility.Collapsed)));
        }
        return variants;
    }

    private static Grid ToneText(
        System.Linq.Expressions.Expression<Func<string>> label,
        System.Linq.Expressions.Expression<Func<string>> tone)
    {
        var variants = new Grid();
        foreach (var (name, foreground, _) in ToneVariants())
        {
            var text = BoundText(label, 11, foreground, true);
            variants.Children.Add(text.Visibility(x => x.Binding(MemberName(tone))
                .Convert((string value) => value == name ? Visibility.Visible : Visibility.Collapsed)));
        }
        return variants;
    }

    private static (string Name, string Foreground, string Background)[] ToneVariants() =>
    [
        ("Success", SuccessText, SuccessFill),
        ("Danger", DangerText, DangerFill),
        ("Warning", WarningText, WarningFill),
        ("Neutral", SecondaryText, Subtle)
    ];

    private static StackPanel Lines(params UIElement[] lines)
    {
        var panel = new StackPanel { Spacing = 3 };
        foreach (var line in lines) panel.Children.Add(line);
        return panel;
    }

    private static FontIcon Icon(string glyph, double size, string foreground) => new FontIcon
    {
        Glyph = glyph,
        FontSize = size
    }.Foreground(ThemeResource.Get<Brush>(foreground));

    private static TextBlock BoundText(System.Linq.Expressions.Expression<Func<string>> value, double size, string foreground, bool bold = false) =>
        StyledText(size, foreground, bold).Text(x => x.Binding(MemberName(value)));

    private static TextBlock BoundText<T>(System.Linq.Expressions.Expression<Func<T>> value, Func<T, string> converter,
        double size, string foreground, bool bold = false) =>
        StyledText(size, foreground, bold).Text(x => x.Binding(MemberName(value)).Convert(converter));

    private static TextBlock StyledText(double size, string foreground, bool bold) => new TextBlock
    {
        FontSize = size,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap
    }.Foreground(ThemeResource.Get<Brush>(foreground));

    private static string MemberName(System.Linq.Expressions.LambdaExpression expression) =>
        expression.Body is System.Linq.Expressions.MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Binding expression must select a property.", nameof(expression));

    private static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "KH";
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.TakeLast(Math.Min(2, words.Length)).Select(word => char.ToUpperInvariant(word[0])));
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy · HH:mm", CultureInfo.GetCultureInfo("vi-VN"));

    private static string FormatOptionalDate(DateTime? value) =>
        value?.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN")) ?? "—";

    private static string FormatCurrency(decimal value) =>
        $"{value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫";

    private static string FormatOrderStatus(string value) => value switch
    {
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        "PartiallyRefunded" => "Hoàn một phần",
        "Refunded" => "Đã hoàn tiền",
        "Processing" => "Đang xử lý",
        _ => "Chờ xử lý"
    };

    private static string FormatPaymentMethod(string value) => value switch
    {
        "VietQr" => "VietQR",
        "Card" => "Thẻ",
        "Cash" => "Tiền mặt",
        _ => value
    };
}
