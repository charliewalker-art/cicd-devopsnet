using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using devopsnet.Models;
using Microsoft.IdentityModel.Tokens;

namespace devopsnet.Services;

public class TokenService
{
    private readonly string _secretKey;
    private readonly int _expirationMinutes;

    public TokenService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey manquant dans la configuration.");
        _expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}