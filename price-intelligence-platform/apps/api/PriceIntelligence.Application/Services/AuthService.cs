using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.DTOs.Auth;
using PriceIntelligence.Application.Interfaces;
using PriceIntelligence.Domain.Entities;
using PriceIntelligence.Domain.Enums;

namespace PriceIntelligence.Application.Services;

public class AuthService(IUserRepository userRepo, IConfiguration config) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await userRepo.GetByEmailAsync(request.Email) is not null)
            throw new ServiceException("Email is already registered.", 409);

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.User
        };

        await userRepo.CreateAsync(user);
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByEmailAsync(request.Email.ToLowerInvariant())
            ?? throw new ServiceException("Invalid email or password.", 401);

        if (!user.IsActive)
            throw new ServiceException("Account is disabled.", 403);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ServiceException("Invalid email or password.", 401);

        return BuildAuthResponse(user);
    }

    public Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        throw new ServiceException("Refresh token not yet implemented.", 501);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key missing");
        var expiry = int.Parse(config["Jwt:ExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var expiresAt = DateTime.UtcNow.AddMinutes(expiry);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new AuthResponse(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt: expiresAt,
            User: new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.CreatedAt)
        );
    }
}