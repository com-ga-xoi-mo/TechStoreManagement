namespace TechStore.Client.Services.Session.Tests
{
    using TechStore.Client.Models.Identity;
    using System.Collections.Generic;

    public static class HeadlessSessionCoordinatorTests
    {
        public static void Verify()
        {
            var provider = new CurrentSessionProvider();
            
            // 1. Initial State
            if (provider.Current.Status != SessionStatus.Unknown) throw new System.Exception("Initial should be Unknown");
            if (provider.HasCapability("catalog:read")) throw new System.Exception("Unknown should fail closed on capabilities");

            // 2. Set Preview
            provider.SetPreview();
            if (provider.Current.Status != SessionStatus.Preview) throw new System.Exception("Should transition to Preview");
            if (!provider.HasCapability("catalog:read")) throw new System.Exception("Preview should have catalog capability");

            // 3. Set Authenticated
            var identity = new SessionSnapshot("Real Admin", new HashSet<string> { "orders:write" });
            provider.SetAuthenticated(identity);
            if (provider.Current.Status != SessionStatus.Authenticated) throw new System.Exception("Should transition to Authenticated");
            if (provider.Current.DisplayName != "Real Admin") throw new System.Exception("Identity mismatch");
            if (!provider.HasCapability("orders:write")) throw new System.Exception("Should have injected capability");
            if (provider.HasCapability("catalog:read")) throw new System.Exception("Should drop old capabilities not in new snapshot");

            // 4. Clear/Expiry
            provider.ClearSession();
            if (provider.Current.Status != SessionStatus.Unknown) throw new System.Exception("Should return to Unknown on clear");
            if (provider.HasCapability("orders:write")) throw new System.Exception("Should drop capabilities on clear");
        }
    }
}
