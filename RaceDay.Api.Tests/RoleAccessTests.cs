using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

/// <summary>
/// Role-based access: unauthenticated callers get 401, and the wrong role gets 403,
/// in both directions (Participant -> Organiser functionality and vice versa).
/// </summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/profile")]
    [InlineData("/api/events/mine")]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events/mine")]
    [InlineData("/api/events/1/enrolments")]
    [InlineData("/api/events/1/results")]
    public async Task OrganiserOnlyGets_AsParticipant_Return403(string url)
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await participant.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    public async Task ParticipantOnlyGets_AsOrganiser_Return403(string url)
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);

        var response = await organiser.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateCategory_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Sneaky" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_Returns403()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await participant.Client.PostAsJsonAsync("/api/enrolments/1/results", new ResultRequest
        {
            FinishTime = new TimeSpan(0, 50, 0),
            FinishPosition = 1,
            TotalFinishers = 10
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEnrolmentStatusOrResults_Returns403()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var status = await participant.Client.PutAsJsonAsync("/api/enrolments/1/status", new UpdateEnrolmentStatusRequest { Status = "Confirmed" });
        var result = await participant.Client.PutAsJsonAsync("/api/results/1", new ResultRequest { FinishTime = new TimeSpan(0, 50, 0), FinishPosition = 1, TotalFinishers = 2 });

        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolOrCancelEnrolments_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);

        var enrol = await organiser.Client.PostAsJsonAsync("/api/events/1/enrolments", new EnrolRequest { CategoryId = 1 });
        var cancel = await organiser.Client.DeleteAsync("/api/enrolments/1");

        Assert.Equal(HttpStatusCode.Forbidden, enrol.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task PublicEndpoints_AreOpenToAnonymousUsers()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var anonymous = _factory.CreateClient();

        var list = await anonymous.GetAsync("/api/events");
        var detail = await anonymous.GetAsync($"/api/events/{ev.EventId}");
        var categories = await anonymous.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
    }
}
