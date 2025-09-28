using Microsoft.Extensions.Logging;

namespace EventRecommender.Models;

public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = default!;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
