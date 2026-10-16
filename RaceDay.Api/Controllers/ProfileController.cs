using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
public class ProfileController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;

    public ProfileController(AppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Returns the logged-in user's own profile.</summary>
    /// <response code="200">Profile object.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Your account no longer exists. Please log in again.");
        return Ok(user.ToResponse());
    }

    /// <summary>Updates the logged-in user's own full name and phone number.</summary>
    /// <param name="request">fullName and phoneNumber.</param>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Your account no longer exists. Please log in again.");

        if (string.IsNullOrWhiteSpace(request.FullName))
            return Fail(400, "Full name is required.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await _db.SaveChangesAsync();

        return Ok(user.ToResponse());
    }

    /// <summary>Uploads or replaces the logged-in user's profile picture.</summary>
    /// <param name="request">multipart/form-data with an image file (jpg, png, gif, webp; max 5 MB).</param>
    /// <response code="200">Returns the picture URL.</response>
    /// <response code="400">Invalid file type or size.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("picture")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadPicture([FromForm] FileUploadRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Your account no longer exists. Please log in again.");

        var upload = await _storage.SaveImageAsync(request.File, "profiles");
        if (!upload.Success) return Fail(400, upload.Error!);

        user.ProfilePictureUrl = AbsoluteUrl(upload.RelativeUrl!);
        await _db.SaveChangesAsync();

        return Ok(new ImageUrlResponse { ImageUrl = user.ProfilePictureUrl });
    }
}
