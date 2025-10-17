import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import { SectionHeader } from "@/components/SectionHeader";
import { cn } from "@/lib/utils";
import Hero from "@/components/Hero";

type Cat = { id: number; name: string };

export default function RecsPage() {
  const [params, setParams] = useSearchParams();
  const selectedCatName = params.get("cat") ?? "";

  const [me, setMe] = useState<{ id: string; userName?: string; displayName?: string | null; email: string } | null>(null);
  const [items, setItems] = useState<EventDto[] | null>(null);
  const [cats, setCats] = useState<Cat[]>([]);
  const [loading, setLoading] = useState(true);

  // who am i?
  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        const u = await api.auth.me().catch(() => null);
        if (!cancel) setMe(u);
      } catch {
        if (!cancel) setMe(null);
      }
    })();
    return () => { cancel = true; };
  }, []);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        setLoading(true);
        const [recs, allCats] = await Promise.all([
          api.getRecs(200).catch(() => [] as EventDto[]),
          api.getCategories().catch(() => [] as Cat[]),
        ]);
        if (cancel) return;
        setItems(recs);
        setCats(allCats);
      } finally {
        setLoading(false);
      }
    })();
    return () => { cancel = true; };
  }, []);

  const filtered = useMemo(() => {
    if (!items) return [];
    if (!selectedCatName) return items;
    const wanted = selectedCatName.toLowerCase();
    return items.filter((e) => (e.category ?? "").toLowerCase() === wanted);
  }, [items, selectedCatName]);

  function setCategory(name: string | "") {
    const next = new URLSearchParams(params.toString());
    if (name === "") next.delete("cat");
    else next.set("cat", name);
    setParams(next, { replace: true });
  }

  return (
    <div className="min-h-screen app-deep text-slate-100">
      <main className="max-w-[1200px] mx-auto px-4 pt-0 pb-12">
        {/* Rectangular hero, flush under header */}
        <Hero
          rectangular
          title="Hand-picked for you"
          subtitle="Follow your vibes — we’ll do the curating."
          imageUrl="https://wetplanetwhitewater.com/wp-content/uploads/AdobeStock_226049210-scaled.jpeg"
          className="mb-8"
        />

        <SectionHeader title="Your Recommendations" className="[&>h2]:text-white" />

        {/* Category bubbles */}
        <div className="flex flex-wrap items-center gap-2 mb-6">
          <button
            onClick={() => setCategory("")}
            className={cn(
              "px-4 py-1.5 rounded-full text-sm border transition-colors",
              selectedCatName === ""
                ? "bg-blue-600 text-white border-transparent"
                : "bg-white/10 text-white border-white/15 hover:bg-white/15"
            )}
          >
            All
          </button>

          {cats.map((c) => {
            const active = c.name === selectedCatName;
            return (
              <button
                key={c.id}
                onClick={() => setCategory(c.name)}
                className={cn(
                  "px-4 py-1.5 rounded-full text-sm border transition-colors",
                  active
                    ? "bg-blue-600 text-white border-transparent"
                    : "bg-white/10 text-white border-white/15 hover:bg-white/15"
                )}
                title={c.name}
              >
                {c.name}
              </button>
            );
          })}
        </div>

        {loading ? (
          <div
            className="grid gap-6"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
          >
            {Array.from({ length: 12 }).map((_, i) => (
              <SkeletonCard key={i} />
            ))}
          </div>
        ) : filtered.length === 0 ? (
          // Signed-out: inviting empty state with actions
          !me ? (
            <div className="card-surface rounded-2xl border border-white/12 p-6 text-center">
              <h3 className="text-lg font-semibold mb-1">Personalized picks await ✨</h3>
              <p className="text-sm text-white/80 mb-4">
                Log in or create an account to get recommendations tailored to you.
              </p>
              <div className="flex items-center justify-center gap-2">
                <Link to="/login" className="btn-outline-modern px-3 py-2">Log in</Link>
                <Link to="/register" className="btn-primary-modern px-3 py-2">Create account</Link>
              </div>
            </div>
          ) : (
            // Signed-in but nothing matched
            <div className="card-surface rounded-2xl border border-white/12 p-6 text-center">
              <h3 className="text-lg font-semibold mb-1">No recommendations yet</h3>
              <p className="text-sm text-white/80 mb-4">
                Try another category, search for a new vibe, or interact with more events.
              </p>
              <div className="flex items-center justify-center gap-2">
                <Link to="/search" className="btn-outline-modern px-3 py-2">Search events</Link>
                <Link to="/trending" className="btn-primary-modern px-3 py-2">Browse Trending</Link>
              </div>
            </div>
          )
        ) : (
          <div
            className="grid gap-6"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
          >
            {filtered.map((ev) => (
              <EventCard key={ev.id} ev={ev} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
