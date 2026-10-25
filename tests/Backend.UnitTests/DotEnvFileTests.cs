using FluentAssertions;
using TechStore.Api.Common.Configuration;

namespace TechStore.UnitTests;

public class DotEnvFileTests
{
    [Fact]
    public void Parse_CommentsQuotesAndExport_ReturnsPlainValues()
    {
        // Arrange
        string[] lines =
        [
            "# comment",
            "",
            "ObjectStorage__Provider='R2'",
            "export ObjectStorage__R2__BucketName = \"tech-store\"",
            "ObjectStorage__R2__PublicBaseUrl=https://pub-1.r2.dev/a=b",
            "no separator",
            "=no key"
        ];
        // Act
        var values = DotEnvFile.Parse(lines);
        // Assert
        values.Should().Equal(
            new KeyValuePair<string, string>("ObjectStorage__Provider", "R2"),
            new KeyValuePair<string, string>("ObjectStorage__R2__BucketName", "tech-store"),
            new KeyValuePair<string, string>("ObjectStorage__R2__PublicBaseUrl", "https://pub-1.r2.dev/a=b"));
    }

    [Fact]
    public void LoadIfPresent_VariableAlreadySet_KeepsExistingValue()
    {
        // Arrange
        var setKey = "TECHSTORE_DOTENV_TEST_" + Guid.NewGuid().ToString("N");
        var newKey = "TECHSTORE_DOTENV_TEST_" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".env");
        File.WriteAllLines(path, [$"{setKey}=from-file", $"{newKey}=from-file"]);
        Environment.SetEnvironmentVariable(setKey, "from-shell");
        try
        {
            // Act
            DotEnvFile.LoadIfPresent(path);
            // Assert
            Environment.GetEnvironmentVariable(setKey).Should().Be("from-shell");
            Environment.GetEnvironmentVariable(newKey).Should().Be("from-file");
        }
        finally
        {
            Environment.SetEnvironmentVariable(setKey, null);
            Environment.SetEnvironmentVariable(newKey, null);
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadIfPresent_MissingFile_DoesNothing()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".env");
        // Act
        var act = () => DotEnvFile.LoadIfPresent(path);
        // Assert
        act.Should().NotThrow();
    }
}
