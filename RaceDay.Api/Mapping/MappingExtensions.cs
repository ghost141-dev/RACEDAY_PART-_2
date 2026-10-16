using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Mapping;

/// <summary>Maps entities to response DTOs so password hashes and navigation loops never leave the API.</summary>
public static class MappingExtensions
{
    public static UserResponse ToResponse(this AppUser u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber,
        ProfilePictureUrl = u.ProfilePictureUrl,
        CreatedAt = u.CreatedAt
    };

    public static EventResponse ToResponse(this Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser?.FullName ?? string.Empty,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        BannerImageUrl = e.BannerImageUrl,
        CreatedAt = e.CreatedAt
    };

    public static EventDetailResponse ToDetailResponse(this Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser?.FullName ?? string.Empty,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        BannerImageUrl = e.BannerImageUrl,
        CreatedAt = e.CreatedAt,
        Categories = e.Categories.OrderBy(c => c.Name).Select(c => c.ToResponse()).ToList(),
        Routes = e.Routes.Select(r => r.ToResponse()).ToList()
    };

    public static CategoryResponse ToResponse(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        DistanceKm = c.DistanceKm
    };

    public static RouteResponse ToResponse(this EventRoute r) => new()
    {
        RouteId = r.RouteId,
        EventId = r.EventId,
        StartPoint = r.StartPoint,
        EndPoint = r.EndPoint,
        ElevationGainM = r.ElevationGainM,
        RouteMapUrl = r.RouteMapUrl,
        Notes = r.Notes
    };

    public static EnrolmentResponse ToResponse(this Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant?.FullName ?? string.Empty,
        EventId = e.EventId,
        EventName = e.Event?.Name ?? string.Empty,
        EventDate = e.Event?.EventDate ?? default,
        CategoryId = e.CategoryId,
        CategoryName = e.Category?.Name ?? string.Empty,
        Status = e.Status,
        EnrolledAt = e.EnrolledAt
    };

    public static ResultResponse ToResponse(this RaceResult r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant?.FullName ?? string.Empty,
        EventId = r.Enrolment.EventId,
        EventName = r.Enrolment.Event?.Name ?? string.Empty,
        EventDate = r.Enrolment.Event?.EventDate ?? default,
        CategoryName = r.Enrolment.Category?.Name ?? string.Empty,
        FinishTime = r.FinishTime,
        FinishPosition = r.FinishPosition,
        TotalFinishers = r.TotalFinishers,
        CapturedAt = r.CapturedAt
    };
}
