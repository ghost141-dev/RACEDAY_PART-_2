using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private static ResultRequest ValidResult() => new()
    {
        FinishTime = new TimeSpan(0, 48, 32),
        FinishPosition = 47,
        TotalFinishers = 312
    };

    [Fact]
    public async Task Organiser_CanCaptureResult_Returns201_AndParticipantSeesIt()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, organiser.UserId, participant.UserId);

        var capture = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());
        var mine = await participant.Client.GetFromJsonAsync<List<ResultResponse>>("/api/results/mine");

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        var result = Assert.Single(mine!);
        Assert.Equal(47, result.FinishPosition);
        Assert.Equal(312, result.TotalFinishers);
        Assert.Equal(new TimeSpan(0, 48, 32), result.FinishTime);
        Assert.Equal("Concluded Test Run", result.EventName);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, organiser.UserId, participant.UserId);
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());

        var second = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_PositionHigherThanTotalFinishers_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, organiser.UserId, participant.UserId);
        var request = ValidResult();
        request.FinishPosition = 500;

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForEventThatHasNotHappened_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", ValidResult());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotCaptureResult_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, owner.UserId, participant.UserId);

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_Returns404()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);

        var response = await organiser.Client.PostAsJsonAsync("/api/enrolments/999999/results", ValidResult());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectResult_Returns200()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, organiser.UserId, participant.UserId);
        var capture = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());
        var created = await capture.Content.ReadFromJsonAsync<ResultResponse>();
        var correction = ValidResult();
        correction.FinishPosition = 46;

        var response = await organiser.Client.PutAsJsonAsync($"/api/results/{created!.ResultId}", correction);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(46, updated!.FinishPosition);
    }

    [Fact]
    public async Task Organiser_CanViewAllResultsForOwnEvent()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var enrolmentId = await TestHelpers.SeedConcludedEnrolmentAsync(_factory, organiser.UserId, participant.UserId);
        var capture = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", ValidResult());
        var created = await capture.Content.ReadFromJsonAsync<ResultResponse>();

        var results = await organiser.Client.GetFromJsonAsync<List<ResultResponse>>($"/api/events/{created!.EventId}/results");

        Assert.Single(results!);
    }

    [Fact]
    public async Task Participant_WithNoResults_GetsEmptyList()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var results = await participant.Client.GetFromJsonAsync<List<ResultResponse>>("/api/results/mine");

        Assert.Empty(results!);
    }
}
