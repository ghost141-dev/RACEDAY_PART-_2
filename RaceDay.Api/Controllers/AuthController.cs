using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

   //Creates a new user account as an Organiser or a Participant.
    
    // Registering as an Organiser also requires the secret organiserCode so that nobody can simply
    // choose the Organiser role. The password is hashed with BCrypt and never returned.
    /// <param name="request">fullName, email, password, role, phoneNumber and (Organisers only) organiserCode.</param>
    /// <response code="201">User created. The password is never returned.</response>
    /// <response code="400">Validation failed (e.g. invalid email, short password, unknown role).</response>
    /// <response code="403">Organiser registration attempted without a valid organiser code.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var role = Roles.Normalise(request.Role);
        if (role is null)
            return Fail(400, "Role must be either Organiser or Participant.");

        if (role == Roles.Organiser)
        {
            var expectedCode = _config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expectedCode) || request.OrganiserCode != expectedCode)
                return Fail(403, "A valid organiser registration code is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail(409, "An account with this email address already exists.");

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim()
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(409, "An account with this email address already exists.");
        }

        return Created("/api/profile", user.ToResponse());
    }

    //Authenticates a user and starts a session storing their user ID and role.
    /// <param name="request">email and password.</param>
    /// <response code="200">Session started. Returns basic user info and role.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Fail(401, "Invalid email or password.");

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(user.ToResponse());
    }

    /// <summary>Ends the current user's session.</summary>
    /// <response code="200">Session ended.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new MessageResponse { Message = "You have been logged out." });
    }
}
