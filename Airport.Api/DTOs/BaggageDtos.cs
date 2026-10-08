using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record BaggageDto(
    int Id,
    int TicketId,
    string TagNumber,
    decimal WeightKg,
    BaggageStatus Status,
    DateTime LastUpdatedAt);

public record CreateBaggageDto(
    int TicketId,
    [Required, MaxLength(40)] string TagNumber,
    [Range(0.1, 100)] decimal WeightKg);

public record UpdateBaggageStatusDto(BaggageStatus Status);
