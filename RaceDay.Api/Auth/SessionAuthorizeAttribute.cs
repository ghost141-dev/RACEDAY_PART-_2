using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Auth;

/// <summary>
/// Protects an endpoint using the server-side session created at login.
/// No session -> 401 Unauthorized. Wrong role -> 403 Forbidden.
/// Use [SessionAuthorize] for "any logged-in user" or [SessionAuthorize(Roles.Organiser)] for a specific role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string? _requiredRole;

    public SessionAuthorizeAttribute() { }

    public SessionAuthorizeAttribute(string requiredRole)
    {
        _requiredRole = requiredRole;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);

        if (userId is null)
        {
            context.Result = BuildResult(StatusCodes.Status401Unauthorized, "Unauthorized",
                "You must be logged in to access this resource.");
            return;
        }

        if (_requiredRole is not null && session.GetString(SessionKeys.Role) != _requiredRole)
        {
            context.Result = BuildResult(StatusCodes.Status403Forbidden, "Forbidden",
                $"This resource is restricted to the {_requiredRole} role.");
        }
    }

    private static ObjectResult BuildResult(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail })
        {
            StatusCode = status
        };
}
