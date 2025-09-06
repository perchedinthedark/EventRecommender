using EventRecommender.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace EventRecommender.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
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

        // User
        modelBuilder.Entity<User>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
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

        // UserEventInteraction (surrogate key so we can keep history)
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
            b.HasKey(x => new { x.FollowerId, x.FollowedId });

            b.HasOne(x => x.Follower)
             .WithMany(u => u.Following)
             .HasForeignKey(x => x.FollowerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Followed)
             .WithMany(u => u.Followers)
             .HasForeignKey(x => x.FollowedId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RecommendationLog
        modelBuilder.Entity<RecommendationLog>(b =>
        {
            b.HasKey(x => x.RecommendationId);

            b.HasOne(x => x.User)
             .WithMany(u => u.Recommendations)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
             .WithMany(e => e.Recommendations)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.UserId, x.EventId, x.DateGenerated });
        });
    }
}
