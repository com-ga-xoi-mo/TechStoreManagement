using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TechStore.Api.Common.Storage;

namespace TechStore.UnitTests;

public class ObjectStorageTests
{
    [Fact]
    public void Validate_DefaultLocalOptions_ReturnsNoErrors()
    {
        // Arrange
        var options = new ObjectStorageOptions();
        // Act
        var errors = options.Validate();
        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnknownProvider_ReturnsProviderError()
    {
        // Arrange
        var options = new ObjectStorageOptions { Provider = "Other" };
        // Act
        var errors = options.Validate();
        // Assert
        errors.Should().Equal("ObjectStorage:Provider");
    }

    [Fact]
    public void Validate_R2WithoutSecret_ReturnsOnlyThatSettingName()
    {
        // Arrange
        var options = R2Options();
        // Act
        var errors = options.Validate();
        // Assert
        errors.Should().Equal("ObjectStorage:R2:SecretAccessKey");
        string.Join(", ", errors).Should().NotContain(options.R2.AccessKeyId);
    }

    [Theory]
    [InlineData("../a.jpg")]
    [InlineData("products/../x.png")]
    [InlineData("Products/A.jpg")]
    [InlineData("a.gif")]
    [InlineData("")]
    [InlineData("a.jpg\n")]
    public void EnsureValid_TraversalKey_Throws(string key)
    {
        // Arrange
        Action act = () => ObjectStorageKey.EnsureValid(key);
        // Act / Assert
        act.Should().Throw<ArgumentException>().WithMessage("Invalid storage key.*");
    }

    [Fact]
    public void EnsureValid_ServerKey_DoesNotThrow()
    {
        // Arrange
        Action act = () => ObjectStorageKey.EnsureValid("products/0f8fad5bd9cb469fa16570867728950e/7c9e6679742540de944be07fc1f90ae7.webp");
        // Act / Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void PublicUrlFor_TrailingSlash_JoinsOnce()
    {
        // Arrange
        const string key = "products/a.png";
        // Act
        var url = R2ObjectStorage.PublicUrlFor("https://img.test/", key);
        // Assert
        url.Should().Be("https://img.test/products/a.png");
    }

    [Fact]
    public async Task LocalPutAsync_WritesFileAndReturnsRelativeUrl()
    {
        // Arrange
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var storage = new LocalObjectStorage(new(), root.FullName, NullLogger<LocalObjectStorage>.Instance);
            byte[] bytes = [1, 2, 3];
            // Act
            var url = await storage.PutAsync("products/a.png", bytes, "image/png");
            // Assert
            url.Should().Be("/uploads/products/a.png");
            (await File.ReadAllBytesAsync(Path.Combine(root.FullName, "uploads/products/a.png"))).Should().Equal(bytes);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task LocalTryDeleteAsync_MissingFile_ReturnsTrue()
    {
        // Arrange
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var storage = new LocalObjectStorage(new(), root.FullName, NullLogger<LocalObjectStorage>.Instance);
            // Act
            var deleted = await storage.TryDeleteAsync("products/missing.png");
            // Assert
            deleted.Should().BeTrue();
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void AddObjectStorage_R2MissingSecret_ThrowsNamingTheSetting()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ObjectStorage:Provider"] = "R2",
            ["ObjectStorage:R2:AccountId"] = "account",
            ["ObjectStorage:R2:AccessKeyId"] = "AKID-VALUE",
            ["ObjectStorage:R2:BucketName"] = "bucket",
            ["ObjectStorage:R2:PublicBaseUrl"] = "https://img.test"
        }).Build();
        var services = new ServiceCollection();
        // Act
        Action act = () => services.AddObjectStorage(configuration);
        // Assert
        var error = act.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain("ObjectStorage:R2:SecretAccessKey").And.NotContain("AKID-VALUE");
    }

    [Fact]
    public void AddObjectStorage_NoSection_RegistersLocalStorage()
    {
        // Arrange
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(new StorageHostEnvironment { ContentRootPath = root.FullName });
            services.AddSingleton<ILogger<LocalObjectStorage>>(NullLogger<LocalObjectStorage>.Instance);
            services.AddObjectStorage(new ConfigurationBuilder().Build());
            using var provider = services.BuildServiceProvider();
            // Act
            var storage = provider.GetRequiredService<IObjectStorage>();
            // Assert
            storage.Should().BeOfType<LocalObjectStorage>();
            provider.GetRequiredService<IObjectStorage>().Should().BeSameAs(storage);
        }
        finally { root.Delete(recursive: true); }
    }

    private static ObjectStorageOptions R2Options() => new()
    {
        Provider = "R2",
        R2 = new() { AccountId = "account", AccessKeyId = "AKID-VALUE", BucketName = "bucket", PublicBaseUrl = "https://img.test" }
    };

    private sealed class StorageHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "StorageTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
