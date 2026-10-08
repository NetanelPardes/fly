using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class ProcessedEvent
{
    public int Id { get; set; }

    public Guid EventId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Consumer { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}
