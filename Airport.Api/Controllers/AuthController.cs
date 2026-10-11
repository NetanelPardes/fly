using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AirportDbContext dbContext, IPasswordHasher<User> passwordHasher, IJwtTokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is not null)
        {
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return Unauthorized();
            }

            return Ok(new AuthResponseDto(tokenService.CreateToken(user), ToDto(user)));
        }

        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        var employeeUser = ToUserDto(employee);
        var employeeResult = passwordHasher.VerifyHashedPassword(employeeUser, employee.PasswordHash, request.Password);
        if (employeeResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(new AuthResponseDto(tokenService.CreateToken(employee), ToDto(employee)));
    }

    [HttpPost("register-passenger")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> RegisterPassenger(RegisterPassengerDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        var identity = NormalizeIdentity(request.FullName, request.FirstName, request.LastName);
        var nationalId = string.IsNullOrWhiteSpace(request.NationalId) ? GenerateNineDigitValue() : request.NationalId.Trim();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? GeneratePhoneValue() : request.PhoneNumber.Trim();

        if (await dbContext.Users.AnyAsync(user => user.NationalId == nationalId, cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(user => user.PhoneNumber == phone, cancellationToken))
        {
            return Conflict("Phone number already exists.");
        }

        var user = new User
        {
            NationalId = nationalId,
            FirstName = identity.FirstName,
            LastName = identity.LastName,
            FullName = identity.FullName,
            Email = email,
            Role = UserRole.Passenger,
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? "m" : request.Gender.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? "Registered online" : request.Address.Trim(),
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Login), new AuthResponseDto(tokenService.CreateToken(user), ToDto(user)));
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.NationalId,
        user.FirstName,
        user.LastName,
        user.FullName,
        user.Email,
        user.Role,
        user.Gender,
        user.Address,
        user.PhoneNumber);

    private static User ToUserDto(Employee employee) => new()
    {
        Id = employee.Id,
        NationalId = employee.NationalId,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        FullName = employee.FullName,
        Email = employee.Email,
        PasswordHash = employee.PasswordHash,
        Role = UserRole.Admin,
        Gender = employee.Gender,
        Address = employee.Address,
        PhoneNumber = employee.PhoneNumber,
        CreatedAt = employee.CreatedAt
    };

    private static UserDto ToDto(Employee employee) => new(
        employee.Id,
        employee.NationalId,
        employee.FirstName,
        employee.LastName,
        employee.FullName,
        employee.Email,
        UserRole.Admin,
        employee.Gender,
        employee.Address,
        employee.PhoneNumber);

    private static (string FirstName, string LastName, string FullName) NormalizeIdentity(
        string fullName,
        string? firstName,
        string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            var first = firstName.Trim();
            var last = lastName.Trim();
            return (first, last, $"{first} {last}");
        }

        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resolvedFirst = parts.ElementAtOrDefault(0) ?? fullName.Trim();
        var resolvedLast = parts.ElementAtOrDefault(1) ?? "Passenger";
        return (resolvedFirst, resolvedLast, $"{resolvedFirst} {resolvedLast}");
    }

    private static string GenerateNineDigitValue() => Random.Shared.Next(100000000, 999999999).ToString();

    private static string GeneratePhoneValue() => $"05{Random.Shared.Next(10000000, 99999999)}";
}
