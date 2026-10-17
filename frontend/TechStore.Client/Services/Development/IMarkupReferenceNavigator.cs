namespace TechStore.Client.Services.Development;

/// <summary>
/// Opens and closes the C# Markup reference specimen from the component gallery.
/// Registered only when <see cref="GalleryOptions.Enabled"/> is true, so a normal launch has no entry point.
/// </summary>
public interface IMarkupReferenceNavigator
{
    /// <summary>Shows the reference specimen in the host window.</summary>
    void ShowReference();

    /// <summary>Returns the host window to the component gallery.</summary>
    void ShowGallery();
}
