using HelpdeskApi.Api.Dtos;
using HelpdeskApi.Domain;
using HelpdeskApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskApi.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly HelpdeskDbContext _db;

    public TicketsController(HelpdeskDbContext db)
    {
        _db = db;
    }

    // GET /api/tickets?status=Open&priority=High&page=1&pageSize=20
    // No manual tenant filtering needed here — the DbContext's global query
    // filter already scopes this to the caller's tenant.
    [HttpGet]
    public async Task<IActionResult> GetTickets(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Tickets.AsQueryable();

        if (status is not null) query = query.Where(t => t.Status == status);
        if (priority is not null) query = query.Where(t => t.Priority == priority);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAt,
                AssignedAgent = t.AssignedAgent != null ? t.AssignedAgent.Email : null
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);

        var ticket = new Ticket
        {
            Subject = request.Subject,
            Description = request.Description,
            Priority = request.Priority,
            CreatedByUserId = userId
            // TenantId is stamped automatically in DbContext.SaveChangesAsync
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
    }

    // Only Agents and Admins can change ticket status/assignment — Customers
    // can create and comment on tickets but not manage their lifecycle.
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateTicketRequest request)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        if (request.Status is not null) ticket.Status = request.Status.Value;
        if (request.AssignedAgentId is not null) ticket.AssignedAgentId = request.AssignedAgentId;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}