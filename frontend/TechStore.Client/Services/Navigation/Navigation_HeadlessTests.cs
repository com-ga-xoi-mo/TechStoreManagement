namespace TechStore.Client.Services.Navigation.Tests
{
    public class MockFrameAdapter : IFrameAdapter
    {
        public bool NavigateToView(RouteId routeId) => true;
        public bool GoBack() => true;
        public bool CanGoBack => true;
    }

    public static class HeadlessNavigationTests
    {
        public static void Verify()
        {
            var session = new TechStore.Client.Services.Session.CurrentSessionProvider();
            session.SetPreview();
            var nav = new NavigationService(session);
            nav.SetFrameAdapter(new MockFrameAdapter());
            
            // Initial Dashboard
            if (nav.CurrentRoute != RouteId.Dashboard) throw new System.Exception("Initial route should be Dashboard");
            
            // Navigate
            var res = nav.NavigateTo(RouteId.Catalog);
            if (!res || nav.CurrentRoute != RouteId.Catalog) throw new System.Exception("Navigation failed");
            
            // No-op reselect
            nav.NavigateTo(RouteId.Catalog);
            
            // Back
            nav.GoBack();
            if (nav.CurrentRoute != RouteId.Dashboard) throw new System.Exception("Back failed");
            
            // Root back rejection
            var backRes = nav.GoBack();
            if (backRes) throw new System.Exception("Should not go back past root");
        }
    }
}
