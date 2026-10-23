using System.Collections.ObjectModel;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductImageSaverTests
{
    private readonly RecordingImageRepository _repository = new();
    private readonly ProductImageGallery _gallery = new(new ScriptedImageFilePicker(), new ObservableCollection<VariantRowItem>(), () => default);
    private readonly Guid _id = Guid.NewGuid();
    private void AddNew(string name = "a.png") => _gallery.Images.Add(ProductImageItem.FromPicked(new(name, 1, [1])));
    [Fact]
    public async Task SaveAsync_DeleteReturns404_ForgetsIdAndContinues()
    {
        // Arrange
        _gallery.RemovedImageIds.Add(Guid.NewGuid()); AddNew();
        _repository.DeleteError = new(ApiErrorKind.Http, "Gone", 404);
        // Act
        await new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Equal(new[] { "delete", "upload", "layout" }, _repository.Calls);
        Assert.Empty(_gallery.RemovedImageIds); Assert.False(_gallery.HasChanges);
    }
    [Fact]
    public async Task SaveAsync_DeleteReturns500_Throws()
    {
        // Arrange
        var id = Guid.NewGuid(); _gallery.RemovedImageIds.Add(id); AddNew();
        var expected = new ApiException(ApiErrorKind.Http, "Server error", 500);
        _repository.DeleteError = expected;
        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default));
        // Assert
        Assert.Same(expected, error); Assert.Equal(new[] { "delete" }, _repository.Calls);
        Assert.Equal(id, Assert.Single(_gallery.RemovedImageIds)); Assert.True(_gallery.Images[0].IsNew);
    }
    [Fact]
    public async Task SaveAsync_NoChanges_SendsNothing()
    {
        // Arrange / Act
        await new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Empty(_repository.Calls);
    }
    [Fact]
    public async Task SaveAsync_DeletesThenUploadsThenLayout()
    {
        // Arrange
        _gallery.Load([new(Guid.NewGuid(), null, "/old.png", "image/png", 1, 0)], []); _gallery.RemoveSelectedCommand.Execute(null); AddNew();
        // Act
        await new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Equal(new[] { "delete", "upload", "layout" }, _repository.Calls); Assert.False(_gallery.HasChanges);
        Assert.Equal(_gallery.Images[0].Id, Assert.Single(_repository.LastLayout!).ImageId);
    }
    [Fact]
    public async Task SaveAsync_VariantImage_SendsRowId()
    {
        // Arrange
        AddNew(); var row = new VariantRowItem(Guid.NewGuid()); _gallery.Images[0].Variant = row;
        // Act
        await new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Equal(row.Id, Assert.Single(_repository.Uploads).VariantId);
    }
    [Fact]
    public async Task SaveAsync_SecondUploadFails_FirstIsSavedSecondStaysNew()
    {
        // Arrange
        _gallery.RemovedImageIds.Add(Guid.NewGuid()); AddNew(); AddNew("b.png"); _repository.FailUploadNumber = 2;
        // Act
        await Assert.ThrowsAsync<ApiException>(() => new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default));
        // Assert
        Assert.False(_gallery.Images[0].IsNew); Assert.True(_gallery.Images[1].IsNew); Assert.Empty(_gallery.RemovedImageIds); Assert.True(_gallery.HasChanges);
    }
    [Fact]
    public async Task SaveAsync_Retry_SendsOnlyMissingUploadAndLayout()
    {
        // Arrange
        AddNew(); AddNew("b.png"); _repository.FailUploadNumber = 2; var saver = new ProductImageSaver(_repository);
        await Assert.ThrowsAsync<ApiException>(() => saver.SaveAsync(_id, _gallery, default)); _repository.Calls.Clear(); _repository.FailUploadNumber = null;
        // Act
        await saver.SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Equal(new[] { "upload", "layout" }, _repository.Calls); Assert.Equal("b.png", _repository.Uploads.Last().FileName); Assert.False(_gallery.HasChanges);
    }
    [Fact]
    public async Task SaveAsync_AllImagesRemoved_SkipsLayout()
    {
        // Arrange
        _gallery.Load([new(Guid.NewGuid(), null, "/old.png", "image/png", 1, 0)], []); _gallery.RemoveSelectedCommand.Execute(null);
        // Act
        await new ProductImageSaver(_repository).SaveAsync(_id, _gallery, default);
        // Assert
        Assert.Equal(new[] { "delete" }, _repository.Calls); Assert.False(_gallery.HasChanges);
    }
}
