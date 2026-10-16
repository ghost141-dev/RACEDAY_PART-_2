using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public class CategoryTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public CategoryTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanAddCategory_Returns201_AndAnyoneCanViewIt()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var create = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Senior (60+)", MinAge = 60, DistanceKm = 10 });
        var list = await _factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains(list!, c => c.Name == "Senior (60+)");
    }

    [Fact]
    public async Task AddCategory_MinAgeGreaterThanMaxAge_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Bad", MinAge = 50, MaxAge = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotAddCategory_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListCategories_UnknownEvent_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateCategory_Returns200()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new CategoryRequest { Name = "21km", DistanceKm = 21.1m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal("21km", updated!.Name);
    }

    [Fact]
    public async Task Organiser_CanDeleteUnusedCategory_Returns204()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Organiser);
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
