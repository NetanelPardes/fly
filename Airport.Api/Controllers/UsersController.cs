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
[Authorize]
public class UsersController(
    AirportDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ICurrentUserService currentUser,
    IEmailSender emailSender,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken)
            || await dbContext.Employees.AnyAsync(employee => employee.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        var identity = NormalizeIdentity(request.FullName, request.FirstName, request.LastName);
        var nationalId = string.IsNullOrWhiteSpace(request.NationalId) ? GenerateNineDigitValue() : request.NationalId.Trim();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? GeneratePhoneValue() : request.PhoneNumber.Trim();
        if (await dbContext.Users.AnyAsync(user => user.NationalId == nationalId, cancellationToken)
            || await dbContext.Employees.AnyAsync(employee => employee.NationalId == nationalId, cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(user => user.PhoneNumber == phone, cancellationToken)
            || await dbContext.Employees.AnyAsync(employee => employee.PhoneNumber == phone, cancellationToken))
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, UpdateUserDto request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var nationalId = request.NationalId.Trim();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var gender = request.Gender.Trim();
        var address = request.Address.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.NationalId == nationalId, cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != id && item.PhoneNumber == phoneNumber, cancellationToken))
        {
            return Conflict("Phone number already exists.");
        }

        var changed = user.NationalId != nationalId
            || user.FirstName != firstName
            || user.LastName != lastName
            || user.FullName != fullName
            || user.Email != email
            || user.Role != request.Role
            || user.Gender != gender
            || user.Address != address
            || user.PhoneNumber != phoneNumber;

        user.NationalId = nationalId;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.FullName = fullName;
        user.Email = email;
        user.Role = request.Role;
        user.Gender = gender;
        user.Address = address;
        user.PhoneNumber = phoneNumber;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (changed)
        {
            await TrySendEmailAsync(ProfileUpdatedEmailTemplate.Create(user), cancellationToken);
        }

        return Ok(ToDto(user));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
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

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken)
    {
        var email = currentUser.Email?.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Email == email, cancellationToken);
        user ??= await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Role == UserRole.Admin, cancellationToken);
        if (user is not null)
        {
            return Ok(ToDto(user));
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Email == email, cancellationToken);
        employee ??= await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentUser.UserId, cancellationToken);

        return employee is null ? NotFound() : Ok(ToDto(employee));
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateMyProfileDto request, CancellationToken cancellationToken)
    {
        var emailClaim = currentUser.Email?.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Email == emailClaim, cancellationToken);
        user ??= await dbContext.Users.FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Role == UserRole.Admin, cancellationToken);
        if (user is not null)
        {
            return await UpdateUserProfile(user, request, cancellationToken);
        }

        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.Email == emailClaim, cancellationToken);
        employee ??= await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == currentUser.UserId, cancellationToken);
        if (employee is not null)
        {
            return await UpdateEmployeeProfile(employee, request, cancellationToken);
        }

        return NotFound();
    }

    private async Task<ActionResult<UserDto>> UpdateUserProfile(User user, UpdateMyProfileDto request, CancellationToken cancellationToken)
    {
        var nationalId = request.NationalId.Trim();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phoneNumber = request.PhoneNumber.Trim();
        var gender = request.Gender.Trim();
        var address = request.Address.Trim();

        if (await dbContext.Users.AnyAsync(item => item.Id != user.Id && item.Email == email, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != user.Id && item.NationalId == nationalId, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.NationalId == nationalId, cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.Id != user.Id && item.PhoneNumber == phoneNumber, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.PhoneNumber == phoneNumber, cancellationToken))
        {
            return Conflict("Phone number already exists.");
        }

        var changed = user.NationalId != nationalId
            || user.FirstName != firstName
            || user.LastName != lastName
            || user.FullName != fullName
            || user.Email != email
            || user.Gender != gender
            || user.Address != address
            || user.PhoneNumber != phoneNumber;

        user.NationalId = nationalId;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.FullName = fullName;
        user.Email = email;
        user.Gender = gender;
        user.Address = address;
        user.PhoneNumber = phoneNumber;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (changed)
        {
            await TrySendEmailAsync(ProfileUpdatedEmailTemplate.Create(user), cancellationToken);
        }

        return Ok(ToDto(user));
    }

    private async Task<ActionResult<UserDto>> UpdateEmployeeProfile(Employee employee, UpdateMyProfileDto request, CancellationToken cancellationToken)
    {
        var nationalId = request.NationalId.Trim();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phoneNumber = request.PhoneNumber.Trim();
        var gender = request.Gender.Trim();
        var address = request.Address.Trim();

        if (await dbContext.Users.AnyAsync(item => item.Email == email, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.Id != employee.Id && item.Email == email, cancellationToken))
        {
            return Conflict("Email already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.NationalId == nationalId, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.Id != employee.Id && item.NationalId == nationalId, cancellationToken))
        {
            return Conflict("National ID already exists.");
        }

        if (await dbContext.Users.AnyAsync(item => item.PhoneNumber == phoneNumber, cancellationToken)
            || await dbContext.Employees.AnyAsync(item => item.Id != employee.Id && item.PhoneNumber == phoneNumber, cancellationToken))
        {
            return Conflict("Phone number already exists.");
        }

        var changed = employee.NationalId != nationalId
            || employee.FirstName != firstName
            || employee.LastName != lastName
            || employee.FullName != fullName
            || employee.Email != email
            || employee.Gender != gender
            || employee.Address != address
            || employee.PhoneNumber != phoneNumber;

        employee.NationalId = nationalId;
        employee.FirstName = firstName;
        employee.LastName = lastName;
        employee.FullName = fullName;
        employee.Email = email;
        employee.Gender = gender;
        employee.Address = address;
        employee.PhoneNumber = phoneNumber;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (changed)
        {
            await TrySendEmailAsync(ProfileUpdatedEmailTemplate.Create(employee), cancellationToken);
        }

        return Ok(ToDto(employee));
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

    private async Task TrySendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        await TrySendEmailAsync(new EmailMessage(to, subject, body), cancellationToken);
    }

    private async Task TrySendEmailAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "User email could not be sent to {Recipient}.", message.To);
        }
    }
}
