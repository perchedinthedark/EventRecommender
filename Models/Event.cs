using System.ComponentModel.DataAnnotations;

namespace EventRecommender.Models;

public class Event
{
    public int EventId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime DateTime { get; set; }

    public string? Location { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    public int OrganizerId { get; set; }
    public Organizer? Organizer { get; set; }

    // navs
    public ICollection<UserEventInteraction> Interactions { get; set; } = new List<UserEventInteraction>();
    public ICollection<RecommendationLog> Recommendations { get; set; } = new List<RecommendationLog>();
}
