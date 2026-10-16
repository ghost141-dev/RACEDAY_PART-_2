using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The logged-in user's ID from the session (0 if no session).</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>The logged-in user's role from the session.</summary>
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    protected IActionResult Fail(int statusCode, string detail) =>
        Problem(detail: detail, statusCode: statusCode);

    protected string AbsoluteUrl(string relativeUrl) =>
        $"{Request.Scheme}://{Request.Host}{relativeUrl}";
}
