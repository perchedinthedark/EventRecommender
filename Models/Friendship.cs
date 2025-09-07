namespace EventRecommender.Models
{
    public class Friendship
    {
        public int Id { get; set; }

        // Foreign keys
        public string FollowerId { get; set; } = default!; // who follows
        public string FolloweeId { get; set; } = default!; // who is followed

        // Navigation
        public ApplicationUser Follower { get; set; } = default!;
        public ApplicationUser Followee { get; set; } = default!;
    }
}
