using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace TechStore.Api.Common.Storage;

public static class ObjectStorageServiceCollectionExtensions
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>() ?? new();
        var names = options.Validate();
        if (names.Count > 0)
            throw new InvalidOperationException($"Missing or invalid object storage settings: {string.Join(", ", names)}");
        services.AddSingleton(options);
        services.AddSingleton<IObjectStorage>(provider =>
            string.Equals(options.Provider, "R2", StringComparison.OrdinalIgnoreCase)
                ? new R2ObjectStorage(options, provider.GetRequiredService<ILogger<R2ObjectStorage>>())
                : new LocalObjectStorage(options, provider.GetRequiredService<IHostEnvironment>().ContentRootPath,
                    provider.GetRequiredService<ILogger<LocalObjectStorage>>()));
        return services;
    }

    public static void UseObjectStorageFiles(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<ObjectStorageOptions>();
        if (!string.Equals(options.Provider, "Local", StringComparison.OrdinalIgnoreCase)) return;
        var root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, options.Local.RootPath));
        Directory.CreateDirectory(root);
        var files = new PhysicalFileProvider(root);
        app.Lifetime.ApplicationStopped.Register(files.Dispose);
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webp"] = "image/webp";
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            RequestPath = options.Local.RequestPath,
            ContentTypeProvider = types,
            OnPrepareResponse = context => context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
        });
    }
}
