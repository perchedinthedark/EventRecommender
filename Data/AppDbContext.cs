using EventRecommender.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Organizer> Organizers => Set<Organizer>();
    public DbSet<UserEventInteraction> UserEventInteractions => Set<UserEventInteraction>();
    public DbSet<RecommendationLog> RecommendationLogs => Set<RecommendationLog>();
    public DbSet<Friendship> Friendships => Set<Friendship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser (extended Identity user)
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.Property(x => x.Location).HasMaxLength(200);
        });

        // Category
        modelBuilder.Entity<Category>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        // Venue
        modelBuilder.Entity<Venue>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Address).HasMaxLength(300);
        });

        // Organizer
        modelBuilder.Entity<Organizer>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.ContactInfo).HasMaxLength(300);
        });

        // Event
        modelBuilder.Entity<Event>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();

            b.HasOne(x => x.Category)
             .WithMany(c => c.Events)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Venue)
             .WithMany(v => v.Events)
             .HasForeignKey(x => x.VenueId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Organizer)
             .WithMany(o => o.Events)
             .HasForeignKey(x => x.OrganizerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.DateTime);
        });

        // UserEventInteraction
        modelBuilder.Entity<UserEventInteraction>(b =>
        {
            b.HasKey(x => x.Id);

            b.HasOne(x => x.User)
             .WithMany(u => u.Interactions)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            
            b.HasOne(x => x.Event)
             .WithMany(e => e.Interactions)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.UserId, x.EventId });
        });

        // Friendship (one-way follow)
        modelBuilder.Entity<Friendship>(b =>
        {
            b.HasKey(x => new { x.FollowerId, x.FolloweeId });

            b.HasOne(x => x.Follower)
             .WithMany(u => u.Following)
             .HasForeignKey(x => x.FollowerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Followee)
             .WithMany(u => u.Followers)
             .HasForeignKey(x => x.FolloweeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RecommendationLog
        modelBuilder.Entity<RecommendationLog>(b =>
        {
            b.HasKey(x => x.Id);

            b.HasOne(x => x.User)
             .WithMany(u => u.Recommendations)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
             .WithMany(e => e.Recommendations)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.UserId, x.EventId, x.RecommendedAt });
        });
    }
}

