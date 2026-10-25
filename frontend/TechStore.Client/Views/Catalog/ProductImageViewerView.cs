using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Catalog;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Catalog;

public sealed class ProductImageViewerView : UserControl
{
    public ProductImageViewer Viewer { get; }
    private readonly Uri _apiBase;
    public ProductImageViewerView(ProductImageViewer viewer, Uri apiBase)
    {
        Viewer = viewer; _apiBase = apiBase;
        var thumbnails = new ItemsRepeater().ItemsSource(x => x.Binding(() => Viewer.VisibleImages))
            .Layout(new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8 })
            .ItemTemplate<ProductImageItem>(BuildThumbnail);
        Content = new StackPanel().DataContext(viewer).Spacing(10).Children(
            CatalogViewParts.ProductImage(x => x.Binding(() => Viewer.SelectedUrl), apiBase, 44).MinHeight(260),
            new ComboBox().Header("Xem ảnh của").ItemsSource(x => x.Binding(() => Viewer.Filters))
                .DisplayMemberPath("Label").SelectedItem(x => x.Binding(() => Viewer.SelectedFilter).Mode(BindingMode.TwoWay))
                .HorizontalAlignment(HorizontalAlignment.Stretch)
                .Visibility(x => x.Binding(() => Viewer.HasFilter).Convert((bool v) => CatalogViewParts.ShowIf(v))),
            new ScrollViewer().HorizontalScrollBarVisibility(ScrollBarVisibility.Auto).VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
                .HorizontalScrollMode(ScrollMode.Enabled).VerticalScrollMode(ScrollMode.Disabled)
                .Visibility(x => x.Binding(() => Viewer.HasThumbnails).Convert((bool v) => CatalogViewParts.ShowIf(v)))
                .Content(thumbnails));
    }
    private UIElement BuildThumbnail(ProductImageItem item) => new Button().Width(72).Height(72).Padding(0)
        .Command(Viewer.SelectImageCommand).CommandParameter(x => x.Binding(() => item))
        .Content(new Grid().Children(
            CatalogViewParts.ProductImage(x => x.Binding(() => item.Url), _apiBase, 20),
            new Border().BorderThickness(new Thickness(3)).BorderBrush(AppColors.Brushes.Primary).IsHitTestVisible(false)
                .Visibility(x => x.Binding(() => item.IsSelected).Convert((bool v) => CatalogViewParts.ShowIf(v)))));
}
