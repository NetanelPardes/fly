using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record GateDto(int Id, string GateNumber, string Terminal);

public record CreateGateDto(
    [Required, MaxLength(20)] string GateNumber,
    [Required, MaxLength(20)] string Terminal);

public record UpdateGateDto(
    [Required, MaxLength(20)] string GateNumber,
    [Required, MaxLength(20)] string Terminal);
