using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

public sealed class DeviceStatusDialog : ContentDialog
{
    public DeviceStatusDialogViewModel ViewModel { get; }

    public DeviceStatusDialog(DeviceStatusDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(8);
        Width = 480;
        MaxWidth = 480;
        Resources["ContentDialogMaxWidth"] = 480d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        this.Background(Theme(AppColors.Surface));
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 480, MaxWidth = 480 }.Background(Theme(AppColors.Surface));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(BuildHeader());

        var scroll = new ScrollViewer
        {
            MaxHeight = 500,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildBody()
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private Grid BuildHeader()
    {
        var header = new Grid
        {
            Padding = new Thickness(16, 12, 12, 12),
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.BorderBrush(Theme(AppColors.Stroke));

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Text("", 16, AppColors.Text, 600);
        title.Text(x => x.Binding(() => ViewModel.Title));
        header.Children.Add(title);

        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Đóng");
        close.Command(x => x.Binding(() => ViewModel.CancelCommand));
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(16), Spacing = 14 };

        // Device summary banner
        var deviceCard = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));

        var deviceCardStack = new StackPanel { Spacing = 4 };
        var idText = Text("", 13, AppColors.Text, 600);
        idText.Text(x => x.Binding(() => ViewModel.DeviceIdentifier));
        deviceCardStack.Children.Add(idText);

        var descText = Text("", 12, AppColors.SecondaryText);
        descText.Text(x => x.Binding(() => ViewModel.DeviceDescription));
        deviceCardStack.Children.Add(descText);

        deviceCard.Child = deviceCardStack;
        body.Children.Add(deviceCard);

        // Reason section (for ReportDefective)
        if (ViewModel.IsReportDefective)
        {
            var reasonPanel = new StackPanel { Spacing = 6 };
            var required = Text("*", 13, AppColors.DangerText, 700);
            required.Margin = new Thickness(3, 0, 0, 0);
            var reasonLabel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { Text("Lý do báo lỗi", 13, AppColors.Text, 600), required }
            };
            reasonPanel.Children.Add(reasonLabel);

            // Preset buttons fill the box; the TextBox pushes every keystroke so the confirm
            // button enables as soon as a reason is typed.
            var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var preset in ViewModel.PresetReasons)
            {
                var presetButton = Button(Text(preset, 12, AppColors.Text, 500), tooltip: $"Dùng lý do \"{preset}\"");
                presetButton.Command = ViewModel.UsePresetReasonCommand;
                presetButton.CommandParameter = preset;
                presets.Children.Add(presetButton);
            }
            reasonPanel.Children.Add(presets);

            var reasonBox = new TextBox
            {
                PlaceholderText = "Chọn lý do gợi ý hoặc tự nhập (ví dụ: Hư màn hình)",
                CornerRadius = new CornerRadius(8),
                FontSize = 13,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                MaxLength = 200,
                HorizontalAlignment = HorizontalAlignment.Stretch
            }
            .FontFamily(AppTypography.Fonts.RegularFont)
            .Background(Theme(AppColors.Surface))
            .Foreground(Theme(AppColors.Text))
            .BorderBrush(Theme(AppColors.Stroke));
            reasonBox.Text(x => x.Binding(() => ViewModel.Reason).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));

            reasonPanel.Children.Add(reasonBox);
            body.Children.Add(reasonPanel);
        }

        // Note section (for both modes)
        var notePanel = new StackPanel { Spacing = 6 };
        notePanel.Children.Add(Text("Ghi chú", 13, AppColors.Text, 600));

        var noteInput = new TextBox
        {
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            PlaceholderText = "Nhập ghi chú chi tiết (tùy chọn)...",
            MinHeight = 70,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        noteInput.Text(x => x.Binding(() => ViewModel.Note).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));

        notePanel.Children.Add(noteInput);
        body.Children.Add(notePanel);

        // Error message
        var error = Text("", 12, AppColors.DangerText);
        error.TextWrapping = TextWrapping.Wrap;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        error.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        body.Children.Add(error);

        return body;
    }

    private Grid BuildFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            BorderThickness = new Thickness(0, 1, 0, 0)
        }.BorderBrush(Theme(AppColors.Stroke));

        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = Button(Text("Hủy", 13, AppColors.SecondaryText, 600));
        cancel.Command(x => x.Binding(() => ViewModel.CancelCommand));

        var confirm = new Button
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 8, 16, 8)
        };
        if (ViewModel.IsReportDefective)
        {
            confirm.Style(AppControlStyles.Styles.DangerButton);
        }
        else
        {
            confirm.Style(AppControlStyles.Styles.PrimaryButton);
        }

        var confirmText = Text("", 13, ViewModel.IsReportDefective ? AppColors.DangerText : AppColors.OnPrimary, 600);
        confirmText.Text(x => x.Binding(() => ViewModel.PrimaryButtonText));
        confirm.Content = confirmText;
        confirm.Command(x => x.Binding(() => ViewModel.ConfirmCommand));
        confirm.IsEnabled(x => x.Binding(() => ViewModel.CanSubmit));

        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);

        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        return footer;
    }
}
