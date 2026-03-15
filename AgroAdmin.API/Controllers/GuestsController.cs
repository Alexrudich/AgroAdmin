using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Guests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuestsController(AppDbContext context) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<GuestDto>> GetById(int id)
    {
        var guest = await context.Guests
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

        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        return Ok(guest.Id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] GuestDto dto)
    {
        var guest = await context.Guests.FindAsync(id);
        if (guest == null)
            return NotFound();

        guest.UpdateInfo(dto.FullName, dto.Phone, dto.Comment);
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<GuestDto>>> Search([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new List<GuestDto>());

        var guests = await context.Guests
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
        var guests = await context.Guests
            .Include(g => g.Bookings)
            .Include(g => g.GroupMembers)
            .ThenInclude(m => m.Group)
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
                    .FirstOrDefault(),
                Group = g.GroupMembers.Select(m => new GuestGroupDto
                {
                    Id = m.Group.Id,
                    Name = m.Group.Name,
                    Description = m.Group.Description,
                    MembersCount = m.Group.Members.Count
                }).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(guests);
    }

    [HttpGet("groups")]
    public async Task<ActionResult<List<GuestGroupDto>>> GetAllGroups()
    {
        var groups = await context.GuestGroups
            .Select(g => new GuestGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                MembersCount = g.Members.Count,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync();

        return Ok(groups);
    }

    [HttpPost("{id}/groups")]
    public async Task<IActionResult> AddToGroup(int id, [FromBody] AddToGroupRequestDto request)
    {
        var guest = await context.Guests.FindAsync(id);
        if (guest == null) return NotFound("Гость не найден");

        var group = await context.GuestGroups.FindAsync(request.GroupId);
        if (group == null) return NotFound("Группа не найдена");

        // Проверяем, не состоит ли уже
        var alreadyMember = await context.GuestGroupMembers
            .AnyAsync(m => m.GuestId == id && m.GroupId == request.GroupId);

        if (alreadyMember)
        {
            return BadRequest("Гость уже состоит в этой группе");
        }

        var member = new GuestGroupMember
        {
            GuestId = id,
            GroupId = request.GroupId
        };

        context.GuestGroupMembers.Add(member);
        await context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("groups")]
    public async Task<ActionResult<int>> CreateGroup([FromBody] CreateGroupRequestDto request)
    {
        var group = new GuestGroup
        {
            Name = request.Name,
            Description = request.Description
        };

        context.GuestGroups.Add(group);
        await context.SaveChangesAsync();

        return Ok(group.Id);
    }

    [HttpGet("{id}/group")]
    public async Task<ActionResult<GuestGroupDto>> GetGuestGroup(int id)
    {
        var member = await context.GuestGroupMembers
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.GuestId == id);

        if (member == null) return Ok(null);

        return Ok(new GuestGroupDto
        {
            Id = member.Group.Id,
            Name = member.Group.Name,
            Description = member.Group.Description,
            MembersCount = await context.GuestGroupMembers
                .CountAsync(m => m.GroupId == member.Group.Id)
        });
    }

    [HttpDelete("{id}/group")]
    public async Task<IActionResult> RemoveFromGroup(int id)
    {
        var member = await context.GuestGroupMembers
            .FirstOrDefaultAsync(m => m.GuestId == id);

        if (member == null) return NotFound();

        context.GuestGroupMembers.Remove(member);
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("group/{groupId}/members")]
    public async Task<ActionResult<List<GuestDto>>> GetGroupMembers(int groupId)
    {
        var members = await context.GuestGroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => m.Guest)
            .Select(g => new GuestDto
            {
                Id = g.Id,
                FullName = g.FullName,
                Phone = g.Phone,
                CreatedAt = g.CreatedAt,
                Comment = g.Comment,
                TotalStays = g.Bookings.Count
            })
            .ToListAsync();

        return Ok(members);
    }

    [HttpDelete("groups/{groupId}")]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        var group = await context.GuestGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) return NotFound();

        context.GuestGroups.Remove(group);
        await context.SaveChangesAsync();

        return NoContent();
    }
}