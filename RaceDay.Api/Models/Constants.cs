namespace RaceDay.Api.Models;

internal static class Normaliser
{
    public static string? Match(string? value, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return allowed.FirstOrDefault(a => a.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static string? Normalise(string? value) => Normaliser.Match(value, Organiser, Participant);
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";

    public static string? Normalise(string? value) => Normaliser.Match(value, Run, Walk, Cycle);
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";

    public static string? Normalise(string? value) => Normaliser.Match(value, Pending, Confirmed);
}
