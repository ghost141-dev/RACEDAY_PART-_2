using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public static class TestHelpers
{
    public const string OrganiserCode = "TEST-ORG-CODE";
    public const string Password = "Password123!";

    public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.raceday.co.za";

    /// <summary>Registers a new user and logs in. The returned client carries the session cookie.</summary>
    public static async Task<TestUser> CreateLoggedInUserAsync(RaceDayFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail(role.ToLowerInvariant());

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = $"Test {role}",
            Email = email,
            Password = Password,
            Role = role,
            OrganiserCode = role == Roles.Organiser ? OrganiserCode : null
        });
        register.EnsureSuccessStatusCode();
        var user = (await register.Content.ReadFromJsonAsync<UserResponse>())!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();

        return new TestUser(client, email, user.UserId);
    }

    public static async Task<EventResponse> CreateEventAsync(HttpClient organiserClient, DateTime? date = null)
    {
        var response = await organiserClient.PostAsJsonAsync("/api/events", new EventRequest
        {
            Name = "Test 10K",
            Description = "A test event",
            EventDate = date ?? DateTime.Now.AddDays(30),
            Location = "Johannesburg",
            DistanceKm = 10,
            EventType = "Run"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiserClient, int eventId, string name = "Open")
    {
        var response = await organiserClient.PostAsJsonAsync($"/api/events/{eventId}/categories", new CategoryRequest
        {
            Name = name,
            MinAge = 18,
            DistanceKm = 10
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(HttpClient participantClient, int eventId, int categoryId)
    {
        var response = await participantClient.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new EnrolRequest { CategoryId = categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }

    /// <summary>
    /// Inserts an event that has already taken place, with an enrolment, directly into the test database.
    /// (The API rightly refuses to enrol people into past events, so this is the only way to set up result tests.)
    /// </summary>
    public static async Task<int> SeedConcludedEnrolmentAsync(RaceDayFactory factory, int organiserId, int participantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ev = new Event
        {
            OrganiserId = organiserId,
            Name = "Concluded Test Run",
            EventDate = DateTime.Now.AddDays(-5),
            Location = "Pretoria",
            DistanceKm = 10,
            EventType = EventTypes.Run
        };
        var category = new Category { Event = ev, Name = "Open" };
        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            Event = ev,
            Category = category,
            Status = EnrolmentStatuses.Confirmed
        };

        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();
        return enrolment.EnrolmentId;
    }
}

public record TestUser(HttpClient Client, string Email, int UserId);
