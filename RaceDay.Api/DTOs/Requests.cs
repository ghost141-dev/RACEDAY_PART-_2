using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

/// <summary>Body for POST /api/auth/register.</summary>
public class RegisterRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Secret code required only when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

/// <summary>Body for POST /api/auth/login.</summary>
public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Body for PUT /api/profile.</summary>
public class UpdateProfileRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>Body for creating or updating an event.</summary>
public class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
}

/// <summary>Body for creating or updating a category.</summary>
public class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }
}

/// <summary>Body for POST /api/events/{eventId}/enrolments.</summary>
public class EnrolRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

/// <summary>Body for PUT /api/enrolments/{id}/status.</summary>
public class UpdateEnrolmentStatusRequest
{
    /// <summary>Pending or Confirmed.</summary>
    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>Body for capturing or correcting a result.</summary>
public class ResultRequest
{
    /// <summary>Finish time in hh:mm:ss format, e.g. 00:48:32.</summary>
    [Required]
    public TimeSpan FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int FinishPosition { get; set; }

    [Range(1, int.MaxValue)]
    public int TotalFinishers { get; set; }
}

/// <summary>multipart/form-data body for image uploads.</summary>
public class FileUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
