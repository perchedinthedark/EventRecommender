namespace EventRecommender.Models
{
    public class RecommendationLog
    {
        public int Id { get; set; }

        // Foreign keys
        public string UserId { get; set; } = default!;
        public int EventId { get; set; }

        // Navigation
        public ApplicationUser User { get; set; } = default!;
        public Event Event { get; set; } = default!;

        public DateTime RecommendedAt { get; set; } = DateTime.UtcNow;
    }
}

