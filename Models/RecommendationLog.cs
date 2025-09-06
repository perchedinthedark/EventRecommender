namespace EventRecommender.Models;

public class RecommendationLog
{
    public int RecommendationId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public int EventId { get; set; }
    public Event Event { get; set; } = default!;

    public float Score { get; set; } // probability or predicted rating
    public DateTime DateGenerated { get; set; } = DateTime.UtcNow;
}

