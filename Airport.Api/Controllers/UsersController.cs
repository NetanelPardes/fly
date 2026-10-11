using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(AirportDbContext dbContext, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => ToDto(user))
            .ToListAsync(cancellationToken);

        var employees = await dbContext.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.FullName)
            .Select(employee => ToDto(employee))
            .ToListAsync(cancellationToken);

        return Ok(users.Concat(employees).OrderBy(user => user.FullName));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto request, CancellationToken cancellationToken)
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
            Role = request.Role,
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? "m" : request.Gender.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? "Created by admin" : request.Address.Trim(),
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ToDto(user));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, UpdateUserDto request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.NationalId == request.NationalId.Trim(), cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken))
        {
            return Conflict("Phone number already exists.");
        }

        user.NationalId = request.NationalId.Trim();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = request.Role;
        user.Gender = request.Gender.Trim();
        user.Address = request.Address.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(user));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
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
        var resolvedLast = parts.ElementAtOrDefault(1) ?? "User";
        return (resolvedFirst, resolvedLast, $"{resolvedFirst} {resolvedLast}");
    }

    private static string GenerateNineDigitValue() => Random.Shared.Next(100000000, 999999999).ToString();

    private static string GeneratePhoneValue() => $"05{Random.Shared.Next(10000000, 99999999)}";
}
