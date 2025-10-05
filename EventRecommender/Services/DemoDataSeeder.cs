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
            var now = DateTime.UtcNow;

           
            // ---------- Users (idempotent + set DisplayName) ----------
            var usersSeed = new (string Email, string Display)[]
            {
    ("alice@example.com", "Alice"),
    ("bob@example.com",   "Bob"),
    ("carol@example.com", "Carol"),
            };

            foreach (var (email, display) in usersSeed)
            {
                var u = await _um.FindByEmailAsync(email);
                if (u == null)
                {
                    var safeHandle = email.Split('@')[0];
                    var baseHandle = safeHandle;
                    int n = 1;
                    while (await _um.FindByNameAsync(safeHandle) != null)
                        safeHandle = $"{baseHandle}{++n}";

                    u = new ApplicationUser { UserName = safeHandle, Email = email, EmailConfirmed = true, DisplayName = display };
                    await _um.CreateAsync(u, "Demo!12345");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(u.DisplayName)) { u.DisplayName = display; }
                    // Optional: if legacy had UserName = Email, normalize to a safe handle once.
                    if (u.UserName == email) { u.UserName = email.Split('@')[0]; }
                    await _um.UpdateAsync(u);
                }
            }

            var alice = (await _um.FindByEmailAsync("alice@example.com"))!;
            var bob = (await _um.FindByEmailAsync("bob@example.com"))!;
            var carol = (await _um.FindByEmailAsync("carol@example.com"))!;

            // ---------- Taxonomy (idempotent) ----------
            async Task<int> EnsureCategory(string name)
            {
                var c = await _db.Categories.FirstOrDefaultAsync(x => x.Name == name);
                if (c == null) { c = new Category { Name = name }; _db.Categories.Add(c); await _db.SaveChangesAsync(); }
                return c.CategoryId;
            }
            async Task<int> EnsureVenue(string name, string addr, int cap)
            {
                var v = await _db.Venues.FirstOrDefaultAsync(x => x.Name == name);
                if (v == null) { v = new Venue { Name = name, Address = addr, Capacity = cap }; _db.Venues.Add(v); await _db.SaveChangesAsync(); }
                return v.VenueId;
            }
            async Task<int> EnsureOrganizer(string name, string? contact = null)
            {
                var o = await _db.Organizers.FirstOrDefaultAsync(x => x.Name == name);
                if (o == null) { o = new Organizer { Name = name, ContactInfo = contact }; _db.Organizers.Add(o); await _db.SaveChangesAsync(); }
                return o.OrganizerId;
            }

            var catMusic = await EnsureCategory("Music");
            var catTech = await EnsureCategory("Tech");
            var catSport = await EnsureCategory("Sports");

            var vHall = await EnsureVenue("City Hall", "Main 1", 800);
            var vHub = await EnsureVenue("Tech Hub", "Innov 42", 300);
            var vStad = await EnsureVenue("Stadium A", "Arena 10", 15000);
            var vCenter = await EnsureVenue("Community Center", "Oak 55", 500);
            var vPark = await EnsureVenue("Open Air Park", "Riverside", 5000);

            var oLive = await EnsureOrganizer("LiveNation");
            var oCode = await EnsureOrganizer("CodeWorks");
            var oAth = await EnsureOrganizer("AthletiCo");
            var oCity = await EnsureOrganizer("CityCulture");
            var oGuild = await EnsureOrganizer("DevGuild");

            // ---------- Events (idempotent by Title) ----------
            async Task AddOrUpdateEvent(
                string title, string desc, int daysFromNow, string loc,
                int catId, int venueId, int orgId, string? imageUrl = null)
            {
                var e = await _db.Events.FirstOrDefaultAsync(x => x.Title == title);
                if (e == null)
                {
                    _db.Events.Add(new Event
                    {
                        Title = title,
                        Description = desc,
                        DateTime = now.AddDays(daysFromNow),
                        Location = loc,
                        CategoryId = catId,
                        VenueId = venueId,
                        OrganizerId = orgId,
                        ImageUrl = imageUrl
                    });
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // If the row exists but ImageUrl is empty and we have one, backfill
                    if (string.IsNullOrWhiteSpace(e.ImageUrl) && !string.IsNullOrWhiteSpace(imageUrl))
                    {
                        e.ImageUrl = imageUrl;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            // Example placeholder images (swap for your own anytime)
            // Using picsum is fine for demo; these calls are just URLs stored in DB.
            await AddOrUpdateEvent("Jazz Night", "Smooth sets", +3, "City", catMusic, vHall, oLive, "https://picsum.photos/seed/jazz/1200/600");
            await AddOrUpdateEvent("Rock Fest", "Guitars!", +10, "City", catMusic, vHall, oLive, "https://picsum.photos/seed/rock/1200/600");
            await AddOrUpdateEvent("AI Summit", "Talks+expo", +5, "Tech", catTech, vHub, oCode, "https://picsum.photos/seed/ai/1200/600");
            await AddOrUpdateEvent("Hack Night", "Code jam", +1, "Tech", catTech, vHub, oCode, "https://picsum.photos/seed/hack/1200/600");
            await AddOrUpdateEvent("Derby Match", "Big game", +7, "Stad", catSport, vStad, oAth, "https://picsum.photos/seed/derby/1200/600");

            await AddOrUpdateEvent("Symphony Gala", "Orchestra evening", +14, "City", catMusic, vHall, oCity, "https://picsum.photos/seed/symphony/1200/600");
            await AddOrUpdateEvent("Indie Jam", "Local bands", +4, "Park", catMusic, vPark, oLive, "https://picsum.photos/seed/indie/1200/600");
            await AddOrUpdateEvent("Cloud Expo", "Cloud & DevOps", +12, "Tech", catTech, vCenter, oGuild, "https://picsum.photos/seed/cloud/1200/600");
            await AddOrUpdateEvent("AI Workshop", "Hands-on ML", +8, "Tech", catTech, vHub, oCode, "https://picsum.photos/seed/workshop/1200/600");
            await AddOrUpdateEvent("Data Night", "Talks + networking", +2, "Tech", catTech, vCenter, oGuild, "https://picsum.photos/seed/data/1200/600");
            await AddOrUpdateEvent("City Run 10K", "Community race", +9, "Park", catSport, vPark, oAth, "https://picsum.photos/seed/run/1200/600");
            await AddOrUpdateEvent("Championship Final", "Season climax", +20, "Stad", catSport, vStad, oAth, "https://picsum.photos/seed/final/1200/600");

            // Refresh events cache after inserts/updates
            var events = await _db.Events.AsNoTracking().ToListAsync();

            // ---------- Interactions (idempotent) ----------
            void Intx(ApplicationUser u, string title, InteractionStatus s, int? rating = null)
            {
                var e = events.FirstOrDefault(x => x.Title == title);
                if (e == null) return;

                if (!_db.UserEventInteractions.Any(x => x.UserId == u.Id && x.EventId == e.EventId))
                {
                    _db.UserEventInteractions.Add(new UserEventInteraction
                    {
                        UserId = u.Id,
                        EventId = e.EventId,
                        Status = s,
                        Rating = rating,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Spread ~40+ interactions across users
            // Alice
            Intx(alice, "AI Summit", InteractionStatus.Interested, 5);
            Intx(alice, "Hack Night", InteractionStatus.Going, 4);
            Intx(alice, "Jazz Night", InteractionStatus.Interested, 3);
            Intx(alice, "Cloud Expo", InteractionStatus.Interested, 4);
            Intx(alice, "AI Workshop", InteractionStatus.Going, 5);
            Intx(alice, "Data Night", InteractionStatus.Interested, 4);
            Intx(alice, "Indie Jam", InteractionStatus.Interested, 3);

            // Bob
            Intx(bob, "Rock Fest", InteractionStatus.Going, 5);
            Intx(bob, "Jazz Night", InteractionStatus.Interested, 4);
            Intx(bob, "Derby Match", InteractionStatus.Interested, 2);
            Intx(bob, "City Run 10K", InteractionStatus.Going, 4);
            Intx(bob, "Championship Final", InteractionStatus.Interested, 5);
            Intx(bob, "Indie Jam", InteractionStatus.Going, 4);
            Intx(bob, "Symphony Gala", InteractionStatus.Interested, 3);

            // Carol
            Intx(carol, "AI Summit", InteractionStatus.Interested, 5);
            Intx(carol, "Derby Match", InteractionStatus.Going, 4);
            Intx(carol, "Cloud Expo", InteractionStatus.Interested, 4);
            Intx(carol, "AI Workshop", InteractionStatus.Interested, 5);
            Intx(carol, "Data Night", InteractionStatus.Going, 5);
            Intx(carol, "Symphony Gala", InteractionStatus.Interested, 4);
            Intx(carol, "Rock Fest", InteractionStatus.Interested, 4);

            // Cross-category extras
            Intx(alice, "City Run 10K", InteractionStatus.Interested, 3);
            Intx(alice, "Championship Final", InteractionStatus.Interested, 4);
            Intx(bob, "AI Workshop", InteractionStatus.Interested, 3);
            Intx(bob, "Data Night", InteractionStatus.Interested, 4);
            Intx(carol, "Indie Jam", InteractionStatus.Interested, 3);

            await _db.SaveChangesAsync();

            // ---------- Click + Dwell samples (idempotent-ish) ----------
            void Click(ApplicationUser u, string title, int dwellMs, int minutesAgo)
            {
                var e = events.FirstOrDefault(x => x.Title == title);
                if (e == null) return;

                _db.EventClicks.Add(new EventClick
                {
                    UserId = u.Id,
                    EventId = e.EventId,
                    ClickedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
                    DwellMs = dwellMs
                });
            }

            Click(alice, "AI Summit", 4200, 5);
            Click(alice, "AI Workshop", 3600, 12);
            Click(bob, "Rock Fest", 5200, 8);
            Click(bob, "Indie Jam", 1800, 3);
            Click(carol, "Data Night", 2500, 20);
            Click(carol, "Symphony Gala", 3000, 15);

            await _db.SaveChangesAsync();

            return (await _db.Users.CountAsync(),
                    await _db.Events.CountAsync(),
                    await _db.UserEventInteractions.CountAsync());
        }
    }
}

