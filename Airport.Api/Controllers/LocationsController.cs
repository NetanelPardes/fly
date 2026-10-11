using Airport.Api.DTOs;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController(AirportLocationCatalog catalog) : ControllerBase
{
    [HttpGet("airports")]
    public ActionResult<AirportLocationCatalogDto> GetAirportLocations()
    {
        return Ok(catalog.GetCatalog());
    }
}
