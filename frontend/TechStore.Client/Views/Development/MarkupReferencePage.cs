using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Development;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Development;

/// <summary>
/// Reference specimen for the team's C# Markup standard (see AGENTS.md section 6).
/// Every value shown is sample data. The page owns no logic: it declares the tree and bindings only.
/// </summary>
public sealed class MarkupReferencePage : Page
{
    public MarkupReferenceViewModel ViewModel { get; }

    public MarkupReferencePage(MarkupReferenceViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        this.RequestedTheme(x => x.Binding(() => ViewModel.ThemeIndex).Convert((int index) => ToTheme(index)))
            .Background(AppColors.Brushes.Canvas)
            .Content(
                new ScrollViewer()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .Content(
                        new StackPanel()
                            .Padding(24)
                            .Spacing(16)
                            .Children(
                                BuildHeader(),
                                BuildSearchPanel(),
                                BuildStateButtons(),
                                BuildStateArea())));
    }

    private static ElementTheme ToTheme(int index) => index switch
    {
        1 => ElementTheme.Light,
        2 => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private UIElement BuildHeader() =>
        new Grid()
            .ColumnSpacing(16)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(GridLength.Auto))
            .Children(
                new StackPanel()
                    .Spacing(4)
                    .Children(
                        new TextBlock()
                            .Style(AppTypography.Styles.HeadingText)
                            .Text("Mẫu tham chiếu C# Markup"),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new StatusBadge().Label("Dữ liệu minh họa").Tone(StatusTone.Info),
                                new TextBlock()
                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Text("Không gọi backend"))),
                new ComboBox()
                    .Style(AppControlStyles.Styles.ComboBox)
                    .Header("Giao diện")
                    .MinWidth(160)
                    .ItemsSource(ViewModel.Themes)
                    .SelectedIndex(x => x.Binding(() => ViewModel.ThemeIndex).Mode(BindingMode.TwoWay))
                    .Grid(column: 1),
                new Button()
                    .Style(AppControlStyles.Styles.Button)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .Content("Quay lại thư viện")
                    .Command(x => x.Binding(() => ViewModel.BackCommand))
                    .Grid(column: 2));

    private UIElement BuildSearchPanel() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Style(AppTypography.Styles.SectionText)
                            .Text("Binding hai chiều và command"),
                        new SearchBoxControl()
                            .Text(x => x.Binding(() => ViewModel.Query).Mode(BindingMode.TwoWay))
                            .SearchCommand(x => x.Binding(() => ViewModel.SearchCommand))
                            .ClearCommand(x => x.Binding(() => ViewModel.ResetCommand)),
                        new StackPanel()
                            .Spacing(4)
                            .Children(
                                ValueRow("Giá trị trong ViewModel:", x => x.Binding(() => ViewModel.Query)),
                                ValueRow("Gửi gần nhất:", x => x.Binding(() => ViewModel.LastSubmitted)),
                                ValueRow("Số lần gửi:", x => x.Binding(() => ViewModel.SubmitCount).Convert((int count) => count.ToString()))),
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Đặt lại")
                            .Command(x => x.Binding(() => ViewModel.ResetCommand))));

    private static UIElement ValueRow(string label, Action<IDependencyPropertyBuilder<string>> value) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(8)
            .Children(
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text(label),
                new TextBlock()
                    .Style(AppResources.GetStyle(AppTypography.MonoText))
                    .Foreground(AppColors.Brushes.Text)
                    .Text(value));

    private UIElement BuildStateButtons() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(8)
            .Children(
                new Button().Style(AppControlStyles.Styles.Button).Content("Đang tải")
                    .Command(x => x.Binding(() => ViewModel.ShowLoadingCommand)),
                new Button().Style(AppControlStyles.Styles.Button).Content("Rỗng")
                    .Command(x => x.Binding(() => ViewModel.ShowEmptyCommand)),
                new Button().Style(AppControlStyles.Styles.Button).Content("Lỗi")
                    .Command(x => x.Binding(() => ViewModel.ShowErrorCommand)),
                new Button().Style(AppControlStyles.Styles.PrimaryButton).Content("Nội dung")
                    .Command(x => x.Binding(() => ViewModel.ShowContentCommand)));

    // Exactly one of the four children is visible, driven by the ViewModel's state flags.
    private UIElement BuildStateArea() =>
        new Grid()
            .Children(
                BuildLoading(),
                BuildEmpty(),
                BuildError(),
                BuildContent());

    private UIElement BuildLoading() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(12)
                    .Children(
                        new ProgressRing().Width(28).Height(28).IsActive(x => x.Binding(() => ViewModel.IsLoading)),
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text("Đang tải số liệu minh họa...")));

    private UIElement BuildEmpty() =>
        new EmptyStateControl()
            .Title("Chưa có dữ liệu")
            .Description("Đây là trạng thái rỗng của trang mẫu.")
            .ActionText("Hiện nội dung")
            .ActionCommand(x => x.Binding(() => ViewModel.ShowContentCommand))
            .Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert((bool v) => ShowIf(v)));

    private UIElement BuildError() =>
        new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => ShowIf(v)))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Style(AppControlStyles.Styles.FieldError)
                            .Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                        new Button()
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Thử lại")
                            .Command(x => x.Binding(() => ViewModel.RetryCommand))));

    private UIElement BuildContent()
    {
        var cards = new Grid().ColumnSpacing(16);
        for (var i = 0; i < ViewModel.Metrics.Length; i++)
        {
            var metric = ViewModel.Metrics[i];
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cards.Children.Add(new StatCard()
                .Title(metric.Title)
                .ValueText(metric.Value)
                .UnitText(metric.Unit)
                .TrendText(metric.Trend)
                .DetailText(metric.Detail)
                .Tone(metric.Tone)
                .Grid(column: i));
        }

        var badges = new StackPanel().Orientation(Orientation.Horizontal).Spacing(8);
        foreach (var badge in ViewModel.Badges)
            badges.Children.Add(new StatusBadge().Label(badge.Label).Tone(badge.Tone));

        return new StackPanel()
            .Spacing(16)
            .Visibility(x => x.Binding(() => ViewModel.IsContent).Convert((bool v) => ShowIf(v)))
            .Children(cards, badges);
    }
}
