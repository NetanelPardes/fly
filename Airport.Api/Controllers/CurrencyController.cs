using Airport.Api.DTOs;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airport.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/currency")]
public class CurrencyController(ICurrencyExchangeService currencyExchangeService) : ControllerBase
{
    [HttpGet("rates")]
    public async Task<ActionResult<CurrencyRatesDto>> GetRates(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await currencyExchangeService.GetUsdRatesAsync(cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Currency conversion is unavailable",
                Detail = error.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}
