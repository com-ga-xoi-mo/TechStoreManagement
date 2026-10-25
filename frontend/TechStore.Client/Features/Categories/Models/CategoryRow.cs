using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using TechStore.Client.Controls;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Features.Categories.Models;

public enum CategoryStatusFilter { All, Active, Hidden }

public enum CategoryWorkspaceTab { Categories, Inventory, SerialImei }

public sealed record CategoryStatusOption(CategoryStatusFilter Value, string Label);

public sealed record CategoryIconDescriptor(string Symbol, StatusTone Tone);

public sealed partial class CategoryRow(CategoryNode node, CategoryRow? parent) : ObservableObject
{
    public CategoryNode Node { get; } = node;
    public Guid Id => Node.Id;
    public string Name => Node.Name;
    public CategoryRow? Parent { get; } = parent;
    public string ParentName => Parent?.Name ?? "Danh mục gốc";
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;
    public bool IsRoot => Parent is null;
    public bool IsActive => Node.IsActive;
    public string StatusText => IsActive ? "Bật" : "Đã ẩn";
    public bool HasChildren => Node.Children.Count > 0;
    public CategoryIconDescriptor Icon { get; } = CategoryIconResolver.Resolve(node.Name, node.Slug);
    public string CountText => (IsRoot ? Node.ProductCount : Node.SkuCount) is int count
        ? $"{count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} {(IsRoot ? "sản phẩm" : "SKU")}" : "—";

    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isForcedOpen;
    [ObservableProperty] private bool _isContextOnly;

    public bool CanToggle => HasChildren && !IsForcedOpen;

    partial void OnIsForcedOpenChanged(bool value) => OnPropertyChanged(nameof(CanToggle));
}
