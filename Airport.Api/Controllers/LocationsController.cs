using Airport.Api.DTOs;
using Airport.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController(AirportDbContext dbContext) : ControllerBase
{
    [HttpGet("airports")]
    public async Task<ActionResult<AirportLocationCatalogDto>> GetAirportLocations(CancellationToken cancellationToken)
    {
        var locations = await dbContext.AirportLocations
            .AsNoTracking()
            .OrderBy(airport => airport.Country)
            .ThenBy(airport => airport.City)
            .ThenBy(airport => airport.Name)
            .Select(airport => new AirportLocationDto(
                airport.City,
                airport.Country,
                airport.Country,
                airport.Name,
                airport.Code,
                null,
                $"{airport.Country}, {airport.City} ({airport.Name})"))
            .ToListAsync(cancellationToken);

        var countries = locations
            .Select(location => location.Country)
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(country => country)
            .ToList();

        return Ok(new AirportLocationCatalogDto(countries, locations));
    }
}
