using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class EnrolmentsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public EnrolmentsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Enrols the logged-in Participant into an event under a chosen category.</summary>
    /// <remarks>The new enrolment starts with status Pending.</remarks>
    /// <param name="eventId">The event ID.</param>
    /// <param name="request">categoryId.</param>
    /// <response code="201">Enrolment record with status.</response>
    /// <response code="400">Category does not belong to the event, or the event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found.");

        if (ev.EventDate < DateTime.Now)
            return Fail(400, "This event has already taken place.");

        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == eventId);
        if (category is null)
            return Fail(400, "The selected category does not belong to this event.");

        var participant = await _db.Users.FindAsync(CurrentUserId);
        if (participant is null) return Fail(401, "Your account no longer exists. Please log in again.");

        var alreadyEnrolled = await _db.Enrolments
            .AnyAsync(e => e.ParticipantId == participant.UserId && e.EventId == eventId);
        if (alreadyEnrolled)
            return Fail(409, "You are already enrolled in this event.");

        var enrolment = new Enrolment
        {
            Participant = participant,
            Event = ev,
            Category = category,
            Status = EnrolmentStatuses.Pending
        };

        _db.Enrolments.Add(enrolment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(409, "You are already enrolled in this event.");
        }

        return Created("/api/enrolments/mine", enrolment.ToResponse());
    }

    /// <summary>Lists all Participants enrolled in an event (for the owning Organiser).</summary>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of enrolments with participant, category and status.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only view enrolments for your own events.");

        var enrolments = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.EnrolledAt)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToResponse()));
    }

    /// <summary>Lists all events the logged-in Participant has enrolled for, with status.</summary>
    /// <response code="200">Array of the Participant's enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    [HttpGet("api/enrolments/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;
        var enrolments = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Where(e => e.ParticipantId == userId)
            .OrderBy(e => e.Event.EventDate)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToResponse()));
    }

    /// <summary>Cancels the logged-in Participant's own enrolment before the event date.</summary>
    /// <param name="id">The enrolment ID.</param>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">Not the owning Participant.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">Event has already concluded.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.ParticipantId != CurrentUserId) return Fail(403, "You can only cancel your own enrolments.");
        if (enrolment.Event.EventDate < DateTime.Now) return Fail(409, "This event has already concluded.");

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Changes an enrolment's status to Pending or Confirmed (owning Organiser only).</summary>
    /// <remarks>Additional endpoint (not in the Part 1 plan) so Organisers can confirm enrolments.</remarks>
    /// <param name="id">The enrolment ID.</param>
    /// <param name="request">status: Pending or Confirmed.</param>
    /// <response code="200">Updated enrolment.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpPut("api/enrolments/{id:int}/status")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateEnrolmentStatusRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only manage enrolments for your own events.");

        var status = EnrolmentStatuses.Normalise(request.Status);
        if (status is null) return Fail(400, "Status must be Pending or Confirmed.");

        enrolment.Status = status;
        await _db.SaveChangesAsync();

        return Ok(enrolment.ToResponse());
    }
}
