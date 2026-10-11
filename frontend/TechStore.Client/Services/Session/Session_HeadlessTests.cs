namespace TechStore.Client.Services.Session.Tests
{
    public static class HeadlessSessionTests
    {
        public static void Verify()
        {
            var provider = new CurrentSessionProvider();
            provider.SetPreview();
            var snapshot = provider.Current;
            
            if (snapshot.Status != SessionStatus.Preview) throw new System.Exception("Status must be Preview.");
            if (snapshot.DisplayName == "Admin") throw new System.Exception("Should not fabricate a fake Admin identity.");
            
            if (!provider.HasCapability("catalog:read")) throw new System.Exception("Preview should have catalog:read");
            if (!provider.HasCapability("inventory:read")) throw new System.Exception("Preview should have inventory:read");
            
            if (provider.HasCapability("pos:write")) throw new System.Exception("Should not have POS capabilities yet.");
            
            System.Console.WriteLine("HeadlessSessionTests PASS");
        }
    }
}
