using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Views.Shell;
using TechStore.Client.Styles;
using TechStore.Client.Services.Development;
using TechStore.Client.ViewModels.Development;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Development;

/// <summary>Offline component and binding gallery. Kept as a development-only page.</summary>
public sealed class ComponentGalleryPage : Page
{
    private readonly ComponentGalleryViewModel _viewModel;
    private readonly ShellDiagnosticViewModel _diagnosticViewModel;
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly IMarkupReferenceNavigator? _markupReference;
    private readonly ScrollViewer _scrollViewer;
    private readonly Grid _metricsGrid;
    private readonly Grid _specimensGrid;

    public ComponentGalleryPage(
        ComponentGalleryViewModel viewModel,
        ShellDiagnosticViewModel diagnosticViewModel,
        ContentDialogPresenter dialogPresenter,
        IMarkupReferenceNavigator? markupReference = null)
    {
        _viewModel = viewModel;
        _diagnosticViewModel = diagnosticViewModel;
        _dialogPresenter = dialogPresenter;
        _markupReference = markupReference;
        DataContext = viewModel;

        _metricsGrid = BuildMetrics();
        _specimensGrid = BuildSpecimens();
        _scrollViewer = new ScrollViewer()
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(new StackPanel().Padding(24).Spacing(20).Children(
                BuildHeader(),
                BuildMetricsGrid(),
                BuildSearchAndControls(),
                BuildStateControls(),
                _specimensGrid,
                BuildColorsAndTypography(),
                BuildSettingsExamples(),
                BuildTable(),
                BuildDiagnostics()));

        this.RequestedTheme(x => x.Binding(() => _viewModel.ThemeIndex).Convert(ToTheme))
            .Background(AppColors.Brushes.Canvas)
            .Content(_scrollViewer);

        SizeChanged += (_, args) => UpdateResponsiveLayout(args.NewSize.Width, args.NewSize.Height);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static ElementTheme ToTheme(int index) => index switch
    {
        1 => ElementTheme.Light,
        2 => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private UIElement BuildHeader() =>
        new Grid().ColumnSpacing(16)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new StackPanel().Spacing(4).Children(
                    new TextBlock().Style(AppTypography.Styles.HeadingText).Text("Thư viện Component & Design System"),
                    new TextBlock().Style(AppTypography.Styles.BodyText).Text("Dữ liệu mẫu offline · C# Markup")),
                ThemeSelector().Grid(column: 1),
                MarkupReferenceButton().Grid(column: 2));

    private Button MarkupReferenceButton()
    {
        var button = new Button().Style(AppControlStyles.Styles.Button).Content("Mở trang tham chiếu C# Markup");
        AutomationProperties.SetAutomationId(button, "OpenMarkupReference");
        button.Click += (_, _) => _markupReference?.ShowReference();
        return button;
    }

    private ComboBox ThemeSelector()
    {
        var selector = new ComboBox().Style(AppControlStyles.Styles.ComboBox).Header("Giao diện")
            .MinWidth(160).ItemsSource(_viewModel.Themes)
            .SelectedIndex(x => x.Binding(() => _viewModel.ThemeIndex).Mode(BindingMode.TwoWay));
        AutomationProperties.SetAutomationId(selector, "ThemeSelector");
        return selector;
    }

    private UIElement BuildMetricsGrid() => _metricsGrid;

    private Grid BuildMetrics()
    {
        var grid = new Grid().ColumnSpacing(16);
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new StatCard().Title("Doanh thu hôm nay").ValueText("482.650.000").UnitText("₫")
                    .TrendText("+12,8%").DetailText("so với tuần trước").Tone(StatusTone.Success).IconSource(GetIcon(AppIcons.MoneyIcon)!).Grid(column: 0));
        grid.Children.Add(new StatCard().Title("Đơn hàng").ValueText("128").UnitText("đơn")
                    .TrendText("+8,2%").DetailText("trong ngày").Tone(StatusTone.Info).IconSource(GetIcon(AppIcons.BagIcon)!).Grid(column: 1));
        grid.Children.Add(new StatCard().Title("Sản phẩm trong kho").ValueText("1.024").UnitText("sản phẩm")
                    .TrendText("Ổn định").DetailText("trên 12 danh mục").Tone(StatusTone.Neutral).IconSource(GetIcon(AppIcons.BoxIcon)!).Grid(column: 2));
        grid.Children.Add(new StatCard().Title("Cần chú ý").ValueText("07").UnitText("sản phẩm")
                    .TrendText("Sắp hết hàng").DetailText("kiểm tra tồn kho").Tone(StatusTone.Warning).IconSource(GetIcon(AppIcons.WarningIcon)!).Grid(column: 3));
        return grid;
    }

    private UIElement BuildSearchAndControls() =>
        new Grid().ColumnSpacing(16)
            .ColumnDefinitions(new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)), new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
            .Children(
                Panel("SearchBox · two-way binding", new StackPanel().Spacing(10).Children(
                    new SearchBoxControl().Text(x => x.Binding(() => _viewModel.SearchText).Mode(BindingMode.TwoWay))
                        .SearchCommand(x => x.Binding(() => _viewModel.ActiveSearchCommand))
                        .ClearCommand(x => x.Binding(() => _viewModel.ClearSearchCommand)),
                    ValueRow("Query", x => x.Binding(() => _viewModel.SearchText)),
                    ValueRow("Lần gửi gần nhất", x => x.Binding(() => _viewModel.LastQuery)),
                    ValueRow("Command hiện tại", x => x.Binding(() => _viewModel.UseAlternateCommands).Convert((bool v) => v ? "Alternate" : "Primary")),
                    ValueRow("Số lần gửi primary / alternate", x => x.Binding(() => _viewModel.SubmitCount).Convert((int n) => $"{n} / {_viewModel.AlternateSubmitCount}")),
                    Toggle("Dùng command thay thế", x => x.Binding(() => _viewModel.UseAlternateCommands).Mode(BindingMode.TwoWay)),
                    Toggle("Cho phép tìm kiếm", x => x.Binding(() => _viewModel.CanSearch).Mode(BindingMode.TwoWay)),
                    new SearchBoxControl().PlaceholderText("SearchBox disabled specimen").IsEnabled(false))),
                Panel("Trạng thái và dữ liệu tùy chọn", new StackPanel().Spacing(10).Children(
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                        ActionButton("Loading", _viewModel.ShowLoadingCommand),
                        ActionButton("Error", _viewModel.ShowErrorCommand),
                        ActionButton("Content", _viewModel.ShowContentCommand)),
                    Toggle("Hiện nội dung tùy chọn", x => x.Binding(() => _viewModel.ShowOptionalContent).Mode(BindingMode.TwoWay)),
                    Toggle("Action có thể thực thi", x => x.Binding(() => _viewModel.CanAct).Mode(BindingMode.TwoWay)),
                    ActionButton("Empty action", x => x.Binding(() => _viewModel.ActiveActionCommand)),
                    ValueRow("Actions primary / alternate", x => x.Binding(() => _viewModel.ActionCount).Convert((int n) => $"{n} / {_viewModel.AlternateActionCount}")),
                    new TextBlock().Style(AppTypography.Styles.BodyText).Text(x => x.Binding(() => _viewModel.OptionalDetail)),
                    OptionalIconElement(),
                    new TextBox().Header("Giá trị metric").Text(x => x.Binding(() => _viewModel.MetricValue).Mode(BindingMode.TwoWay)),
                    new TextBox().Header("Chi tiết metric (để trống để bỏ optional)").Text(x => x.Binding(() => _viewModel.MetricDetail).Mode(BindingMode.TwoWay)) )));

    private UIElement BuildStateControls() =>
        Panel("EmptyStateControl và trạng thái StatCard", new StackPanel().Spacing(12).Children(
            new EmptyStateControl().Title("Không có kết quả")
                .Description("Mẫu trạng thái rỗng có action tùy chọn.")
                .ActionText("Thực hiện action")
                .ActionCommand(x => x.Binding(() => _viewModel.ActiveActionCommand)),
            new StatCard().Title("Giá trị bằng không").ValueText(x => x.Binding(() => _viewModel.MetricValue).Convert((string value) => string.IsNullOrWhiteSpace(value) ? "0" : value)).UnitText("₫").DetailText("").Tone(StatusTone.Neutral),
            new StatCard().Title("Thiếu giá trị").ValueText(x => x.Binding(() => _viewModel.MetricError).Convert((string error) => error)).UnitText("").DetailText("Không có dữ liệu").Tone(StatusTone.Neutral)));

    private Grid BuildSpecimens()
    {
        var badges = new StackPanel().Spacing(10).Children(
            new TextBlock().Style(AppTypography.Styles.SectionText).Text("StatusBadge · tone và label"),
            new ComboBox().Header("Tone đang chọn").ItemsSource(_viewModel.BadgeTones)
                .SelectedIndex(x => x.Binding(() => _viewModel.BadgeToneIndex).Mode(BindingMode.TwoWay)),
            new TextBlock().Style(AppTypography.Styles.BodyText).Text("StatusBadge label và tone được binding hai chiều để kiểm tra cập nhật tại chỗ."),
            new StatusBadge().Label(x => x.Binding(() => _viewModel.BadgeLabel).Mode(BindingMode.TwoWay))
                .Tone(x => x.Binding(() => _viewModel.SelectedBadgeTone)),
            new TextBox().Header("Label có thể sửa").Text(x => x.Binding(() => _viewModel.BadgeLabel).Mode(BindingMode.TwoWay)),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                new StatusBadge().Label("Success").Tone(StatusTone.Success),
                new StatusBadge().Label("Warning").Tone(StatusTone.Warning),
                new StatusBadge().Label("Danger").Tone(StatusTone.Danger),
                new StatusBadge().Label("Info").Tone(StatusTone.Info),
                new StatusBadge().Label("").Tone(StatusTone.Neutral)),
            new InfoBar().IsOpen(true).Severity(InfoBarSeverity.Informational)
                .Title("Thông tin").Message("InfoBar mẫu với resource theme hiện hành."));
        var phone = new StackPanel().Spacing(10).Children(
            new TextBlock().Style(AppTypography.Styles.SectionText).Text("Binding hai chiều và validation"),
            new TextBox().Header("Số điện thoại").PlaceholderText("0901234567")
                .Text(x => x.Binding(() => _viewModel.Phone).Mode(BindingMode.TwoWay)),
            new Button().Style(AppControlStyles.Styles.Button).Content("Kiểm tra")
                .Command(x => x.Binding(() => _viewModel.ValidatePhoneCommand)),
            new TextBlock().Style(AppControlStyles.Styles.FieldError).Text(x => x.Binding(() => _viewModel.PhoneError)));
        return new Grid().ColumnSpacing(16)
            .ColumnDefinitions(new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)), new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
            .Children(Panel("StatusBadge và InfoBar", badges), Panel("Form control", phone));
    }

    private UIElement BuildColorsAndTypography() =>
        Panel("Typography, icons và màu semantic", new StackPanel().Spacing(12).Children(
            new TextBlock().Style(AppTypography.Styles.HeadingText).Text("Tiếng Việt: Thiết bị công nghệ & bảo hành"),
            new TextBlock().Style(AppTypography.Styles.BodyText).Text("Aa 0123456789 · Sản phẩm, tồn kho, khách hàng"),
            new TextBlock().Style(AppTypography.Styles.MonoText).Text("IMEI 359012345678901 · SKU TECH-001"),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                Swatch("Canvas", AppColors.Brushes.Canvas), Swatch("Surface", AppColors.Brushes.Surface),
                Swatch("Primary", AppColors.Brushes.Primary), Swatch("Success", AppColors.Brushes.SuccessFill),
                Swatch("Warning", AppColors.Brushes.WarningFill), Swatch("Danger", AppColors.Brushes.DangerFill)),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                IconSample("Money", AppIcons.MoneyIcon), IconSample("Box", AppIcons.BoxIcon),
                IconSample("Warning", AppIcons.WarningIcon), IconSample("Bag", AppIcons.BagIcon), IconSample("Empty", AppIcons.EmptyIcon))));

    private UIElement BuildSettingsExamples() =>
        Panel("CommunityToolkit Settings controls", new StackPanel().Spacing(8).Children(
            new CommunityToolkit.WinUI.Controls.SettingsCard { Header = "Tùy chọn cửa hàng", Description = "Mẫu SettingsCard dùng chung resource và typography." },
            new Expander().Header("Thông tin thêm").Content(new TextBlock().Text("Nội dung mở rộng mẫu."))));

    private UIElement BuildTable()
    {
        var list = new ListView().ItemsSource(_viewModel.Rows);
        list.ItemTemplate<GalleryRow>(_ =>
        {
            var panel = new StackPanel().Orientation(Orientation.Horizontal).Spacing(16);
            panel.Children.Add(new TextBlock().Width(100).Text(x => x.Binding(nameof(GalleryRow.Code))));
            panel.Children.Add(new TextBlock().Width(180).Text(x => x.Binding(nameof(GalleryRow.Customer))));
            panel.Children.Add(new TextBlock().Width(360).Text(x => x.Binding(nameof(GalleryRow.Product))));
            panel.Children.Add(new TextBlock().Width(140).Text(x => x.Binding(nameof(GalleryRow.Amount))));
            return panel;
        });
        return Panel("Danh sách đơn hàng", new StackPanel().Spacing(8).Children(
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(16).Children(
                new TextBlock().Width(100).Text("Mã đơn"), new TextBlock().Width(180).Text("Khách hàng"),
                new TextBlock().Width(360).Text("Sản phẩm"), new TextBlock().Width(140).Text("Thành tiền")), list));
    }

    private UIElement BuildDiagnostics() =>
        Panel("Shell diagnostics", new StackPanel().Spacing(8).Children(
            new TextBlock().Text(x => x.Binding(() => _diagnosticViewModel.DiagnosticStatus)),
            new Button().Style(AppControlStyles.Styles.Button).Content("Bắt đầu tải chờ")
                .Command(x => x.Binding(() => _diagnosticViewModel.StartPendingLoadCommand)),
            new Button().Style(AppControlStyles.Styles.Button).Content("Mô phỏng lỗi")
                .Command(x => x.Binding(() => _diagnosticViewModel.SimulateLoadFailureCommand)),
            new Button().Style(AppControlStyles.Styles.Button).Content("Mở dialog")
                .Command(x => x.Binding(() => _diagnosticViewModel.ShowConfirmDialogCommand)),
            new Button().Style(AppControlStyles.Styles.Button).Content("Thử lại")
                .Command(x => x.Binding(() => _diagnosticViewModel.RetryCommand)),
            BackToGalleryButton()));

    private Button BackToGalleryButton()
    {
        var button = new Button().Style(AppControlStyles.Styles.Button).Content("Về Component Gallery");
        button.Click += (_, _) => _markupReference?.ShowGallery();
        return button;
    }

    private static UIElement Panel(string title, UIElement child) => new Border().Style(AppControlStyles.Styles.Panel)
        .Child(new StackPanel().Spacing(12).Children(new TextBlock().Style(AppTypography.Styles.SectionText).Text(title), child));

    private static UIElement ValueRow(string label, Action<IDependencyPropertyBuilder<string>> binding) =>
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new TextBlock().Foreground(AppColors.Brushes.SecondaryText).Text(label),
            new TextBlock().Foreground(AppColors.Brushes.Text).Text(binding));

    private CheckBox Toggle(string text, Action<IDependencyPropertyBuilder<bool?>> binding)
    {
        var checkBox = new CheckBox().Content(text);
        checkBox.IsChecked(x => binding(x));
        return checkBox;
    }

    private static Button ActionButton(string text, System.Windows.Input.ICommand command) =>
        new Button().Style(AppControlStyles.Styles.Button).Content(text).Command(command);

    private static Button ActionButton(string text, Action<IDependencyPropertyBuilder<System.Windows.Input.ICommand>> binding) =>
        new Button().Style(AppControlStyles.Styles.Button).Content(text).Command(binding);

    private static UIElement Swatch(string label, Action<IDependencyPropertyBuilder<Brush>> brush) => new StackPanel().Spacing(4).Children(
        new Border().Width(48).Height(32).Background(brush).BorderBrush(AppColors.Brushes.Stroke).BorderThickness(new Thickness(1)),
        new TextBlock().Text(label));

    private static UIElement IconSample(string label, string key) => new StackPanel().Spacing(4).Children(
        new IconSourceElement().Width(24).Height(24).IconSource(GetIcon(key)!), new TextBlock().Text(label));

    private static IconSource? GetIcon(string key) => Application.Current.Resources.TryGetValue(key, out var value) ? value as IconSource : null;

    private UIElement OptionalIconElement()
    {
        var icon = new IconSourceElement().Width(24).Height(24).IconSource(GetIcon(AppIcons.MoneyIcon)!);
        icon.Visibility = _viewModel.ShowOptionalContent ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.ShowOptionalContent))
                icon.Visibility = _viewModel.ShowOptionalContent ? Visibility.Visible : Visibility.Collapsed;
        };
        return icon;
    }

    private void UpdateResponsiveLayout(double width, double height)
    {
        var columns = width < 760 ? 1 : width < 1180 ? 2 : 4;
        SetColumns(_metricsGrid, columns);
        SetColumns(_specimensGrid, width < 760 ? 1 : 2);
        _scrollViewer.MaxHeight = double.IsFinite(height) && height > 0 ? height : double.PositiveInfinity;
    }

    private static void SetColumns(Grid grid, int count)
    {
        grid.ColumnDefinitions.Clear();
        for (var i = 0; i < count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < grid.Children.Count; i++)
            if (grid.Children[i] is FrameworkElement element)
                Grid.SetColumn(element, i % count);
        if (count == 1)
            for (var i = 0; i < grid.Children.Count; i++)
                if (grid.Children[i] is FrameworkElement element)
                    Grid.SetRow(element, i);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dialogPresenter.Attach(this, () => null);
        await _diagnosticViewModel.ActivateAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _dialogPresenter.Detach();
        _diagnosticViewModel.Deactivate();
    }
}
