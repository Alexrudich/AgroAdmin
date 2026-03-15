using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Guests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public GuestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GuestDto>> GetById(int id)
    {
        var guest = await _context.Guests
            .Select(g => new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                Phone = g.Phone,
                CreatedAt = g.CreatedAt,
                Comment = g.Comment,
                TotalStays = g.Bookings.Count,
                LastBookingDate = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.ArrivalDate)
                    .FirstOrDefault(),
                LastFeedback = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.FeedbackComment)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(g => g.Id == id);

        if (guest == null)
            return NotFound();

        return Ok(guest);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] GuestDto dto)
    {
        var guest = new Guest(
            dto.FullName,
            dto.Phone,
            dto.Comment
        );

        _context.Guests.Add(guest);
        await _context.SaveChangesAsync();

        return Ok(guest.Id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] GuestDto dto)
    {
        var guest = await _context.Guests.FindAsync(id);
        if (guest == null)
            return NotFound();

        guest.UpdateInfo(dto.FullName, dto.Phone, dto.Comment);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<GuestDto>>> Search([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new List<GuestDto>());

        var guests = await _context.Guests
            .Where(g => EF.Functions.Like(g.FullName, $"%{term}%") ||
                        EF.Functions.Like(g.Phone, $"%{term}%"))
            .Select(g => new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                Phone = g.Phone,
                CreatedAt = g.CreatedAt,
                Comment = g.Comment,
                TotalStays = g.Bookings.Count,
                LastBookingDate = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.ArrivalDate)
                    .FirstOrDefault(),
                LastFeedback = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.FeedbackComment)
                    .FirstOrDefault()
            })
            .Take(10)
            .ToListAsync();

        return Ok(guests);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GuestDto>>> GetAll(int page = 1, int pageSize = 50)
    {
        var guests = await _context.Guests
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                Phone = g.Phone,
                CreatedAt = g.CreatedAt,
                Comment = g.Comment,
                TotalStays = g.Bookings.Count,
                LastBookingDate = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.ArrivalDate)
                    .FirstOrDefault(),
                LastFeedback = g.Bookings
                    .OrderByDescending(b => b.ArrivalDate)
                    .Select(b => b.FeedbackComment)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(guests);
    }
}