using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record LoginDto([Required, EmailAddress] string Email, [Required] string Password);

public record RegisterPassengerDto(
    [MaxLength(9)] string? NationalId,
    [MaxLength(80)] string? FirstName,
    [MaxLength(80)] string? LastName,
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(4), MaxLength(100)] string Password,
    [MaxLength(20)] string? Gender,
    [MaxLength(240)] string? Address,
    [MaxLength(30)] string? PhoneNumber);

public record AuthResponseDto(string Token, UserDto User);

public record UserDto(
    int Id,
    string NationalId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    UserRole Role,
    string Gender,
    string Address,
    string PhoneNumber);

public record CreateUserDto(
    [MaxLength(9)] string? NationalId,
    [MaxLength(80)] string? FirstName,
    [MaxLength(80)] string? LastName,
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(4), MaxLength(100)] string Password,
    UserRole Role,
    [MaxLength(20)] string? Gender,
    [MaxLength(240)] string? Address,
    [MaxLength(30)] string? PhoneNumber);

public record UpdateUserDto(
    [Required, MaxLength(9)] string NationalId,
    [Required, MaxLength(80)] string FirstName,
    [Required, MaxLength(80)] string LastName,
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    UserRole Role,
    [Required, MaxLength(20)] string Gender,
    [Required, MaxLength(240)] string Address,
    [Required, MaxLength(30)] string PhoneNumber);
