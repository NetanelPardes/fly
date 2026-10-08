using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class OutboxMessage
{
    public int Id { get; set; }

    public Guid EventId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Attempts { get; set; }

    [MaxLength(500)]
    public string? LastError { get; set; }
}
