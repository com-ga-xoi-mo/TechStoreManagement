using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Categories.ViewModels;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Categories.Pages;

internal sealed class CategoryTableRows(CategoryManagementViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 320 });
        foreach (var width in new[] { 160, 130, 120, 150 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width), MinWidth = width });
        return grid;
    }

    protected override DependencyObject GetContainerForItemOverride() => new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false, Padding = new Thickness(0)
    };

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container && item is CategoryRow row) container.Content = BuildRow(row);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildRow(CategoryRow row)
    {
        var grid = Columns();
        var normalKey = row.IsRoot ? AppColors.Surface : AppColors.Canvas;
        grid.Background(Theme(normalKey));
        grid.Opacity = row.IsActive ? 1 : 0.75;
        grid.PointerEntered += (_, _) => grid.Background(Theme(AppColors.Subtle));
        grid.PointerExited += (_, _) => grid.Background(Theme(normalKey));
        var name = new Grid { ColumnSpacing = 8 };
        void AddNamePart(FrameworkElement part, bool fill = false)
        {
            var column = name.ColumnDefinitions.Count;
            name.ColumnDefinitions.Add(new ColumnDefinition { Width = fill ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            Grid.SetColumn(part, column);
            name.Children.Add(part);
        }
        if (!row.IsRoot) AddNamePart(Text("↳", 14, AppColors.SecondaryText));
        var chevron = Icon("right", AppColors.SecondaryText, 18);
        chevron.DataContext = row;
        chevron.RenderTransform(x => x.Binding(() => row.IsExpanded)
            .Convert((bool expanded) => (Transform)new RotateTransform { Angle = expanded ? 90d : 0d, CenterX = 9, CenterY = 9 }));
        var expand = Button(chevron, tooltip: $"Mở hoặc thu gọn {row.Name}");
        expand.Width = expand.Height = 24;
        expand.Padding = new Thickness(3);
        expand.DataContext = viewModel;
        expand.Command(x => x.Binding(() => viewModel.ToggleExpansionCommand));
        expand.CommandParameter = row;
        // The expand button is the only element in a row that needs both the row and
        // the workspace ViewModel, so its enabled state keeps the platform binding.
        BindingOperations.SetBinding(expand, IsEnabledProperty,
            new Binding { Source = row, Path = new PropertyPath(nameof(row.CanToggle)) });
        expand.Visibility(ShowIf(row.HasChildren));
        // Roots without children keep the name aligned with expandable root rows.
        if (row.IsRoot || row.HasChildren)
        {
            var slot = new Border { Width = 24, Child = expand };
            AddNamePart(slot);
        }
        if (row.IsRoot)
        {
            var iconKeys = StatusBadgePresentation.GetKeys(row.Icon.Tone);
            AddNamePart(new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1), Child = Icon(row.Icon.Symbol, iconKeys.Text)
            }.Background(Theme(iconKeys.Fill)).BorderBrush(Theme(iconKeys.Stroke)));
        }
        var nameText = Text(size: 14, color: AppColors.Text, weight: row.IsRoot ? (ushort)600 : (ushort)500);
        nameText.DataContext = row;
        nameText.Text(x => x.Binding(() => row.Name));
        ToolTipService.SetToolTip(nameText, row.Name);
        AddNamePart(nameText, fill: true);
        AddCell(grid, name, 0, row, row.IsRoot ? 16 : 48 + (row.Depth - 1) * 24);
        var parent = Text(size: 12, color: AppColors.SecondaryText, weight: row.IsRoot ? (ushort)500 : (ushort)400);
        parent.DataContext = row;
        parent.Text(x => x.Binding(() => row.ParentName));
        AddCell(grid, row.IsRoot ? Badge(parent, StatusTone.Neutral) : parent, 1, row);
        var count = Text(size: 14, color: row.IsRoot ? AppColors.Text : AppColors.SecondaryText, weight: row.IsRoot ? (ushort)500 : (ushort)400);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.DataContext = row;
        count.Text(x => x.Binding(() => row.CountText));
        AddCell(grid, count, 2, row);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        var trackWidth = row.IsRoot ? 36 : 28;
        var trackHeight = row.IsRoot ? 20 : 16;
        status.Children.Add(new Border
        {
            Width = trackWidth, Height = trackHeight, CornerRadius = new CornerRadius(trackHeight / 2d),
            Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center,
            Child = new Border
            {
                Width = trackHeight - 4, Height = trackHeight - 4,
                CornerRadius = new CornerRadius((trackHeight - 4) / 2d),
                HorizontalAlignment = row.IsActive ? HorizontalAlignment.Right : HorizontalAlignment.Left
            }.Background(Theme(row.IsActive ? AppColors.OnPrimary : AppColors.Surface))
        }.Background(Theme(row.IsActive ? AppColors.Primary : AppColors.Subtle)));
        status.Children.Add(Text(row.StatusText, 12, AppColors.SecondaryText, 500));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(status, $"Trạng thái: {row.StatusText}");
        var statusButton = new Button
        {
            Content = status,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
            MinHeight = 0,
            IsTabStop = true,
            CommandParameter = row,
            DataContext = viewModel
        }.Style(AppControlStyles.Styles.QuietButton);
        statusButton.Command(x => x.Binding(() => viewModel.ToggleStatusCommand));
        ToolTipService.SetToolTip(statusButton, $"Đổi trạng thái {row.Name}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(statusButton, $"Đổi trạng thái {row.Name}");
        AddCell(grid, statusButton, 3, row);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        var edit = Button(Icon("edit", AppColors.SecondaryText, row.IsRoot ? 18 : 16), tooltip: $"Sửa {row.Name}");
        edit.DataContext = viewModel;
        edit.Command(x => x.Binding(() => viewModel.EditCategoryCommand));
        edit.CommandParameter = row;
        var delete = Button(Icon("delete", AppColors.SecondaryText, row.IsRoot ? 18 : 16), tooltip: $"Xóa {row.Name}");
        delete.DataContext = viewModel;
        delete.Command(x => x.Binding(() => viewModel.DeleteCategoryCommand));
        delete.CommandParameter = row;
        delete.PointerEntered += (_, _) => delete.Content = Icon("delete", AppColors.DangerText, row.IsRoot ? 18 : 16);
        delete.PointerExited += (_, _) => delete.Content = Icon("delete", AppColors.SecondaryText, row.IsRoot ? 18 : 16);
        if (!row.IsRoot) { edit.Padding = new Thickness(4); delete.Padding = new Thickness(4); }
        actions.Children.Add(edit);
        actions.Children.Add(delete);
        AddCell(grid, actions, 4, row);
        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
    }

    private static void AddCell(Grid grid, UIElement content, int column, CategoryRow row, double left = 16)
    {
        var cell = new Border { Child = content, Padding = new Thickness(left, row.IsRoot ? 14 : 12, 16, row.IsRoot ? 14 : 12) };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
