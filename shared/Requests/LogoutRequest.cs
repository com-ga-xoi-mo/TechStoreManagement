namespace TechStore.Shared.Requests;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
