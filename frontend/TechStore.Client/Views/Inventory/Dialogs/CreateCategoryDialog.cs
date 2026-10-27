using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

/// <summary>
/// 100% C# Markup Fluent dialog for Category Creation (UC-CAT-02).
/// Strictly no XAML files. Faithfully adheres to docs/designs/create-category.html.
/// </summary>
public sealed class CreateCategoryDialog : ContentDialog
{
    public CreateCategoryDialogViewModel ViewModel { get; }

    public CreateCategoryDialog(CreateCategoryDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Margin = new Thickness(0);

        Resources["ContentDialogMaxWidth"] = 640d;
        Resources["ContentDialogMinWidth"] = 640d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogTopPadding"] = 0d;
        Resources["ContentDialogBottomPadding"] = 0d;
        Resources["ContentDialogTitleMargin"] = new Thickness(0);
        Resources["ContentDialogContentMargin"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;

        MinWidth = 640;
        MaxWidth = 640;
        Width = 640;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(12);
        this.Background(Theme(AppColors.Surface));

        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;

        Content = BuildLayout();

        ViewModel.RequestClose += (_, _) => Hide();
    }

    private FrameworkElement BuildLayout()
    {
        var container = new Grid
        {
            Width = 640,
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(12)
        }.Background(Theme(AppColors.Surface));

        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
        container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Body
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Footer

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        container.Children.Add(header);

        var body = BuildBody();
        Grid.SetRow(body, 1);
        container.Children.Add(body);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        container.Children.Add(footer);

        return container;
    }

    private FrameworkElement BuildHeader()
    {
        var headerGrid = new Grid
        {
            Padding = new Thickness(24, 20, 24, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }.Background(Theme(AppColors.Surface));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBox = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Background(Theme(AppColors.InfoFill));
        var folderIcon = Icon("folder_add", AppColors.InfoText, 22);
        folderIcon.HorizontalAlignment = HorizontalAlignment.Center;
        folderIcon.VerticalAlignment = VerticalAlignment.Center;
        iconBox.Child = folderIcon;
        titleStack.Children.Add(iconBox);

        var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(Text("Thêm danh mục mới", 18, AppColors.Text, 600));
        textStack.Children.Add(Text("Tạo danh mục phân loại sản phẩm mới cho hệ thống TechStore", 13, AppColors.SecondaryText, 400));
        titleStack.Children.Add(textStack);

        Grid.SetColumn(titleStack, 0);
        headerGrid.Children.Add(titleStack);

        var closeBtn = new Button
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Content = Icon("close", AppColors.SecondaryText, 18),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        }.Style(AppControlStyles.Styles.QuietButton);
        closeBtn.Command(x => x.Binding(() => ViewModel.CancelCommand));
        ToolTipService.SetToolTip(closeBtn, "Đóng cửa sổ");

        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(closeBtn);

        var headerContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        headerContainer.Children.Add(headerGrid);
        headerContainer.Children.Add(new Border
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }.Background(Theme(AppColors.Stroke)));

        return headerContainer;
    }

    private FrameworkElement BuildBody()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        var form = new StackPanel
        {
            Spacing = 16,
            Padding = new Thickness(24, 20, 24, 20),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        form.Children.Add(BuildErrorBanner());
        form.Children.Add(BuildNameField());
        form.Children.Add(BuildParentCategoryField());
        form.Children.Add(BuildCodeAndIconRow());
        form.Children.Add(BuildDisplayOrderField());
        form.Children.Add(BuildDescriptionField());
        form.Children.Add(BuildPosStatusField());
        form.Children.Add(BuildSystemTipBox());

        scrollViewer.Content = form;
        return scrollViewer;
    }

    private FrameworkElement BuildErrorBanner()
    {
        var banner = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .Background(Theme(AppColors.DangerFill))
        .BorderBrush(Theme(AppColors.DangerStroke));

        var msgText = Text("", 13, AppColors.DangerText, 500);
        msgText.TextWrapping = TextWrapping.Wrap;
        msgText.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        banner.Child = msgText;
        banner.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));

        return banner;
    }

    private FrameworkElement BuildNameField()
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };

        var labelGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labelStack.Children.Add(Text("Tên danh mục", 14, AppColors.Text, 500));
        labelStack.Children.Add(Text(" *", 14, AppColors.DangerText, 700));
        Grid.SetColumn(labelStack, 0);
        labelGrid.Children.Add(labelStack);

        var slugStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        slugStack.Children.Add(Text("Slug tự động: ", 12, AppColors.SecondaryText, 400));
        var slugVal = Text("", 12, AppColors.InfoText, 600);
        slugVal.Text(x => x.Binding(() => ViewModel.AutoSlug));
        slugStack.Children.Add(slugVal);
        Grid.SetColumn(slugStack, 1);
        labelGrid.Children.Add(slugStack);

        panel.Children.Add(labelGrid);

        var input = new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 14,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(1),
            PlaceholderText = "Ví dụ: Tai nghe & Âm thanh, Đồng hồ thông minh...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Canvas))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        input.Text(x => x.Binding(() => ViewModel.Name).Mode(BindingMode.TwoWay));
        panel.Children.Add(input);

        return panel;
    }

    private FrameworkElement BuildParentCategoryField()
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(Text("Danh mục cha", 14, AppColors.Text, 500));

        var combo = new ComboBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.ParentOptions,
            DisplayMemberPath = nameof(ParentCategoryOption.DisplayName)
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        combo.SelectedItem(x => x.Binding(() => ViewModel.SelectedParentOption).Mode(BindingMode.TwoWay));
        panel.Children.Add(combo);

        panel.Children.Add(Text("Chọn danh mục cha để phân cấp phân loại hoặc giữ nguyên để làm danh mục gốc.", 12, AppColors.SecondaryText, 400));
        return panel;
    }

    private FrameworkElement BuildCodeAndIconRow()
    {
        var grid = new Grid { ColumnSpacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var codePanel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        codePanel.Children.Add(Text("Mã phân cấp SKU / Danh mục", 14, AppColors.Text, 500));
        var codeInput = new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(1),
            PlaceholderText = "VD: CAT-AUDIO-01",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Canvas))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        codeInput.Text(x => x.Binding(() => ViewModel.ClientCode).Mode(BindingMode.TwoWay));
        codePanel.Children.Add(codeInput);
        Grid.SetColumn(codePanel, 0);
        grid.Children.Add(codePanel);

        var iconPanel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        iconPanel.Children.Add(Text("Biểu tượng đại diện", 14, AppColors.Text, 500));

        var iconSelector = new Border
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));
        var iconSelectorGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        iconSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        iconSelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconPreviewStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        }.Background(Theme(AppColors.Surface));
        iconBox.Child(x => x.Binding(() => ViewModel.SelectedIcon).Convert((string symbol) => Icon(symbol, AppColors.InfoText, 16)));
        var iconNameText = Text(ViewModel.SelectedIconLabel, 13, AppColors.Text, 500);
        iconNameText.Text(x => x.Binding(() => ViewModel.SelectedIconLabel));

        iconPreviewStack.Children.Add(iconBox);
        iconPreviewStack.Children.Add(iconNameText);
        Grid.SetColumn(iconPreviewStack, 0);
        iconSelectorGrid.Children.Add(iconPreviewStack);

        var cycleBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        }.Style(AppControlStyles.Styles.QuietButton);
        cycleBtn.Command(x => x.Binding(() => ViewModel.CycleIconCommand));
        var cycleContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        cycleContent.Children.Add(Icon("sync", AppColors.InfoText, 14));
        cycleContent.Children.Add(Text("Chọn icon", 12, AppColors.InfoText, 500));
        cycleBtn.Content = cycleContent;
        Grid.SetColumn(cycleBtn, 1);
        iconSelectorGrid.Children.Add(cycleBtn);

        iconSelector.Child = iconSelectorGrid;
        iconPanel.Children.Add(iconSelector);

        Grid.SetColumn(iconPanel, 1);
        grid.Children.Add(iconPanel);

        return grid;
    }

    private FrameworkElement BuildDisplayOrderField()
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(Text("Thứ tự hiển thị & Vị trí ưu tiên", 14, AppColors.Text, 500));

        var input = new NumberBox
        {
            Width = 180,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(0),
            FontSize = 14,
            Padding = new Thickness(8, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Minimum = 0,
            Maximum = 9999,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Value = ViewModel.DisplayOrder
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Foreground(Theme(AppColors.Text));
        input.Value(x => x.Binding(() => ViewModel.DisplayOrder).Mode(BindingMode.TwoWay));
        panel.Children.Add(input);

        return panel;
    }

    private FrameworkElement BuildDescriptionField()
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };

        var labelGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = Text("Mô tả chi tiết & Ghi chú SEO", 14, AppColors.Text, 500);
        label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 0);
        labelGrid.Children.Add(label);

        var optBadge = Text("Tùy chọn", 12, AppColors.SecondaryText, 400);
        optBadge.HorizontalAlignment = HorizontalAlignment.Right;
        optBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(optBadge, 1);
        labelGrid.Children.Add(optBadge);

        panel.Children.Add(labelGrid);

        var input = new TextBox
        {
            Height = 80,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(1),
            PlaceholderText = "Mô tả vắn tắt về danh mục sản phẩm, phục vụ tìm kiếm và quản lý nội bộ...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Canvas))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        input.Text(x => x.Binding(() => ViewModel.Description).Mode(BindingMode.TwoWay));
        panel.Children.Add(input);

        return panel;
    }

    private FrameworkElement BuildPosStatusField()
    {
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { Spacing = 2, Padding = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(Text("Hiển thị trên hệ thống bán hàng & POS", 14, AppColors.Text, 500));
        textStack.Children.Add(Text("Cho phép nhân viên bán hàng và thu ngân chọn danh mục này khi tìm sản phẩm", 12, AppColors.SecondaryText, 400));
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        var toggle = new ToggleSwitch
        {
            Width = 44,
            MinWidth = 44,
            OnContent = null,
            OffContent = null,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0)
        };
        toggle.IsOn(x => x.Binding(() => ViewModel.IsActive).Mode(BindingMode.TwoWay));
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        card.Child = grid;
        return card;
    }

    private FrameworkElement BuildSystemTipBox()
    {
        var box = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .Background(Theme(AppColors.InfoFill))
        .BorderBrush(Theme(AppColors.InfoStroke));

        var grid = new Grid { ColumnSpacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var infoIcon = Icon("info", AppColors.InfoText, 20);
        infoIcon.VerticalAlignment = VerticalAlignment.Top;
        infoIcon.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetColumn(infoIcon, 0);
        grid.Children.Add(infoIcon);

        var tipText = new TextBlock
        {
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            VerticalAlignment = VerticalAlignment.Center
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Foreground(Theme(AppColors.InfoText));
        tipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Gợi ý: ", FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 } });
        tipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Sau khi tạo danh mục, bạn có thể phân bổ sản phẩm vào danh mục này ngay tại màn hình " });
        tipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Quản lý Sản phẩm", FontWeight = new Windows.UI.Text.FontWeight { Weight = 500 } });
        tipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = " hoặc nhập hàng loạt thông qua tệp tin Excel." });

        Grid.SetColumn(tipText, 1);
        grid.Children.Add(tipText);

        box.Child = grid;
        return box;
    }

    private FrameworkElement BuildFooter()
    {
        var footerContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        footerContainer.Children.Add(new Border
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }.Background(Theme(AppColors.Stroke)));

        var footerGrid = new Grid
        {
            Padding = new Thickness(24, 16, 24, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        }.Background(Theme(AppColors.Surface));
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var keepCreatingCheck = new CheckBox
        {
            Content = Text("Tiếp tục thêm danh mục khác sau khi lưu", 13, AppColors.SecondaryText, 400),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        keepCreatingCheck.IsChecked(x => x.Binding(() => ViewModel.KeepCreating).Mode(BindingMode.TwoWay));
        Grid.SetColumn(keepCreatingCheck, 0);
        footerGrid.Children.Add(keepCreatingCheck);

        var btnStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = new Button
        {
            Content = Text("Hủy bỏ", 14, AppColors.Text, 500),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 9, 16, 9),
            Height = 40
        }
        .Style(AppControlStyles.Styles.QuietButton)
        .Background(Theme(AppColors.Subtle))
        .Foreground(Theme(AppColors.Text));
        cancelBtn.Command(x => x.Binding(() => ViewModel.CancelCommand));
        btnStack.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 9, 18, 9),
            Height = 40
        }
        .Style(AppControlStyles.Styles.PrimaryButton)
        .Background(Theme(AppColors.Primary))
        .Foreground(Theme(AppColors.OnPrimary));
        saveBtn.Command(x => x.Binding(() => ViewModel.SaveCommand));

        var saveContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var ring = new ProgressRing
        {
            Width = 16,
            Height = 16
        }.Foreground(Theme(AppColors.OnPrimary));
        ring.IsActive(x => x.Binding(() => ViewModel.IsBusy));
        ring.Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert(ShowIf));
        var addIcon = Icon("add", AppColors.OnPrimary, 16);
        addIcon.Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => busy ? Visibility.Collapsed : Visibility.Visible));
        var addText = Text("Thêm danh mục", 14, AppColors.OnPrimary, 600);

        saveContent.Children.Add(ring);
        saveContent.Children.Add(addIcon);
        saveContent.Children.Add(addText);
        saveBtn.Content = saveContent;
        btnStack.Children.Add(saveBtn);

        Grid.SetColumn(btnStack, 1);
        footerGrid.Children.Add(btnStack);

        footerContainer.Children.Add(footerGrid);
        return footerContainer;
    }
}
