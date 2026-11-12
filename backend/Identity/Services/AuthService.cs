using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public async Task<LoginResponseDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password)) return null;
        var found = await userRepository.GetByNormalizedUsernameAsync(request.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (found is null) return null;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetForUpdateAsync(found.Id, cancellationToken);
            if (user is not { IsActive: true } || AuthenticatedUser.SoleRole(user) is null
                || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return null;
            }
            var response = IssueTokens(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<LoginResponseDto?> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return null;
        var hash = HashToken(request.RefreshToken);
        var found = await refreshTokenRepository.FindByHashAsync(hash, cancellationToken);
        if (found is null) return null;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetForUpdateAsync(found.UserId, cancellationToken);
            var token = await refreshTokenRepository.GetForUpdateAsync(hash, cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (user is not { IsActive: true } || AuthenticatedUser.SoleRole(user) is null
                || token is null || token.UserId != user.Id || token.IsRevoked || token.ExpiresAt <= now)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return null;
            }
            token.IsRevoked = true;
            token.UpdatedAt = now;
            var response = IssueTokens(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> ValidateIdentityAsync(AuthenticatedUser identity, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdWithRolesAsync(identity.Id, cancellationToken);
        return user is { IsActive: true } && user.TokenVersion == identity.TokenVersion
            && AuthenticatedUser.SoleRole(user) == identity.Role;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdWithRolesAsync(userId, cancellationToken);
        return user is { IsActive: true } && AuthenticatedUser.SoleRole(user) is not null ? Profile(user) : null;
    }

    public async Task<bool> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetForUpdateAsync(userId, cancellationToken);
            if (user is null)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return false;
            }
            user.TokenVersion = checked(user.TokenVersion + 1);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            user.UpdatedAt = now;
            foreach (var token in await refreshTokenRepository.GetActiveTokensByUserIdAsync(userId, cancellationToken))
            {
                token.IsRevoked = true;
                token.UpdatedAt = now;
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private LoginResponseDto IssueTokens(User user)
    {
        var options = jwtOptions.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, AuthenticatedUser.SoleRole(user)!.Value.ToString()),
                new Claim("token_version", user.TokenVersion.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            NotBefore = now, IssuedAt = now, Expires = now.AddMinutes(options.ExpiryMinutes),
            Issuer = options.Issuer, Audience = options.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)), SecurityAlgorithms.HmacSha256)
        });
        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        refreshTokenRepository.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = HashToken(rawRefresh),
            ExpiresAt = now.AddDays(options.RefreshTokenExpiryDays), CreatedAt = now
        });
        return new LoginResponseDto
        {
            AccessToken = handler.WriteToken(token), RefreshToken = rawRefresh,
            TokenType = "Bearer", ExpiresIn = options.ExpiryMinutes * 60, User = Profile(user)
        };
    }

    private static UserProfileDto Profile(User user) => new()
    {
        Id = user.Id, Username = user.Username, FullName = user.FullName, Email = user.Email,
        Role = AuthenticatedUser.SoleRole(user)!.Value, IsActive = user.IsActive, LastLoginAt = user.UpdatedAt
    };

    private static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
