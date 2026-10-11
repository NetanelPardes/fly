using Airport.Api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Airport.Api.Services;

public interface IJwtTokenService
{
    string CreateToken(User user);
    string CreateToken(Employee employee);
}

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(User user)
    {
        return CreateToken(user.Id, user.Email, user.Role);
    }

    public string CreateToken(Employee employee)
    {
        return CreateToken(employee.Id, employee.Email, UserRole.Admin);
    }

    private string CreateToken(int id, string email, UserRole role)
    {
        var key = configuration["JWT_KEY"] ?? "development-only-secret-key-change-me-please-32chars";
        var issuer = configuration["JWT_ISSUER"] ?? "AirportManagementSystem";
        var audience = configuration["JWT_AUDIENCE"] ?? "AirportManagementSystem";
        var expiresMinutes = int.TryParse(configuration["JWT_EXPIRES_MINUTES"], out var minutes) ? minutes : 480;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
