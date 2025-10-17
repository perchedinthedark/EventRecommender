import { useEffect, useState, useMemo } from "react";
import { Link, useParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import { SectionHeader } from "@/components/SectionHeader";
import Hero from "@/components/Hero";

export default function SavedPage() {
  const { mode } = useParams<{ mode: "interested" | "going" }>();
  const [items, setItems] = useState<EventDto[] | null>(null);

  // Hero copy + art per mode
  const hero = useMemo(() => {
    if (mode === "going") {
      return {
        title: "You’re going 🎉",
        subtitle: "Everything you’ve RSVP’d to—dates, details, and pure hype.",
        // subtle crowd / stage lights
        imageUrl:
          "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?q=80&w=1600&auto=format&fit=crop",
      };
    }
    return {
      title: "Saved for later",
      subtitle: "Keep tabs on events you’re curious about. Tap “Going” when you’re in.",
      // cozy group / pre-event mood
      imageUrl:
        "https://images.stockcake.com/public/b/1/1/b1132ad5-3aae-4b9b-90ae-800f7db77bd9_large/colorful-sticky-notes-stockcake.jpg",
    };
  }, [mode]);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        const status =
          (mode === "going" ? "Going" : "Interested") as "Interested" | "Going";
        const data = await api.getSaved(status);
        if (!cancel) setItems(data);
      } catch {
        if (!cancel) setItems([]);
      }
    })();
    return () => {
      cancel = true;
    };
  }, [mode]);

  const title = mode === "going" ? "Going" : "Interested";

  return (
    <div className="min-h-screen app-deep text-slate-100">
      {/* flush under header */}
      <main className="max-w-[1200px] mx-auto px-4 pt-0 pb-12">
        <Hero
          rectangular
          title={hero.title}
          subtitle={hero.subtitle}
          imageUrl={hero.imageUrl}
          className="mb-8"
        />

        <SectionHeader title={`${title} events`} className="[&>h2]:text-white" />

        {items === null ? (
          <div
            className="grid gap-6"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
          >
            {Array.from({ length: 9 }).map((_, i) => (
              <SkeletonCard key={i} />
            ))}
          </div>
        ) : items.length === 0 ? (
          // prettier, inviting empty state
          <div className="card-surface rounded-2xl border border-white/12 p-6 text-center">
            {mode === "going" ? (
              <>
                <h3 className="text-lg font-semibold mb-1">No plans yet</h3>
                <p className="text-sm text-white/80 mb-4">
                  Find something that sparks joy and hit <span className="font-medium">Going</span>.
                </p>
                <div className="flex items-center justify-center gap-2">
                  <Link to="/trending" className="btn-outline-modern px-3 py-2">
                    Explore Trending »
                  </Link>
                  <Link to="/recs" className="btn-primary-modern px-3 py-2">
                    See Recommendations
                  </Link>
                </div>
              </>
            ) : (
              <>
                <h3 className="text-lg font-semibold mb-1">Nothing saved yet</h3>
                <p className="text-sm text-white/80 mb-4">
                  Tap <span className="font-medium">Interested</span> on events to keep them here.
                </p>
                <div className="flex items-center justify-center gap-2">
                  <Link to="/search" className="btn-outline-modern px-3 py-2">
                    Search events »
                  </Link>
                  <Link to="/trending" className="btn-primary-modern px-3 py-2">
                    Browse Trending
                  </Link>
                </div>
              </>
            )}
          </div>
        ) : (
          <div
            className="grid gap-6"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
          >
            {items.map((ev) => (
              <EventCard key={ev.id} ev={ev} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
