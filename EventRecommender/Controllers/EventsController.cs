using EventRecommender.Data;
using EventRecommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EventRecommender.Controllers
{
    public class EventsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EventsController> _log;

        public EventsController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<EventsController> log)
        {
            _context = context;
            _userManager = userManager;
            _log = log;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Venue);

            return View(await appDbContext.ToListAsync());
        }

        // GET: Events/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var evt = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(m => m.EventId == id);

            if (evt == null) return NotFound();

            // read the logged-in user's existing interaction (to highlight buttons)
            var userId = _userManager.GetUserId(User);
            UserEventInteraction? my = null;

            if (!string.IsNullOrEmpty(userId))
            {
                my = await _context.UserEventInteractions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == evt.EventId);
            }

            ViewBag.MyStatus = my?.Status;   // for button highlight
            ViewBag.MyRating = my?.Rating;   // for rating display

            return View(evt);
        }


        // GET: /Events/TrackAndShow/5
        [HttpGet]
        public async Task<IActionResult> TrackAndShow(int id)
        {
            var userId = _userManager.GetUserId(User);
            _context.EventClicks.Add(new EventClick { UserId = userId, EventId = id });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        public async Task<IActionResult> Dwell([FromBody] DwellDto dto)
        {
            if (dto == null || dto.EventId <= 0) return Ok(); // swallow bad/untyped beacons

            var userId = _userManager.GetUserId(User); // null if anonymous
            _context.EventClicks.Add(new EventClick
            {
                UserId = userId,
                EventId = dto.EventId,
                DwellMs = dto.DwellMs,
                ClickedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Ok();
        }


        public record DwellDto(int EventId, int DwellMs);

        // GET: Events/Create
        public IActionResult Create()
        {
            // populate dropdowns
            ViewData["CategoryId"] = new SelectList(_context.Categories.AsNoTracking().OrderBy(c => c.Name), "CategoryId", "Name");
            ViewData["OrganizerId"] = new SelectList(_context.Organizers.AsNoTracking().OrderBy(o => o.Name), "OrganizerId", "Name");
            ViewData["VenueId"] = new SelectList(_context.Venues.AsNoTracking().OrderBy(v => v.Name), "VenueId", "Name");
            return View();
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventId,Title,Description,DateTime,Location,CategoryId,VenueId,OrganizerId")] Event @event)
        {
            _log.LogInformation("POST /Events/Create hit. Title={Title}, DateTime={DateTime}, CatId={CatId}, VenueId={VenueId}, OrgId={OrgId}",
                @event?.Title, @event?.DateTime, @event?.CategoryId, @event?.VenueId, @event?.OrganizerId);

            if (!ModelState.IsValid)
            {
                _log.LogWarning("ModelState invalid on Create: {Errors}", DumpModelStateErrors());

                ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event?.CategoryId);
                ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event?.OrganizerId);
                ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event?.VenueId);
                return View(@event);
            }

            try
            {
                _context.Add(@event);
                await _context.SaveChangesAsync();
                _log.LogInformation("Event created OK with EventId={Id}", @event.EventId);
                TempData["Flash"] = "Event created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error saving Event on Create.");
                ModelState.AddModelError(string.Empty, "Unexpected error while saving the event.");
                ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event?.CategoryId);
                ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event?.OrganizerId);
                ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event?.VenueId);
                return View(@event);
            }
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
            ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
            return View(@event);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventId,Title,Description,DateTime,Location,CategoryId,VenueId,OrganizerId")] Event @event)
        {
            if (id != @event.EventId) return NotFound();

            if (!ModelState.IsValid)
            {
                _log.LogWarning("ModelState invalid on Edit: {Errors}", DumpModelStateErrors());
                ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
                ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
                ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
                return View(@event);
            }

            try
            {
                _context.Update(@event);
                await _context.SaveChangesAsync();
                _log.LogInformation("Event updated EventId={Id}", @event.EventId);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(@event.EventId)) return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error saving Event on Edit.");
                ModelState.AddModelError(string.Empty, "Unexpected error while saving the event.");
                ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
                ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
                ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
                return View(@event);
            }
        }

        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var evt = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(m => m.EventId == id);

            if (evt == null) return NotFound();

            return View(evt);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event != null) _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Events/MarkInteraction (Interested/Going)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkInteraction(int eventId, string status)
        {
            if (!Enum.TryParse<InteractionStatus>(status, ignoreCase: true, out var parsedStatus))
                parsedStatus = InteractionStatus.None;

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var interaction = await _context.UserEventInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == eventId);

            if (interaction == null)
            {
                interaction = new UserEventInteraction
                {
                    UserId = userId,
                    EventId = eventId,
                    Status = parsedStatus,
                    Timestamp = DateTime.UtcNow
                };
                _context.UserEventInteractions.Add(interaction);
            }
            else
            {
                interaction.Status = parsedStatus;
                interaction.Timestamp = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // POST: Events/SetRating (optional)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRating(int eventId, int rating)
        {
            if (rating < 1 || rating > 5) return BadRequest("Rating must be 1-5.");

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var interaction = await _context.UserEventInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == eventId);

            if (interaction == null)
            {
                interaction = new UserEventInteraction
                {
                    UserId = userId,
                    EventId = eventId,
                    Status = InteractionStatus.None,
                    Rating = rating,
                    Timestamp = DateTime.UtcNow
                };
                _context.UserEventInteractions.Add(interaction);
            }
            else
            {
                interaction.Rating = rating;
                interaction.Timestamp = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // POST: Events/RecordView (optional implicit signal)
        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RecordView(int eventId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Ok();

            var interaction = await _context.UserEventInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == eventId);

            if (interaction == null)
            {
                _context.UserEventInteractions.Add(new UserEventInteraction
                {
                    UserId = userId,
                    EventId = eventId,
                    Status = InteractionStatus.None,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                interaction.Timestamp = DateTime.UtcNow; // keep explicit status, just bump last-seen
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private bool EventExists(int id) => _context.Events.Any(e => e.EventId == id);

        private string DumpModelStateErrors()
        {
            var errs = ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value!.Errors.Select(e => e.ErrorMessage))}");
            return string.Join(" || ", errs);
        }

    }
}
