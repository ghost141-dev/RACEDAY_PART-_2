namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser Organiser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = EventTypes.Run;
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<EventRoute> Routes { get; set; } = new List<EventRoute>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
