using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

/// <summary>Event management: Organisers can manage their events, everyone else is rejected.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    private static EventRequest ValidEvent() => new()
    {
        Name = "Soweto 5K",
        Description = "Community run",
        EventDate = DateTime.Now.AddDays(20),
        Location = "Soweto",
        DistanceKm = 5,
        EventType = "Run"
    };

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Soweto 5K", created!.Name);
        Assert.Equal(organiser.UserId, created.OrganiserId);
    }

    [Fact]
    public async Task Participant_CreateEvent_Returns403()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await participant.Client.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CreateEvent_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var request = ValidEvent();
        request.EventType = "Swim";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var request = ValidEvent();
        request.Name = "";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CanListUpcomingEvents_Returns200()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var created = await TestHelpers.CreateEventAsync(organiser.Client);
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.Contains(events!, e => e.EventId == created.EventId);
    }

    [Fact]
    public async Task ListEvents_DoesNotIncludePastEvents()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var past = await TestHelpers.CreateEventAsync(organiser.Client, DateTime.Now.AddDays(-10));
        var anonymous = _factory.CreateClient();

        var events = await anonymous.GetFromJsonAsync<List<EventResponse>>("/api/events");

        Assert.DoesNotContain(events!, e => e.EventId == past.EventId);
    }

    [Fact]
    public async Task ListEvents_FilterByType_OnlyReturnsMatchingType()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var cycle = ValidEvent();
        cycle.Name = "Cycle Filter Test";
        cycle.EventType = "Cycle";
        await organiser.Client.PostAsJsonAsync("/api/events", cycle);
        var anonymous = _factory.CreateClient();

        var events = await anonymous.GetFromJsonAsync<List<EventResponse>>("/api/events?type=Cycle");

        Assert.NotEmpty(events!);
        Assert.All(events!, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task GetEventById_ReturnsDetail_WithCategories()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId, "Under 20");
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.Single(detail!.Categories);
        Assert.Equal("Under 20", detail.Categories[0].Name);
    }

    [Fact]
    public async Task GetEventById_Unknown_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_Returns200()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var update = ValidEvent();
        update.Name = "Renamed Event";

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Renamed Event", updated!.Name);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", ValidEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_Returns204_ThenEventIsGone()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");
        var afterwards = await organiser.Client.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvents_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var update = await participant.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", ValidEvent());
        var delete = await participant.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_MyEvents_OnlyListsOwnEvents()
    {
        var first = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var second = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var mine = await TestHelpers.CreateEventAsync(first.Client);
        var theirs = await TestHelpers.CreateEventAsync(second.Client);

        var events = await first.Client.GetFromJsonAsync<List<EventResponse>>("/api/events/mine");

        Assert.Contains(events!, e => e.EventId == mine.EventId);
        Assert.DoesNotContain(events!, e => e.EventId == theirs.EventId);
    }
}
