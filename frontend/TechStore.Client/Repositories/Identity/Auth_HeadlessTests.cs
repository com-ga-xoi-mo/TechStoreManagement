namespace TechStore.Client.Repositories.Identity.Tests
{
    using TechStore.Client.Models.Identity;
    using System.Threading.Tasks;

    public static class HeadlessAuthTests
    {
        public static async Task VerifyAsync()
        {
            var repo = new FakeAuthRepository();
            
            // 1. Success
            var res1 = await repo.LoginAsync("admin", "Admin@123");
            if (res1.Status != LoginResultStatus.Success || res1.Session == null) throw new System.Exception("Admin login should succeed");
            if (res1.Session.DisplayName != "Administrator") throw new System.Exception("Wrong display name");

            // 2. Wrong Credentials
            var res2 = await repo.LoginAsync("admin", "wrong");
            if (res2.Status != LoginResultStatus.WrongCredentials) throw new System.Exception("Should be wrong credentials");

            // 3. Locked
            var res3 = await repo.LoginAsync("locked", "any");
            if (res3.Status != LoginResultStatus.LockedOut) throw new System.Exception("Should be locked");

            // 4. Inactive
            var res4 = await repo.LoginAsync("inactive", "any");
            if (res4.Status != LoginResultStatus.Inactive) throw new System.Exception("Should be inactive");

            // 5. Network Error
            var res5 = await repo.LoginAsync("network", "any");
            if (res5.Status != LoginResultStatus.NetworkError) throw new System.Exception("Should be network error");
            
            System.Console.WriteLine("HeadlessAuthTests PASS");
        }
    }
}
