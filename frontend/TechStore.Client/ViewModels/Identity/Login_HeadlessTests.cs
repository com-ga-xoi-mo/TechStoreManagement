namespace TechStore.Client.ViewModels.Identity.Tests
{
    using TechStore.Client.Repositories.Identity;
    using TechStore.Client.Services.Session;
    using TechStore.Client.Services.Navigation;
    using System.Threading;

    public static class HeadlessLoginTests
    {
        public static async System.Threading.Tasks.Task VerifyAsync()
        {
            var repo = new FakeAuthRepository();
            var session = new CurrentSessionProvider();
            var vm = new LoginViewModel(repo, session, new FakeRootNavigation(), new AuthModeProvider(SessionConfig.CurrentMode));

            // 1. Invalid input -> no request
            await vm.LoginCommand.ExecuteAsync(null);
            if (session.Current.Status != SessionStatus.Unknown) throw new System.Exception("Should not authenticate with empty fields");

            // 2. Reject -> Username preserved, Password cleared
            vm.Username = "admin";
            vm.Password = "wrong";
            await vm.LoginCommand.ExecuteAsync(null);
            if (vm.Username != "admin" || vm.Password != string.Empty) throw new System.Exception("Should preserve username and clear password on fail");
            if (string.IsNullOrEmpty(vm.ErrorMessage)) throw new System.Exception("Should show error message");

            // 3. Success (Simulating ExplicitPreview behavior by observing the VM logic or just validating repo success mapping)
            vm.Username = "admin";
            vm.Password = "Admin@123";
            await vm.LoginCommand.ExecuteAsync(null);
            
            if (SessionConfig.CurrentMode == AuthMode.ExplicitPreview)
            {
                 if (session.Current.Status != SessionStatus.Preview) throw new System.Exception("Should set Preview");
            }
            else
            {
                 if (session.Current.Status != SessionStatus.Unknown) throw new System.Exception("Normal mode must not authenticate");
            }
            
            System.Console.WriteLine("HeadlessLoginTests PASS");
        }

        private sealed class FakeRootNavigation : IRootNavigationService
        {
            public void ShowShell() { }
            public void ShowLogin() { }
        }
    }
}
