namespace EventRecommender.Models;

public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Location { get; set; }

    // navs
    public ICollection<UserEventInteraction> Interactions { get; set; } = new List<UserEventInteraction>();
    public ICollection<Friendship> Following { get; set; } = new List<Friendship>(); // users I follow
    public ICollection<Friendship> Followers { get; set; } = new List<Friendship>(); // users who follow me
    public ICollection<RecommendationLog> Recommendations { get; set; } = new List<RecommendationLog>();
}
