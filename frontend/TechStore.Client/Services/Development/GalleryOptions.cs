using Microsoft.Extensions.Configuration;
namespace TechStore.Client.Services.Development;

public sealed record GalleryOptions(bool Enabled)
{
    public static GalleryOptions FromConfiguration(IConfiguration configuration) =>
        Parse(configuration["TECHSTORE_UI_GALLERY"]);

    public static GalleryOptions Parse(string? value) => value switch
    {
        null or "0" => new(false),
        "1" => new(true),
        _ => throw new ArgumentException("TECHSTORE_UI_GALLERY must be 0 or 1.")
    };
}
