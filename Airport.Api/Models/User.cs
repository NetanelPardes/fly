using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(9)]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    [Required]
    [MaxLength(20)]
    public string Gender { get; set; } = "m";

    [Required]
    [MaxLength(240)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
