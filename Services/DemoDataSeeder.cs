using System;
using System.Linq;
using System.Threading.Tasks;
using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventRecommender.Services
{
    public class DemoDataSeeder
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public DemoDataSeeder(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db; _um = um;
        }

        public async Task<(int users, int events_, int interactions)> SeedAsync()
        {
            // ---------- Users ----------
            var emails = new[] { "alice@example.com", "bob@example.com", "carol@example.com" };
            foreach (var e in emails)
            {
                if (await _um.FindByEmailAsync(e) == null)
                {
                    var u = new ApplicationUser { UserName = e, Email = e, EmailConfirmed = true };
                    var res = await _um.CreateAsync(u, "Demo!12345");
                    if (!res.Succeeded)
                        throw new InvalidOperationException("Failed to create demo user: " +
                            string.Join(", ", res.Errors.Select(x => x.Description)));
                }
            }
            var alice = (await _um.FindByEmailAsync("alice@example.com"))!;
            var bob = (await _um.FindByEmailAsync("bob@example.com"))!;
            var carol = (await _um.FindByEmailAsync("carol@example.com"))!;

            // ---------- Taxonomy (upsert-ish) ----------
            async Task<int> EnsureCategoryAsync(string name)
            {
                var id = await _db.Categories.Where(c => c.Name == name).Select(c => c.CategoryId).FirstOrDefaultAsync();
                if (id == 0)
                {
                    var c = new Category { Name = name };
                    _db.Categories.Add(c);
                    await _db.SaveChangesAsync();
                    id = c.CategoryId;
                }
                return id;
            }

            async Task<int> EnsureVenueAsync(string name, string address, int capacity)
            {
                var id = await _db.Venues.Where(v => v.Name == name).Select(v => v.VenueId).FirstOrDefaultAsync();
                if (id == 0)
                {
                    var v = new Venue { Name = name, Address = address, Capacity = capacity };
                    _db.Venues.Add(v);
                    await _db.SaveChangesAsync();
                    id = v.VenueId;
                }
                return id;
            }

            async Task<int> EnsureOrganizerAsync(string name, string? contact = null)
            {
                var id = await _db.Organizers.Where(o => o.Name == name).Select(o => o.OrganizerId).FirstOrDefaultAsync();
                if (id == 0)
                {
                    var o = new Organizer { Name = name, ContactInfo = contact };
                    _db.Organizers.Add(o);
                    await _db.SaveChangesAsync();
                    id = o.OrganizerId;
                }
                return id;
            }

            var catMusic = await EnsureCategoryAsync("Music");
            var catTech = await EnsureCategoryAsync("Tech");
            var catSport = await EnsureCategoryAsync("Sports");

            var venHall = await EnsureVenueAsync("City Hall", "Main 1", 800);
            var venHub = await EnsureVenueAsync("Tech Hub", "Innov 42", 300);
            var venStad = await EnsureVenueAsync("Stadium A", "Arena 10", 15000);

            var orgLive = await EnsureOrganizerAsync("LiveNation");
            var orgCode = await EnsureOrganizerAsync("CodeWorks");
            var orgAth = await EnsureOrganizerAsync("AthletiCo");

            // ---------- Events (upsert by Title) ----------
            var now = DateTime.UtcNow;

            async Task<int> EnsureEventAsync(
                string title, string desc, int daysFromNow,
                int categoryId, int venueId, int organizerId, string location = "City")
            {
                var evt = await _db.Events.FirstOrDefaultAsync(e => e.Title == title);
                if (evt == null)
                {
                    evt = new Event
                    {
                        Title = title,
                        Description = desc,
                        DateTime = now.AddDays(daysFromNow),
                        Location = location,
                        CategoryId = categoryId,
                        VenueId = venueId,
                        OrganizerId = organizerId
                    };
                    _db.Events.Add(evt);
                    await _db.SaveChangesAsync();
                }
                return evt.EventId;
            }

            await EnsureEventAsync("Jazz Night", "Smooth sets", 3, catMusic, venHall, orgLive, "City");
            await EnsureEventAsync("Rock Fest", "Guitars!", 10, catMusic, venHall, orgLive, "City");
            await EnsureEventAsync("AI Summit", "Talks+expo", 5, catTech, venHub, orgCode, "Tech");
            await EnsureEventAsync("Hack Night", "Code jam", 1, catTech, venHub, orgCode, "Tech");
            await EnsureEventAsync("Derby Match", "Big game", 7, catSport, venStad, orgAth, "Stad");

            // ---------- Interactions (lookup by title each time) ----------
            async Task IntxAsync(ApplicationUser u, string title, InteractionStatus s, int? rating = null)
            {
                var eid = await _db.Events.Where(x => x.Title == title).Select(x => x.EventId).FirstOrDefaultAsync();
                if (eid == 0) throw new InvalidOperationException($"Seed error: event '{title}' not found.");

                var row = await _db.UserEventInteractions.FirstOrDefaultAsync(x => x.UserId == u.Id && x.EventId == eid);
                if (row == null)
                {
                    _db.UserEventInteractions.Add(new UserEventInteraction
                    {
                        UserId = u.Id,
                        EventId = eid,
                        Status = s,
                        Rating = rating,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    row.Status = s;
                    row.Rating = rating;
                    row.Timestamp = DateTime.UtcNow;
                }
            }

            // Core set (~8–10)
            await IntxAsync(alice, "AI Summit", InteractionStatus.Interested, 5);
            await IntxAsync(alice, "Hack Night", InteractionStatus.Going, 4);
            await IntxAsync(alice, "Jazz Night", InteractionStatus.Interested, 3);

            await IntxAsync(bob, "Rock Fest", InteractionStatus.Going, 5);
            await IntxAsync(bob, "Jazz Night", InteractionStatus.Interested, 4);
            await IntxAsync(bob, "Derby Match", InteractionStatus.Interested, 2);

            await IntxAsync(carol, "AI Summit", InteractionStatus.Interested, 5);
            await IntxAsync(carol, "Derby Match", InteractionStatus.Going, 4);

            // Extra to comfortably exceed MF threshold (aim ≥ 20)
            await IntxAsync(alice, "Rock Fest", InteractionStatus.Interested, 4);
            await IntxAsync(alice, "Derby Match", InteractionStatus.Interested, 3);

            await IntxAsync(bob, "AI Summit", InteractionStatus.Interested, 4);
            await IntxAsync(bob, "Hack Night", InteractionStatus.Interested, 5);

            await IntxAsync(carol, "Jazz Night", InteractionStatus.Interested, 3);
            await IntxAsync(carol, "Rock Fest", InteractionStatus.Interested, 4);
            await IntxAsync(carol, "Hack Night", InteractionStatus.Interested, 5);

            await _db.SaveChangesAsync();

            return (3, await _db.Events.CountAsync(), await _db.UserEventInteractions.CountAsync());
        }
    }
}

