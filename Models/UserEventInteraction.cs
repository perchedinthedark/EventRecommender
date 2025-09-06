namespace EventRecommender.Models;

public class UserEventInteraction
{
    public int Id { get; set; } // surrogate key so we can store multiple interactions over time
    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public int EventId { get; set; }
    public Event Event { get; set; } = default!;

    public bool IsInterested { get; set; }
    public bool IsGoing { get; set; }
    public int? Rating { get; set; } // 1..5 optional
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
