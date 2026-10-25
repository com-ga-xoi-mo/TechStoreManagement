using System.Collections.ObjectModel;
using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductImageGalleryTests
{
    private readonly ScriptedImageFilePicker _picker = new();
    private readonly ObservableCollection<VariantRowItem> _rows = [];
    private ProductImageGallery Create() => new(_picker, _rows, () => default);
    private static ProductImage Saved(int order = 0, Guid? variant = null) => new(Guid.NewGuid(), variant, $"/uploads/{order}.png", "image/png", 100, order);
    private async Task Add(ProductImageGallery gallery, int count)
    {
        _picker.Results.Enqueue(Enumerable.Range(0, count).Select(i => new TechStore.Client.Services.Files.PickedImageFile($"{i}.png", 1, [1])).ToArray());
        await gallery.AddImagesCommand.ExecuteAsync(null);
    }
    [Fact]
    public void NewGallery_NoImages_ShowsEmptyState()
    {
        // Arrange / Act
        var gallery = Create();
        // Assert
        Assert.Equal("0/20", gallery.CountText); Assert.True(gallery.CanAddMore); Assert.Null(gallery.LargePreview);
    }
    [Fact]
    public async Task SelectThumbnail_SecondImage_UpdatesPreviewAndSelection()
    {
        // Arrange
        var gallery = Create(); await Add(gallery, 3);
        // Act
        gallery.SelectImageCommand.Execute(gallery.Images[1]);
        // Assert
        Assert.Same(gallery.Images[1], gallery.SelectedImage); Assert.True(gallery.Images[1].IsSelected);
        Assert.Single(gallery.Images, i => i.IsSelected); Assert.Same(gallery.Images[1].PendingContent, gallery.LargePreview!.Bytes);
    }
    [Fact]
    public async Task AddImages_ThreeFiles_AppendsAndMarksNew()
    {
        // Arrange
        var gallery = Create();
        // Act
        await Add(gallery, 3);
        // Assert
        Assert.Equal("3/20", gallery.CountText); Assert.All(gallery.Images, i => Assert.True(i.IsNew)); Assert.Same(gallery.Images[0], gallery.SelectedImage);
    }
    [Fact]
    public async Task AddImages_BadFiles_ReportsNames()
    {
        // Arrange
        var gallery = Create(); _picker.Results.Enqueue([new("big.jpg", 6_000_000, null), new("photo.heic", 1, [1])]);
        // Act
        await gallery.AddImagesCommand.ExecuteAsync(null);
        // Assert
        Assert.Empty(gallery.Images); Assert.Contains("big.jpg lớn hơn 5 MB", gallery.PickMessage); Assert.Contains("photo.heic", gallery.PickMessage);
    }
    [Fact]
    public async Task AddImages_OverLimit_AddsTwoAndHidesButton()
    {
        // Arrange
        var gallery = Create(); gallery.Load(Enumerable.Range(0, 18).Select(i => Saved(i)).ToArray(), []);
        // Act
        await Add(gallery, 4);
        // Assert
        Assert.Equal(20, gallery.Images.Count); Assert.False(gallery.CanAddMore); Assert.Contains("Chỉ thêm được 2", gallery.PickMessage);
    }
    [Fact]
    public async Task PickerCancelled_KeepsImagesAndMessage()
    {
        // Arrange
        var gallery = Create(); await Add(gallery, 1); gallery.PickMessage = "previous";
        // Act
        await gallery.AddImagesCommand.ExecuteAsync(null);
        // Assert
        Assert.Single(gallery.Images); Assert.Equal("previous", gallery.PickMessage);
    }
    [Fact]
    public async Task MakeCover_ThirdSharedImage_MovesToFront()
    {
        // Arrange
        var gallery = Create(); await Add(gallery, 3); var item = gallery.Images[2]; gallery.SelectedImage = item;
        // Act
        gallery.MakeCoverCommand.Execute(null);
        // Assert
        Assert.Same(item, gallery.Images[0]); Assert.True(item.IsCover); Assert.False(gallery.ShowMakeCover);
    }
    [Fact]
    public async Task VariantImage_HidesMakeCover()
    {
        // Arrange
        var row = new VariantRowItem(null); _rows.Add(row); var gallery = Create(); await Add(gallery, 1);
        // Act
        gallery.SelectedVariantChoice = gallery.VariantChoices[1];
        // Assert
        Assert.False(gallery.ShowMakeCover); Assert.False(gallery.MakeCoverCommand.CanExecute(null));
    }
    [Fact]
    public async Task MoveRight_KeepsSelectionAndUpdatesBoundaryCommands()
    {
        // Arrange
        var gallery = Create(); await Add(gallery, 3); var item = gallery.SelectedImage;
        // Act
        gallery.MoveRightCommand.Execute(null);
        // Assert
        Assert.Same(item, gallery.Images[1]); Assert.Same(item, gallery.SelectedImage); Assert.True(gallery.CanMoveLeft);
        gallery.MoveLeftCommand.Execute(null); Assert.False(gallery.CanMoveLeft);
    }
    [Fact]
    public async Task RemoveLast_SelectsPrevious()
    {
        // Arrange
        var gallery = Create(); await Add(gallery, 3); gallery.SelectedImage = gallery.Images[2];
        // Act
        gallery.RemoveSelectedCommand.Execute(null);
        // Assert
        Assert.Equal(2, gallery.Images.Count); Assert.Same(gallery.Images[1], gallery.SelectedImage);
    }
    [Fact]
    public async Task TagImage_UpdatesLabelAndRowCount()
    {
        // Arrange
        var row = new VariantRowItem(null) { VariantName = "Đen" }; _rows.Add(row); var gallery = Create(); await Add(gallery, 2);
        // Act
        foreach (var image in gallery.Images) image.Variant = row;
        // Assert
        Assert.All(gallery.Images, i => Assert.Equal("Đen", i.VariantLabel)); Assert.Equal("2 ảnh", row.ImageCountText);
    }
    [Fact]
    public async Task RemoveUnsavedRow_ReturnsImagesToShared()
    {
        // Arrange
        var row = new VariantRowItem(null); _rows.Add(row); var gallery = Create(); await Add(gallery, 1); gallery.SelectedImage!.Variant = row;
        // Act
        _rows.Remove(row);
        // Assert
        Assert.Null(gallery.SelectedImage.Variant); Assert.Equal(0, row.ImageCount);
    }
    [Fact]
    public void HasChanges_AfterLoad_IsFalse()
    {
        // Arrange / Act
        var gallery = Create(); gallery.Load([Saved()], []);
        // Assert
        Assert.False(gallery.HasChanges);
    }
    [Fact]
    public void HasChanges_AfterReorder_IsTrue()
    {
        // Arrange
        var gallery = Create(); gallery.Load([Saved(), Saved(1)], []);
        // Act
        gallery.MoveRightCommand.Execute(null);
        // Assert
        Assert.True(gallery.HasChanges);
    }
    [Fact]
    public void HasChanges_AfterVariantChange_IsTrue()
    {
        // Arrange
        var row = new VariantRowItem(Guid.NewGuid()); _rows.Add(row); var gallery = Create(); gallery.Load([Saved()], _rows);
        // Act
        gallery.SelectedImage!.Variant = row;
        // Assert
        Assert.True(gallery.HasChanges);
    }
    [Fact]
    public void AcceptChanges_ResetsHasChanges()
    {
        // Arrange
        var gallery = Create(); gallery.Load([Saved(), Saved(1)], []); gallery.MoveRightCommand.Execute(null);
        // Act
        gallery.AcceptChanges();
        // Assert
        Assert.False(gallery.HasChanges);
    }
    [Fact]
    public async Task PickerThrows_ShowsMessage()
    {
        // Arrange
        _picker.Error = new InvalidOperationException("picker failed"); var gallery = Create();
        // Act
        await gallery.AddImagesCommand.ExecuteAsync(null);
        // Assert
        Assert.Equal("picker failed", gallery.PickMessage);
    }
    [Fact]
    public async Task RenameRow_KeepsSelectedImageVariant()
    {
        // Arrange
        var row = new VariantRowItem(null) { VariantName = "Đen" }; _rows.Add(row); var gallery = Create(); await Add(gallery, 1);
        gallery.SelectedVariantChoice = gallery.VariantChoices[1];
        // Act
        row.VariantName = "Đen mới"; gallery.SelectedVariantChoice = null;
        // Assert
        Assert.Same(row, gallery.SelectedImage!.Variant); Assert.Equal("Đen mới", gallery.SelectedVariantChoice!.Label); Assert.Equal("Đen mới", gallery.SelectedImage.VariantLabel);
    }
    [Fact]
    public async Task VariantChoiceNull_IsIgnored()
    {
        // Arrange
        var row = new VariantRowItem(null); _rows.Add(row); var gallery = Create(); await Add(gallery, 1); gallery.SelectedVariantChoice = gallery.VariantChoices[1];
        // Act
        gallery.SelectedVariantChoice = null;
        // Assert
        Assert.Same(row, gallery.SelectedImage!.Variant);
    }
}
