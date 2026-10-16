using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class ResultsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public ResultsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Captures a finish time and position for a Participant's enrolment after the event.</summary>
    /// <param name="enrolmentId">The enrolment ID.</param>
    /// <param name="request">finishTime (hh:mm:ss), finishPosition, totalFinishers.</param>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed, or the event has not taken place yet.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">Result already captured.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/results")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Event)
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only capture results for your own events.");

        var error = Validate(request);
        if (error is not null) return Fail(400, error);

        if (enrolment.Event.EventDate > DateTime.Now)
            return Fail(400, "Results can only be captured after the event has taken place.");

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return Fail(409, "A result has already been captured for this enrolment.");

        var result = new RaceResult
        {
            Enrolment = enrolment,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition,
            TotalFinishers = request.TotalFinishers
        };

        _db.Results.Add(result);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(409, "A result has already been captured for this enrolment.");
        }

        return Created("/api/results/mine", result.ToResponse());
    }

    /// <summary>Corrects a previously captured result.</summary>
    /// <param name="id">The result ID.</param>
    /// <param name="request">finishTime (hh:mm:ss), finishPosition, totalFinishers.</param>
    /// <response code="200">Updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultRequest request)
    {
        var result = await WithDetails(_db.Results).FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Fail(404, "Result not found.");
        if (result.Enrolment.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only correct results for your own events.");

        var error = Validate(request);
        if (error is not null) return Fail(400, error);

        result.FinishTime = request.FinishTime;
        result.FinishPosition = request.FinishPosition;
        result.TotalFinishers = request.TotalFinishers;
        await _db.SaveChangesAsync();

        return Ok(result.ToResponse());
    }

    /// <summary>Returns the logged-in Participant's full personal race history.</summary>
    /// <response code="200">Array of results across all completed events, most recent first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    [HttpGet("api/results/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;
        var results = await WithDetails(_db.Results.AsNoTracking())
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .ToListAsync();

        return Ok(results.Select(r => r.ToResponse()));
    }

    /// <summary>Returns all captured results for an event (for the owning Organiser to review).</summary>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of results ordered by finishing position.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only view results for your own events.");

        var results = await WithDetails(_db.Results.AsNoTracking())
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.FinishPosition)
            .ToListAsync();

        return Ok(results.Select(r => r.ToResponse()));
    }

    private static IQueryable<RaceResult> WithDetails(IQueryable<RaceResult> query) =>
        query
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category);

    private static string? Validate(ResultRequest request)
    {
        if (request.FinishTime <= TimeSpan.Zero) return "Finish time must be greater than zero.";
        if (request.FinishPosition < 1) return "Finish position must be 1 or higher.";
        if (request.TotalFinishers < request.FinishPosition)
            return "Total finishers cannot be less than the finishing position.";
        return null;
    }
}
