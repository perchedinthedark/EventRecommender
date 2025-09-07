using Microsoft.AspNetCore.Identity;

namespace EventRecommender.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Custom fields
        public string? Location { get; set; }

        // Navigation properties
        public ICollection<UserEventInteraction> Interactions { get; set; } = new List<UserEventInteraction>();
        public ICollection<Friendship> Following { get; set; } = new List<Friendship>();
        public ICollection<Friendship> Followers { get; set; } = new List<Friendship>();
        public ICollection<RecommendationLog> Recommendations { get; set; } = new List<RecommendationLog>();

    }
}