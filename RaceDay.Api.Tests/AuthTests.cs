using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

/// <summary>User registration, login, logout and session behaviour.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    private static RegisterRequest NewParticipant(string email) => new()
    {
        FullName = "Test Participant",
        Email = email,
        Password = TestHelpers.Password,
        Role = Roles.Participant
    };

    [Fact]
    public async Task Register_Participant_Returns201_AndNeverReturnsPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewParticipant(TestHelpers.UniqueEmail("p")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Participant", body);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant(TestHelpers.UniqueEmail("o"));
        request.Role = Roles.Organiser;
        request.OrganiserCode = TestHelpers.OrganiserCode;

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant(TestHelpers.UniqueEmail("o"));
        request.Role = Roles.Organiser;

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_Returns403()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant(TestHelpers.UniqueEmail("o"));
        request.Role = Roles.Organiser;
        request.OrganiserCode = "WRONG-CODE";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.UniqueEmail("dup");

        var first = await client.PostAsJsonAsync("/api/auth/register", NewParticipant(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", NewParticipant(email));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewParticipant("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant(TestHelpers.UniqueEmail("p"));
        request.Password = "123";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400()
    {
        var client = _factory.CreateClient();
        var request = NewParticipant(TestHelpers.UniqueEmail("p"));
        request.Role = "Admin";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.UniqueEmail("hash");
        await client.PostAsJsonAsync("/api/auth/register", NewParticipant(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(TestHelpers.Password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt hashes start with $2
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithRole()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.UniqueEmail("login");
        await client.PostAsJsonAsync("/api/auth/register", NewParticipant(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(Roles.Participant, user!.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.UniqueEmail("wrongpw");
        await client.PostAsJsonAsync("/api/auth/register", NewParticipant(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "nobody@nowhere.co.za", Password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_PersistsAcrossRequests_AfterLogin()
    {
        var user = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await user.Client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(user.Email, profile!.Email);
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_SoProfileIsNoLongerAvailable()
    {
        var user = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var logout = await user.Client.PostAsync("/api/auth/logout", null);
        var afterLogout = await user.Client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesOwnDetails()
    {
        var user = await TestHelpers.CreateLoggedInUserAsync(_factory, Roles.Participant);

        var response = await user.Client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest { FullName = "Renamed Person", PhoneNumber = "0825550000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Renamed Person", updated!.FullName);
        Assert.Equal("0825550000", updated.PhoneNumber);
    }
}
