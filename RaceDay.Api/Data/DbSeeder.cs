using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds demo data that mirrors the Part 1 SQL script (2 Organisers, 2 Participants, 3 Events,
/// categories, routes, enrolments, results). Runs only in Development and only if the Users table is empty.
/// Unlike the Part 1 script, these users have REAL BCrypt hashes so you can log in with them.
/// All demo accounts use the password: Password123!
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        var thandiwe = new AppUser { FullName = "Thandiwe Nkosi", Email = "thandiwe.organiser@raceday.co.za", PasswordHash = hash, Role = Roles.Organiser, PhoneNumber = "0821110001" };
        var johan = new AppUser { FullName = "Johan van der Merwe", Email = "johan.organiser@raceday.co.za", PasswordHash = hash, Role = Roles.Organiser, PhoneNumber = "0821110002" };
        var lindiwe = new AppUser { FullName = "Lindiwe Dube", Email = "lindiwe.participant@raceday.co.za", PasswordHash = hash, Role = Roles.Participant, PhoneNumber = "0821110003" };
        var sipho = new AppUser { FullName = "Sipho Mahlangu", Email = "sipho.participant@raceday.co.za", PasswordHash = hash, Role = Roles.Participant, PhoneNumber = "0821110004" };

        // Dates are relative to "now" so the demo always has one finished event and two upcoming ones.
        var today = DateTime.Today;

        var joburg10k = new Event
        {
            Organiser = thandiwe,
            Name = "Joburg City 10K",
            Description = "A fast, flat 10km road run through the Johannesburg CBD.",
            EventDate = today.AddDays(-30).AddHours(7),
            Location = "Johannesburg",
            DistanceKm = 10,
            EventType = EventTypes.Run
        };
        var sowetoWalk = new Event
        {
            Organiser = thandiwe,
            Name = "Soweto Community Walk",
            Description = "A family-friendly charity walk supporting local schools.",
            EventDate = today.AddDays(30).AddHours(8),
            Location = "Soweto",
            DistanceKm = 5,
            EventType = EventTypes.Walk
        };
        var winelandsTour = new Event
        {
            Organiser = johan,
            Name = "Cape Winelands Cycle Tour",
            Description = "A scenic road cycling tour through the Cape Winelands.",
            EventDate = today.AddDays(60).AddHours(6).AddMinutes(30),
            Location = "Stellenbosch",
            DistanceKm = 94.7m,
            EventType = EventTypes.Cycle
        };

        var open10k = new Category { Event = joburg10k, Name = "10km Open", MinAge = 18, DistanceKm = 10 };
        var under20 = new Category { Event = joburg10k, Name = "Under 20", MinAge = 13, MaxAge = 19, DistanceKm = 10 };
        var senior = new Category { Event = joburg10k, Name = "Senior (60+)", MinAge = 60, DistanceKm = 10 };
        var familyWalk = new Category { Event = sowetoWalk, Name = "5km Family Walk", DistanceKm = 5 };
        var individual = new Category { Event = winelandsTour, Name = "94.7km Individual", MinAge = 18, DistanceKm = 94.7m };
        var relay = new Category { Event = winelandsTour, Name = "94.7km Team Relay", MinAge = 18, DistanceKm = 94.7m };

        var routes = new[]
        {
            new EventRoute { Event = joburg10k, StartPoint = "Mary Fitzgerald Square", EndPoint = "Constitution Hill", ElevationGainM = 85, Notes = "Mostly flat with one incline near the finish." },
            new EventRoute { Event = sowetoWalk, StartPoint = "Soweto Theatre", EndPoint = "Freedom Square", ElevationGainM = 40, Notes = "Suitable for wheelchairs and prams." },
            new EventRoute { Event = winelandsTour, StartPoint = "Stellenbosch Square", EndPoint = "Franschhoek Pass", ElevationGainM = 1120, Notes = "Includes the Helshoogte Pass climb - bring water." }
        };

        var lindiwe10k = new Enrolment { Participant = lindiwe, Event = joburg10k, Category = open10k, Status = EnrolmentStatuses.Confirmed };
        var sipho10k = new Enrolment { Participant = sipho, Event = joburg10k, Category = under20, Status = EnrolmentStatuses.Confirmed };
        var lindiweTour = new Enrolment { Participant = lindiwe, Event = winelandsTour, Category = individual, Status = EnrolmentStatuses.Pending };

        var results = new[]
        {
            new RaceResult { Enrolment = lindiwe10k, FinishTime = new TimeSpan(0, 48, 32), FinishPosition = 47, TotalFinishers = 312 },
            new RaceResult { Enrolment = sipho10k, FinishTime = new TimeSpan(0, 52, 10), FinishPosition = 118, TotalFinishers = 312 }
        };

        db.Users.AddRange(thandiwe, johan, lindiwe, sipho);
        db.Events.AddRange(joburg10k, sowetoWalk, winelandsTour);
        db.Categories.AddRange(open10k, under20, senior, familyWalk, individual, relay);
        db.Routes.AddRange(routes);
        db.Enrolments.AddRange(lindiwe10k, sipho10k, lindiweTour);
        db.Results.AddRange(results);

        await db.SaveChangesAsync();
    }
}
