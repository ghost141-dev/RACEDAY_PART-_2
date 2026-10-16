# RaceDay – Part 2: RESTful API (PROG6212/w)

ASP.NET Core 8 Web API for the RaceDay event management system. It follows the Part 1 endpoint plan, uses
EF Core Code-First on SQL Server, server-side session authentication with two roles, Swagger, and xUnit tests
that run in GitHub Actions.

## Roles

| Role | Can do |
|------|--------|
| **Organiser** | Create / edit / delete their own events, upload event banners, manage categories, view enrolments for their events, confirm enrolments, capture and correct results |
| **Participant** | Browse events, enrol in an event under a category, view / cancel their own enrolments, view their own results |
| Both | Register, log in / out, view and update their profile, upload a profile picture |

Organiser registration needs a secret **organiser code** (`Registration:OrganiserCode` in `appsettings.json`) so that
nobody can simply choose the Organiser role.

## Project layout

```
RaceDay.sln
RaceDay.Api/          Controllers, Models, Data (DbContext + seeder), DTOs, Auth, Services, Migrations
RaceDay.Api.Tests/    xUnit tests (in-memory database)
.github/workflows/part2-api.yml   CI: restore, build, test on every push
```

## Setup and run

1. Install Visual Studio 2022 with the ASP.NET workload (includes SQL Server LocalDB) and the .NET 8 SDK.
2. Check the LocalDB instance exists: `sqllocaldb info` should list `PROG_6212`
   (create it with `sqllocaldb create PROG_6212` and `sqllocaldb start PROG_6212` if not).
3. The connection string is in `RaceDay.Api/appsettings.json`:
   `Data source=(localdb)\PROG_6212;database=RaceDayDb;Integrated Security=True;TrustServerCertificate=True;`
   If you already created `RaceDayDb` with your Part 1 script on this instance, drop it first or change the database name.
4. Open `RaceDay.sln`. The `InitialCreate` migration is already included in `RaceDay.Api/Migrations`.
   In **Package Manager Console** (Default project: `RaceDay.Api`) run:
   ```
   Update-Database
   ```
   (The API also applies pending migrations automatically when it starts.)
5. Set `RaceDay.Api` as the startup project and press F5. Swagger opens at `http://localhost:5080/swagger`.
6. In Development the database is seeded with demo data (2 Organisers, 2 Participants, 3 Events, categories, routes, enrolments, results).
   All demo accounts use the password `Password123!`, e.g. `thandiwe.organiser@raceday.co.za` and `lindiwe.participant@raceday.co.za`.

### Using Swagger
Call `POST /api/auth/login` first. The browser stores the session cookie and sends it on later calls, so protected endpoints work from the Swagger UI.

## Authentication design

* Passwords are hashed with BCrypt and never returned.
* On login the API stores `UserId` and `Role` in a **server-side session** (`AddSession`, 30-minute idle timeout).
* `[SessionAuthorize]` (any logged-in user) and `[SessionAuthorize(Roles.Organiser)]` / `[SessionAuthorize(Roles.Participant)]`
  protect endpoints: **401** when there is no session, **403** when the role is wrong.
* Organisers can only modify their own events, categories, enrolments and results (403 otherwise).

## Running the tests

```
dotnet test RaceDay.sln
```

Tests use the real API pipeline with an isolated EF Core in-memory database, so they need no SQL Server and run unchanged on GitHub Actions.
They cover registration, login/logout, hashing, event management, role rejection in both directions, categories, enrolments and results.

## Endpoints

| Method | Route | Role |
|--------|-------|------|
| POST | /api/auth/register | None |
| POST | /api/auth/login | None |
| POST | /api/auth/logout | Any |
| GET / PUT | /api/profile | Any |
| POST | /api/profile/picture | Any |
| GET | /api/events (filters: `type`, `location`) | None |
| GET | /api/events/mine | Organiser |
| GET | /api/events/{id} | None |
| POST | /api/events | Organiser |
| PUT / DELETE | /api/events/{id} | Organiser (owner) |
| POST | /api/events/{id}/banner | Organiser (owner) |
| GET | /api/events/{eventId}/categories | None |
| POST | /api/events/{eventId}/categories | Organiser (owner) |
| PUT / DELETE | /api/categories/{id} | Organiser (owner) |
| POST | /api/events/{eventId}/enrolments | Participant |
| GET | /api/events/{eventId}/enrolments | Organiser (owner) |
| GET | /api/enrolments/mine | Participant |
| PUT | /api/enrolments/{id}/status | Organiser (owner) |
| DELETE | /api/enrolments/{id} | Participant (owner) |
| POST | /api/enrolments/{enrolmentId}/results | Organiser (owner) |
| PUT | /api/results/{id} | Organiser (owner) |
| GET | /api/results/mine | Participant |
| GET | /api/events/{eventId}/results | Organiser (owner) |

## Differences from the Part 1 plan

1. `POST /api/auth/register` accepts an extra optional field `organiserCode` and returns **403** if an Organiser registers without a valid code.
2. **Added** `GET /api/events/mine` (Organiser): the Part 3 dashboard needs the Organiser's own events, including past ones.
3. **Added** `PUT /api/enrolments/{id}/status` (Organiser): confirms or reverts an enrolment (Pending / Confirmed).
4. `POST /api/events/{eventId}/enrolments` also returns **400** if the event has already taken place.
5. `POST /api/enrolments/{enrolmentId}/results` also returns **400** if the event has not taken place yet.
6. Banner and profile images are saved to `wwwroot/uploads` in Part 2 behind `IFileStorageService`; Part 3 swaps in Azure Blob Storage.
7. Database: all foreign keys are `NO ACTION` as in the SQL script, so deleting an event removes its categories, routes, enrolments and results explicitly in one `SaveChanges`.

## CI/CD

`.github/workflows/part2-api.yml` restores, builds and runs all tests on every push.

<img width="1503" height="315" alt="Green CI build" src="https://github.com/user-attachments/assets/1a9940e2-25dc-4d53-a720-c2e54b3c0fa0" />

## Video

Unlisted YouTube walkthrough of the running API, code structure, endpoints, authentication, Swagger and unit tests:

**REPLACE THIS LINE WITH YOUR UNLISTED VIDEO LINK (https://www.youtube.com/watch?v=...)**

## AI usage disclosure

I used Claude (Anthropic) as a review and learning tool on this project. I asked it to review my Part 2 work
against the marking rubric, identify what was missing or incorrect (for example the CI/CD workflow setup,
Swagger documentation, EF Core migrations and README requirements), and explain how to resolve each issue.
It also helped me work out which additional classes I still needed to create, and guided me through the work
in Visual Studio. The GitHub Actions workflow file is based on a version suggested by Claude, and Claude
explained the EF Core migration commands, which I ran myself. I used its explanations to understand the
problems and applied the changes myself.
