using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using TechStore.Client.ViewModels.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Catalog;

internal static class CatalogViewParts
{
    private static readonly ConditionalWeakTable<byte[], BitmapImage> PreviewCache = new();

    public static ImageSource? ToImageSource(ImagePreview? preview, Uri apiBase)
    {
        if (preview?.Bytes is { } bytes)
        {
            try
            {
                return PreviewCache.GetValue(bytes, content =>
                {
                    var bitmap = new BitmapImage();
                    using var stream = new MemoryStream(content);
                    bitmap.SetSource(stream.AsRandomAccessStream());
                    return bitmap;
                });
            }
            catch (Exception) { return null; }
        }
        return CatalogPresentation.ResolveImageUri(preview?.Url, apiBase) is { } uri ? new BitmapImage(uri) : null;
    }

    public static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    public static TextBlock Caption(string text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap)
            .Text(text);

    public static TextBlock SectionTitle(string text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.SectionText))
            .Foreground(AppColors.Brushes.Text)
            .Text(text);

    public static Grid ProductImage(Action<IDependencyPropertyBuilder<ImageSource>> source, double iconSize)
    {
        var fontIcon = new FontIcon()
            .Glyph("\uE8B9")
            .Foreground(AppColors.Brushes.SecondaryText)
            .FontSize(iconSize)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

        var image = new Image()
            .Stretch(Stretch.UniformToFill)
            .Source(source);

        return new Grid()
            .Background(AppColors.Brushes.Subtle)
            .Children(fontIcon, image);
    }

    public static Grid ProductImage(Action<IDependencyPropertyBuilder<string?>> url, Uri apiBase, double iconSize)
    {
        var fontIcon = new FontIcon()
            .Glyph("\uE8B9")
            .Foreground(AppColors.Brushes.SecondaryText)
            .FontSize(iconSize)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

        var image = new Image()
            .Stretch(Stretch.UniformToFill)
            .Source(sourceBuilder =>
            {
                var adapter = new ImageSourceBindingAdapter(sourceBuilder, apiBase);
                url(adapter);
            });

        return new Grid()
            .Background(AppColors.Brushes.Subtle)
            .Children(fontIcon, image);
    }

    private sealed class ImageSourceBindingAdapter(
        IDependencyPropertyBuilder<ImageSource> targetBuilder,
        Uri apiBase) : IDependencyPropertyBuilder<string?>
    {
        BindingBuilder<TSource, string?> IDependencyPropertyBuilder<string?>.Binding<TSource>(Func<TSource> getter, string? propertyName)
        {
            targetBuilder.Binding(getter, propertyName)
                .Convert((TSource val) =>
                {
                    var str = val as string;
                    return (CatalogPresentation.ResolveImageUri(str, apiBase) is { } uri
                        ? new BitmapImage(uri)
                        : null)!;
                });
            return null!;
        }

        BindingFactory<string?> IDependencyPropertyBuilder<string?>.Bind() => null!;
        BindingFactory<TSource, string?> IDependencyPropertyBuilder<string?>.Bind<TSource>() => null!;
        BindingFactory<string?> IDependencyPropertyBuilder<string?>.Bind(string path) => null!;
        BindingFactory<TSource, string?> IDependencyPropertyBuilder<string?>.Bind<TSource>(string path) => null!;
        BindingFactory<TSource, string?> IDependencyPropertyBuilder<string?>.Bind<TSource>(Func<TSource> getter, string? propertyName) => null!;
        BindingBuilder<string?> IDependencyPropertyBuilder<string?>.Binding() => null!;
        BindingBuilder<TSource, string?> IDependencyPropertyBuilder<string?>.Binding<TSource>() => null!;
        BindingBuilder<string?> IDependencyPropertyBuilder<string?>.Binding(string path)
        {
            targetBuilder.Binding(path)
                .Convert((object? val) =>
                {
                    var str = val as string;
                    return (CatalogPresentation.ResolveImageUri(str, apiBase) is { } uri
                        ? new BitmapImage(uri)
                        : null)!;
                });
            return null!;
        }
        BindingBuilder<TSource, string?> IDependencyPropertyBuilder<string?>.Binding<TSource>(string path)
        {
            targetBuilder.Binding(path)
                .Convert((object? val) =>
                {
                    var str = val as string;
                    return (CatalogPresentation.ResolveImageUri(str, apiBase) is { } uri
                        ? new BitmapImage(uri)
                        : null)!;
                });
            return null!;
        }
        SourceBindingBuilder<string?> IDependencyPropertyBuilder<string?>.Source<TElement>(TElement element) => null!;
        TypedSourceBindingBuilder<TElement, string?> IDependencyPropertyBuilder<string?>.Source<TElement>(string elementName) => null!;
        SourceBindingBuilder<string?> IDependencyPropertyBuilder<string?>.Source(string elementName) => null!;
        TypedSourceBindingBuilder<TElement, string?> IDependencyPropertyBuilder<string?>.RelativeSource<TElement>(RelativeSourceMode mode) => null!;
        SourceBindingBuilder<string?> IDependencyPropertyBuilder<string?>.RelativeSource(RelativeSourceMode mode) => null!;
        BindingFactory<string?> IDependencyPropertyBuilder<string?>.TemplateBind(string path) => null!;
        BindingFactory<TSource, string?> IDependencyPropertyBuilder<string?>.TemplateBind<TSource>(string path) => null!;
        BindingFactory<TSource, string?> IDependencyPropertyBuilder<string?>.TemplateBind<TSource>(Func<TSource> getter, string? propertyName) => null!;
        void IDependencyPropertyBuilder<string?>.StaticResource(string resourceKey) { }
        void IDependencyPropertyBuilder<string?>.ThemeResource(string resourceKey) { }
        void IDependencyPropertyBuilder<string?>.Localize() { }
        void IDependencyPropertyBuilder<string?>.Localize(string key) { }
    }
}
