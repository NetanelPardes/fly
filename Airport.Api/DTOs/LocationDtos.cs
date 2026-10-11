namespace Airport.Api.DTOs;

public record AirportLocationDto(
    string City,
    string Country,
    string CountryCode,
    string AirportName,
    string? IataCode,
    string? IcaoCode,
    string Label);

public record AirportLocationCatalogDto(
    IReadOnlyList<string> Countries,
    IReadOnlyList<AirportLocationDto> Locations);
