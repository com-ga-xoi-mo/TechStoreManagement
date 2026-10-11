namespace TechStore.Client.Services.Session.Tests
{
    public static class HeadlessSessionConfigTests
    {
        public static void Verify()
        {
            if (SessionConfig.CurrentMode == AuthMode.Real)
            {
                var provider = new CurrentSessionProvider();
                if (provider.Current.Status != SessionStatus.Unknown) 
                    throw new System.Exception("Real mode must start as Unknown");
                
                var nav = new TechStore.Client.Services.Navigation.NavigationService(provider);
                if (nav.GetAllowedMenuRoutes().Count > 0)
                    throw new System.Exception("Real mode Unknown session must have no allowed protected routes initially");
            }
            
            System.Console.WriteLine("HeadlessSessionConfigTests PASS");
        }
    }
}
