using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Services;

namespace TechStore.Api.Identity;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddStoreAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (context.Principal is null || !AuthenticatedUser.TryRead(context.Principal, out var user)
                        || !await context.HttpContext.RequestServices.GetRequiredService<IAuthService>()
                            .ValidateIdentityAsync(user, context.HttpContext.RequestAborted))
                        context.Fail("Invalid session.");
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 401, Title = "Unauthorized", Detail = "Vui lòng đăng nhập lại."
                    }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
                },
                OnForbidden = context =>
                {
                    context.Response.StatusCode = 403;
                    return context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 403, Title = "Forbidden", Detail = "Bạn không có quyền thực hiện thao tác này."
                    }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().Build();
            options.FallbackPolicy = options.DefaultPolicy;
        });
        return services;
    }
}
