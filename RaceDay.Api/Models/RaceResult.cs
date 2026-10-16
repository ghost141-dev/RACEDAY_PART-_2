namespace RaceDay.Api.Models;

/// <summary>Maps to the Results table. One-to-one with Enrolment.</summary>
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public int TotalFinishers { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}
