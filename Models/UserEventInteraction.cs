namespace EventRecommender.Models
{
    public class UserEventInteraction
    {
        public int Id { get; set; }

        // Foreign keys
        public string UserId { get; set; } = default!; // Identity uses string keys
        public int EventId { get; set; }

        // Navigation
        public ApplicationUser User { get; set; } = default!;
        public Event Event { get; set; } = default!;

        public string InteractionType { get; set; } = default!; // e.g. "Interested", "Attending"
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

