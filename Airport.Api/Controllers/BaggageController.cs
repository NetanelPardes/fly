using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BaggageController(AirportDbContext dbContext, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BaggageDto>>> GetBaggage(CancellationToken cancellationToken)
    {
        var query = dbContext.Baggage
            .AsNoTracking()
            .Include(baggage => baggage.Ticket)
            .AsQueryable();

        if (User.IsInRole(nameof(UserRole.Passenger)))
        {
            query = query.Where(baggage => baggage.Ticket.PassengerId == currentUser.UserId);
        }

        var baggageItems = await query
            .OrderByDescending(baggage => baggage.LastUpdatedAt)
            .Select(baggage => ToDto(baggage))
            .ToListAsync(cancellationToken);

        return Ok(baggageItems);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BaggageDto>> GetBaggageById(int id, CancellationToken cancellationToken)
    {
        var baggage = await dbContext.Baggage
            .AsNoTracking()
            .Include(item => item.Ticket)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (baggage is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && baggage.Ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        return Ok(ToDto(baggage));
    }

    [HttpPost]
    public async Task<ActionResult<BaggageDto>> CreateBaggage(CreateBaggageDto request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(
            ticket => ticket.Id == request.TicketId && ticket.Status == TicketStatus.Active,
            cancellationToken);
        if (ticket is null)
        {
            return ValidationProblem("Active ticket does not exist.");
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        if (!User.IsInRole(nameof(UserRole.Passenger)) && !User.IsInRole(nameof(UserRole.Admin)) && !User.IsInRole(nameof(UserRole.Baggage)))
        {
            return Forbid();
        }

        var baggage = new Baggage
        {
            TicketId = request.TicketId,
            TagNumber = request.TagNumber.Trim().ToUpperInvariant(),
            WeightKg = request.WeightKg,
            Status = BaggageStatus.CheckedIn,
            LastUpdatedAt = DateTime.UtcNow
        };

        dbContext.Baggage.Add(baggage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetBaggageById), new { id = baggage.Id }, ToDto(baggage));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Baggage,Admin")]
    public async Task<ActionResult<BaggageDto>> UpdateStatus(int id, UpdateBaggageStatusDto request, CancellationToken cancellationToken)
    {
        var baggage = await dbContext.Baggage.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (baggage is null)
        {
            return NotFound();
        }

        baggage.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(baggage));
    }

    private static BaggageDto ToDto(Baggage baggage) => new(
        baggage.Id,
        baggage.TicketId,
        baggage.TagNumber,
        baggage.WeightKg,
        baggage.Status,
        baggage.LastUpdatedAt);
}
