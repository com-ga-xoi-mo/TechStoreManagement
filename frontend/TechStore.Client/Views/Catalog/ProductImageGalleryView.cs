using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Catalog;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Catalog;

public sealed class ProductImageGalleryView : UserControl
{
    public ProductImageGallery Gallery { get; }
    private readonly Uri _apiBase;
    public ProductImageGalleryView(ProductImageGallery gallery, Uri apiBase)
    {
        Gallery = gallery; _apiBase = apiBase;
        var thumbnails = new ItemsRepeater().ItemsSource(x => x.Binding(() => Gallery.Images))
            .Layout(new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8 })
            .ItemTemplate<ProductImageItem>(BuildThumbnail);
        Content = new StackPanel().DataContext(gallery).Spacing(10).Children(
            new Grid().ColumnDefinitions("*, Auto").Children(
                CatalogViewParts.SectionTitle("Ảnh sản phẩm"),
                new TextBlock().Grid(column: 1).Text(x => x.Binding(() => Gallery.CountText)).Foreground(AppColors.Brushes.SecondaryText)),
            CatalogViewParts.ProductImage(x => x.Binding(() => Gallery.LargePreview)
                .Convert((ImagePreview? p) => CatalogViewParts.ToImageSource(p, apiBase)!), 44).MinHeight(260),
            new StatusBadge().Label("Bìa").Tone(StatusTone.Info)
                .Visibility(x => x.Binding(() => Gallery.SelectedImage!.IsCover).Convert((bool v) => CatalogViewParts.ShowIf(v))),
            new ComboBox().Header("Dùng cho").ItemsSource(x => x.Binding(() => Gallery.VariantChoices))
                .DisplayMemberPath("Label").SelectedItem(x => x.Binding(() => Gallery.SelectedVariantChoice).Mode(BindingMode.TwoWay))
                .Visibility(x => x.Binding(() => Gallery.HasSelection).Convert((bool v) => CatalogViewParts.ShowIf(v)))
                .HorizontalAlignment(HorizontalAlignment.Stretch),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(4).Children(
                new Button().Content("Đặt làm ảnh bìa").Command(x => x.Binding(() => Gallery.MakeCoverCommand))
                    .Visibility(x => x.Binding(() => Gallery.ShowMakeCover).Convert((bool v) => CatalogViewParts.ShowIf(v))),
                new Button().Content("<").Command(x => x.Binding(() => Gallery.MoveLeftCommand)),
                new Button().Content(">").Command(x => x.Binding(() => Gallery.MoveRightCommand)),
                new Button().Content("Xóa ảnh").Command(x => x.Binding(() => Gallery.RemoveSelectedCommand))),
            new ScrollViewer().HorizontalScrollBarVisibility(ScrollBarVisibility.Auto).VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
                .HorizontalScrollMode(ScrollMode.Enabled).VerticalScrollMode(ScrollMode.Disabled)
                .Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                    thumbnails,
                    new Button().Content("+ Thêm").MinHeight(72).Command(x => x.Binding(() => Gallery.AddImagesCommand))
                        .Visibility(x => x.Binding(() => Gallery.CanAddMore).Convert((bool v) => CatalogViewParts.ShowIf(v))))),
            new TextBlock().Text(x => x.Binding(() => Gallery.PickMessage)).TextWrapping(TextWrapping.Wrap)
                .Foreground(AppColors.Brushes.WarningText)
                .Visibility(x => x.Binding(() => Gallery.HasPickMessage).Convert((bool v) => CatalogViewParts.ShowIf(v))),
            CatalogViewParts.Caption("JPG, PNG, WebP – tối đa 5 MB mỗi ảnh, 20 ảnh mỗi sản phẩm"));
    }
    private UIElement BuildThumbnail(ProductImageItem item) => new StackPanel().Spacing(2).Children(
        new Button().Padding(0).Width(72).Height(72).Command(Gallery.SelectImageCommand)
            .CommandParameter(x => x.Binding(() => item))
            .Content(new Grid().Children(
                CatalogViewParts.ProductImage(x => x.Binding(() => item.Preview)
                    .Convert((ImagePreview p) => CatalogViewParts.ToImageSource(p, _apiBase)!), 20),
                new Border().BorderThickness(new Thickness(3)).BorderBrush(AppColors.Brushes.Primary).IsHitTestVisible(false)
                    .Visibility(x => x.Binding(() => item.IsSelected).Convert((bool v) => CatalogViewParts.ShowIf(v))))),
        new TextBlock().Text("Bìa").Foreground(AppColors.Brushes.Primary)
            .Visibility(x => x.Binding(() => item.IsCover).Convert((bool v) => CatalogViewParts.ShowIf(v))),
        new TextBlock().Text("Mới").Foreground(AppColors.Brushes.WarningText)
            .Visibility(x => x.Binding(() => item.IsNew).Convert((bool v) => CatalogViewParts.ShowIf(v))),
        new TextBlock().Text(x => x.Binding(() => item.VariantLabel)).Foreground(AppColors.Brushes.SecondaryText)
            .Visibility(x => x.Binding(() => item.HasVariantLabel).Convert((bool v) => CatalogViewParts.ShowIf(v))));
}
