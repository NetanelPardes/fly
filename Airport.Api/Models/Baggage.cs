using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Airport.Api.Models;

public class Baggage
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    [MaxLength(40)]
    public string TagNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(6,2)")]
    public decimal WeightKg { get; set; }

    public BaggageStatus Status { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
