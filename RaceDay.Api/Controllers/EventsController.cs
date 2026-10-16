using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;

    public EventsController(AppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Lists all upcoming events, with optional filtering by type or location.</summary>
    /// <param name="type">Optional: Run, Walk or Cycle.</param>
    /// <param name="location">Optional: text the event location must contain.</param>
    /// <response code="200">Array of upcoming events, soonest first.</response>
    /// <response code="400">Unknown event type filter.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUpcoming([FromQuery] string? type, [FromQuery] string? location)
    {
        var now = DateTime.Now;
        var query = _db.Events.AsNoTracking().Include(e => e.Organiser).Where(e => e.EventDate >= now);

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalisedType = EventTypes.Normalise(type);
            if (normalisedType is null) return Fail(400, "Event type must be Run, Walk or Cycle.");
            query = query.Where(e => e.EventType == normalisedType);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var term = location.Trim().ToLower();
            query = query.Where(e => e.Location.ToLower().Contains(term));
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    /// <summary>Lists every event owned by the logged-in Organiser (past and upcoming).</summary>
    /// <remarks>Additional endpoint (not in the Part 1 plan) used by the Part 3 Organiser dashboard.</remarks>
    /// <response code="200">Array of the Organiser's events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;
        var events = await _db.Events.AsNoTracking()
            .Include(e => e.Organiser)
            .Where(e => e.OrganiserId == userId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    /// <summary>Returns full detail for one event, including its categories and routes.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="200">Event object.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .Include(e => e.Routes)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null) return Fail(404, "Event not found.");
        return Ok(ev.ToDetailResponse());
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <param name="request">name, description, eventDate, location, distanceKm, eventType (Run, Walk or Cycle).</param>
    /// <response code="201">New event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequest request)
    {
        var error = Validate(request, out var eventType);
        if (error is not null) return Fail(400, error);

        var organiser = await _db.Users.FindAsync(CurrentUserId);
        if (organiser is null) return Fail(401, "Your account no longer exists. Please log in again.");

        var ev = new Event
        {
            OrganiserId = organiser.UserId,
            Organiser = organiser,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = eventType!
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToResponse());
    }

    /// <summary>Updates an event the logged-in Organiser owns.</summary>
    /// <param name="id">The event ID.</param>
    /// <param name="request">name, description, eventDate, location, distanceKm, eventType.</param>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only modify your own events.");

        var error = Validate(request, out var eventType);
        if (error is not null) return Fail(400, error);

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = eventType!;
        await _db.SaveChangesAsync();

        return Ok(ev.ToResponse());
    }

    /// <summary>Deletes an event the logged-in Organiser owns, together with its categories, routes, enrolments and results.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only delete your own events.");

        // Foreign keys use Restrict (matching the SQL script), so dependants are removed explicitly.
        // A single SaveChanges runs as one database transaction.
        _db.Results.RemoveRange(await _db.Results.Where(r => r.Enrolment.EventId == id).ToListAsync());
        _db.Enrolments.RemoveRange(await _db.Enrolments.Where(en => en.EventId == id).ToListAsync());
        _db.Categories.RemoveRange(await _db.Categories.Where(c => c.EventId == id).ToListAsync());
        _db.Routes.RemoveRange(await _db.Routes.Where(r => r.EventId == id).ToListAsync());
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Uploads a banner image for an event the Organiser owns.</summary>
    /// <param name="id">The event ID.</param>
    /// <param name="request">multipart/form-data with an image file (jpg, png, gif, webp; max 5 MB).</param>
    /// <response code="200">Returns the banner URL.</response>
    /// <response code="400">Invalid file type or size.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadBanner(int id, [FromForm] FileUploadRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only modify your own events.");

        var upload = await _storage.SaveImageAsync(request.File, "banners");
        if (!upload.Success) return Fail(400, upload.Error!);

        ev.BannerImageUrl = AbsoluteUrl(upload.RelativeUrl!);
        await _db.SaveChangesAsync();

        return Ok(new ImageUrlResponse { ImageUrl = ev.BannerImageUrl });
    }

    private static string? Validate(EventRequest request, out string? eventType)
    {
        eventType = EventTypes.Normalise(request.EventType);

        if (string.IsNullOrWhiteSpace(request.Name)) return "Event name is required.";
        if (string.IsNullOrWhiteSpace(request.Location)) return "Event location is required.";
        if (eventType is null) return "Event type must be Run, Walk or Cycle.";
        if (request.EventDate == default) return "Event date is required.";
        if (request.DistanceKm <= 0) return "Distance must be greater than zero.";
        return null;
    }
}
