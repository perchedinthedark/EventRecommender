using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EventRecommender.Data;
using EventRecommender.Models;

namespace EventRecommender.Controllers
{
    public class EventsController : Controller
    {
        private readonly AppDbContext _context;

        public EventsController(AppDbContext context)
        {
            _context = context;
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
            if (id == null)
            {
                return NotFound();
            }

            var evt = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(m => m.EventId == id);

            if (evt == null)
            {
                return NotFound();
            }

            return View(evt);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name");
            ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name");
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name");
            return View();
        }

        // POST: Events/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventId,Title,Description,DateTime,Location,CategoryId,VenueId,OrganizerId")] Event @event)
        {
            if (ModelState.IsValid)
            {
                _context.Add(@event);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
            ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
            return View(@event);
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
            ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
            return View(@event);
        }

        // POST: Events/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventId,Title,Description,DateTime,Location,CategoryId,VenueId,OrganizerId")] Event @event)
        {
            if (id != @event.EventId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(@event);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.EventId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", @event.CategoryId);
            ViewData["OrganizerId"] = new SelectList(_context.Organizers, "OrganizerId", "Name", @event.OrganizerId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "VenueId", "Name", @event.VenueId);
            return View(@event);
        }

        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evt = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Organizer)
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(m => m.EventId == id);

            if (evt == null)
            {
                return NotFound();
            }

            return View(evt);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                _context.Events.Remove(@event);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventId == id);
        }
    }
}
