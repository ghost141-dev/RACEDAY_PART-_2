namespace RaceDay.Api.Models;

/// <summary>Maps to the Routes table (named EventRoute to avoid clashing with routing types).</summary>
public class EventRoute
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public decimal? ElevationGainM { get; set; }
    public string? RouteMapUrl { get; set; }
    public string? Notes { get; set; }
}
