using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    //Lists all categories available for a specific event.
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(404, "Event not found.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories.Select(c => c.ToResponse()));
    }

    //Adds an age or distance category to an event the Organiser owns.
    /// <param name="eventId">The event ID.</param>
    /// <param name="request">name, minAge, maxAge, distanceKm.</param>
    /// <response code="201">New category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only manage categories for your own events.");

        var error = Validate(request);
        if (error is not null) return Fail(400, error);

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", category.ToResponse());
    }

    //Updates an existing category on an event the Organiser owns.
    /// <param name="id">The category ID.</param>
    /// <param name="request">name, minAge, maxAge, distanceKm.</param>
    /// <response code="200">Updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only manage categories for your own events.");

        var error = Validate(request);
        if (error is not null) return Fail(400, error);

        category.Name = request.Name.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(category.ToResponse());
    }

    //Removes a category from an event the Organiser owns.
    /// <param name="id">The category ID.</param>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">Not the owning Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Category has existing enrolments.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only manage categories for your own events.");

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Fail(409, "This category has existing enrolments and cannot be deleted.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string? Validate(CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Category name is required.";
        if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge > request.MaxAge)
            return "Minimum age cannot be greater than maximum age.";
        return null;
    }
}
