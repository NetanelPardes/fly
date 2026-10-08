using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record LoginDto([Required, EmailAddress] string Email, [Required] string Password);

public record RegisterPassengerDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(6), MaxLength(100)] string Password);

public record AuthResponseDto(string Token, UserDto User);

public record UserDto(int Id, string FullName, string Email, UserRole Role);

public record CreateUserDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(6), MaxLength(100)] string Password,
    UserRole Role);

public record UpdateUserDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    UserRole Role);
