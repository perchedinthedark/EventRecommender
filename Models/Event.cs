namespace EventRecommender.Models;

public class Event
{
    public int EventId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime DateTime { get; set; }
    public string? Location { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public int VenueId { get; set; }
    public Venue Venue { get; set; } = default!;

    public int OrganizerId { get; set; }
    public Organizer Organizer { get; set; } = default!;

    // navs
    public ICollection<UserEventInteraction> Interactions { get; set; } = new List<UserEventInteraction>();
    public ICollection<RecommendationLog> Recommendations { get; set; } = new List<RecommendationLog>();
}
