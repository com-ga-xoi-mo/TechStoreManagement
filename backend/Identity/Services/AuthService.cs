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
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _passwordHasher = new PasswordHasher<User>();
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByNormalizedUsernameAsync(normalizedUsername, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Get primary role
        var roleType = user.UserRoles.FirstOrDefault()?.Role?.Name ?? RoleType.SalesStaff;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Generate Access Token (JWT)
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, roleType.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            NotBefore = now,
            IssuedAt = now,
            Expires = now.AddMinutes(_jwtOptions.ExpiryMinutes),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        // Generate Refresh Token
        var rawRefreshToken = GenerateSecureToken();
        var refreshTokenHash = HashToken(rawRefreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedAt = now
        };

        _refreshTokenRepository.Add(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtOptions.ExpiryMinutes * 60,
            User = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = string.Empty,
                Role = roleType,
                IsActive = user.IsActive,
                LastLoginAt = user.UpdatedAt
            }
        };
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var roleType = user.UserRoles.FirstOrDefault()?.Role?.Name ?? RoleType.SalesStaff;

        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = string.Empty,
            Role = roleType,
            IsActive = user.IsActive,
            LastLoginAt = user.UpdatedAt
        };
    }

    public async Task<bool> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenHash = HashToken(request.RefreshToken);
            var token = await _refreshTokenRepository.GetActiveTokenAsync(userId, tokenHash, cancellationToken);

            if (token != null)
            {
                token.IsRevoked = true;
                token.UpdatedAt = now;
            }
        }
        else
        {
            // If no specific refresh token provided, revoke all active refresh tokens for this user
            var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(userId, cancellationToken);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
