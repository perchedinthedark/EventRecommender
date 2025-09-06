namespace EventRecommender.Models;

public class Friendship
{
    // one-way "follow": Follower -> Followed
    public int FollowerId { get; set; }
    public User Follower { get; set; } = default!;

    public int FollowedId { get; set; }
    public User Followed { get; set; } = default!;

    public DateTime Since { get; set; } = DateTime.UtcNow;
}
