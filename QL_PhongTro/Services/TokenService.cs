using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Services;

public class TokenService(JwtSettings jwtSettings, ITimeProvider timeProvider, TokenBlacklistService blacklistService, SessionVersionStore sessions)
{
    private readonly JwtSettings _jwtSettings = jwtSettings;
    private readonly ITimeProvider _timeProvider = timeProvider;
    private readonly TokenBlacklistService _blacklistService = blacklistService;

    public string GenerateAccessToken(TaiKhoan user)
    {
        var version = sessions.Capture(user.Id, user.MatKhau)
            ?? throw new InvalidOperationException("Credentials changed during token creation.");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.HoTen),
            new Claim(ClaimTypes.Role, user.VaiTro ?? "KHACH_THUE"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(SessionVersionStore.ClaimType, version)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = _timeProvider.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: now.AddMinutes(_jwtSettings.AccessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            
            if (!string.IsNullOrEmpty(jti) && _blacklistService.IsBlacklisted(jti))
            {
                return null;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var handler2 = new JwtSecurityTokenHandler();
            var principal = handler2.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            }, out _);
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ||
                !sessions.IsValid(accountId, principal.FindFirstValue(SessionVersionStore.ClaimType))) return null;
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public void BlacklistAccessToken(string token, TimeSpan expiration)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        
        if (!string.IsNullOrEmpty(jti))
        {
            _blacklistService.Blacklist(jti, expiration);
        }
    }
}
