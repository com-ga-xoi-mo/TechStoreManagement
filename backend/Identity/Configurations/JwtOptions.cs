namespace TechStore.Api.Identity.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "TechStore_Default_Secret_Key_At_Least_32_Bytes_Long_2026!";
    public string Issuer { get; set; } = "TechStore.Api";
    public string Audience { get; set; } = "TechStore.Client";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
