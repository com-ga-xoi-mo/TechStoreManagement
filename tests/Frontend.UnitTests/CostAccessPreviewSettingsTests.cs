using TechStore.Client.Services.Session;
using Xunit;

namespace TechStore.Client.Tests.Session;

public sealed class CostAccessPreviewSettingsTests
{
    [Fact]
    public void PreviewMode_OnlyExplicitAuthorizedValueGrantsCostCapability()
    {
        var original = Environment.GetEnvironmentVariable(CostAccessPreviewSettings.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(CostAccessPreviewSettings.EnvironmentVariable, "authorized");
            var authorized = new CurrentSessionProvider();
            authorized.SetPreview();

            Assert.Equal(SessionStatus.Preview, authorized.Current.Status);
            Assert.True(authorized.HasCapability("catalog:cost-read"));

            Environment.SetEnvironmentVariable(CostAccessPreviewSettings.EnvironmentVariable, "denied");
            var denied = new CurrentSessionProvider();
            denied.SetPreview();

            Assert.Equal(SessionStatus.Preview, denied.Current.Status);
            Assert.False(denied.HasCapability("catalog:cost-read"));
            Assert.Equal(CostAccessPreviewMode.Denied, CostAccessPreviewSettings.Parse("anything-else"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(CostAccessPreviewSettings.EnvironmentVariable, original);
        }
    }
}
