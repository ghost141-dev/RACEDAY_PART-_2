namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public AppUser Participant { get; set; } = null!;
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Status { get; set; } = EnrolmentStatuses.Pending;
    public DateTime EnrolledAt { get; set; } = DateTime.Now;

    public RaceResult? Result { get; set; }
}
