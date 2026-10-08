using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GatesController(AirportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GateDto>>> GetGates(CancellationToken cancellationToken)
    {
        var gates = await dbContext.Gates
            .AsNoTracking()
            .OrderBy(gate => gate.Terminal)
            .ThenBy(gate => gate.GateNumber)
            .Select(gate => ToDto(gate))
            .ToListAsync(cancellationToken);

        return Ok(gates);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GateDto>> GetGate(int id, CancellationToken cancellationToken)
    {
        var gate = await dbContext.Gates.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return gate is null ? NotFound() : Ok(ToDto(gate));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GateDto>> CreateGate(CreateGateDto request, CancellationToken cancellationToken)
    {
        var gate = new Gate
        {
            GateNumber = request.GateNumber.Trim(),
            Terminal = request.Terminal.Trim()
        };

        dbContext.Gates.Add(gate);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetGate), new { id = gate.Id }, ToDto(gate));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GateDto>> UpdateGate(int id, UpdateGateDto request, CancellationToken cancellationToken)
    {
        var gate = await dbContext.Gates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (gate is null)
        {
            return NotFound();
        }

        gate.GateNumber = request.GateNumber.Trim();
        gate.Terminal = request.Terminal.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(gate));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGate(int id, CancellationToken cancellationToken)
    {
        var gate = await dbContext.Gates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (gate is null)
        {
            return NotFound();
        }

        dbContext.Gates.Remove(gate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static GateDto ToDto(Gate gate) => new(gate.Id, gate.GateNumber, gate.Terminal);
}
