using Microsoft.Extensions.Logging;

namespace EventRecommender.Models;

public class Organizer
{
    public int OrganizerId { get; set; }
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
