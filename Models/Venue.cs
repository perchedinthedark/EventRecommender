using Microsoft.Extensions.Logging;

namespace EventRecommender.Models;

public class Venue
{
    public int VenueId { get; set; }
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public int? Capacity { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

