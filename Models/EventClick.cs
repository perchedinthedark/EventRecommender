using System;

namespace EventRecommender.Models
{
    public class EventClick
    {
        public int Id { get; set; }
        public string? UserId { get; set; }   // null if anonymous
        public int EventId { get; set; }
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public int? DwellMs { get; set; }     // optional follow-up
    }
}
