using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;

namespace TechStore.Client.ViewModels.Catalog;

public sealed class ProductImageSaver(IProductImageRepository images)
{
    public async Task SaveAsync(Guid productId, ProductImageGallery gallery, CancellationToken cancellationToken)
    {
        if (!gallery.HasChanges) return;
        foreach (var id in gallery.RemovedImageIds.ToArray())
        {
            try { await images.DeleteAsync(productId, id, cancellationToken); }
            catch (ApiException ex) when (ex.StatusCode == 404) { }
            gallery.ForgetRemoved(id);
        }
        foreach (var item in gallery.Images.Where(i => i.IsNew))
        {
            if (item.Variant != null && item.Variant.Id == null) throw new InvalidOperationException("Biến thể của ảnh chưa được lưu.");
            var saved = await images.UploadAsync(productId, new(item.FileName, item.ContentType, item.PendingContent!, item.Variant?.Id), cancellationToken);
            item.MarkSaved(saved.Id, saved.Url);
        }
        if (gallery.Images.Count > 0)
            await images.SaveLayoutAsync(productId, gallery.Images.Select(i => new ImageLayoutEntry(i.Id!.Value, i.Variant?.Id)).ToArray(), cancellationToken);
        gallery.AcceptChanges();
    }
}
