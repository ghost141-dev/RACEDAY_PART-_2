using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

/// <summary>Event enrolments: Participants can enrol and the enrolment is recorded correctly.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_Returns201_WithPendingStatus()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new EnrolRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(participant.UserId, enrolment!.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(EnrolmentStatuses.Pending, enrolment.Status);
    }

    [Fact]
    public async Task Enrolment_IsRecorded_AndVisibleToParticipantAndOwningOrganiser()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/mine");
        var forEvent = await organiser.Client.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments");

        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
        Assert.Contains(forEvent!, e => e.EnrolmentId == enrolment.EnrolmentId && e.ParticipantId == participant.UserId);
    }

    [Fact]
    public async Task Enrol_Twice_Returns409()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var second = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new EnrolRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var eventA = await TestHelpers.CreateEventAsync(organiser.Client);
        var eventB = await TestHelpers.CreateEventAsync(organiser.Client);
        var categoryOfB = await TestHelpers.CreateCategoryAsync(organiser.Client, eventB.EventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventA.EventId}/enrolments", new EnrolRequest { CategoryId = categoryOfB.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownEvent_Returns404()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await participant.Client.PostAsJsonAsync("/api/events/999999/enrolments", new EnrolRequest { CategoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_PastEvent_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var pastEvent = await TestHelpers.CreateEventAsync(organiser.Client, DateTime.Now.AddDays(-3));
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, pastEvent.EventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{pastEvent.EventId}/enrolments", new EnrolRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events/1/enrolments", new EnrolRequest { CategoryId = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new EnrolRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotViewEventEnrolments_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_Returns204()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var response = await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelAnotherParticipantsEnrolment_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(owner.Client, ev.EventId, category.CategoryId);

        var response = await other.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanConfirmEnrolment_StatusChangesToConfirmed()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new UpdateEnrolmentStatusRequest { Status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(EnrolmentStatuses.Confirmed, updated!.Status);
    }

    [Fact]
    public async Task Participant_CannotChangeEnrolmentStatus_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var response = await participant.Client.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new UpdateEnrolmentStatusRequest { Status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
