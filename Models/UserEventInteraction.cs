namespace EventRecommender.Models
{
    public class UserEventInteraction
    {
        public int Id { get; set; }

        // Foreign keys
        public string UserId { get; set; } = default!;
        public int EventId { get; set; }

        // Navigation
        public ApplicationUser User { get; set; } = default!;
        public Event Event { get; set; } = default!;

        public InteractionStatus Status { get; set; }  // None, Interested, Going
        public int? Rating { get; set; }               // optional 1..5
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum InteractionStatus { None = 0, Interested = 1, Going = 2 }
}

