import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";
import { cn } from "@/lib/utils";

type CatBlock = {
  id: number;
  name: string;
  items: EventDto[] | null; // null = loading, [] = empty
  expanded: boolean;
};

function Grid({ items, loadingCount = 12 }: { items: EventDto[] | null; loadingCount?: number }) {
  if (items === null) {
    return (
      <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
        {Array.from({ length: loadingCount }).map((_, i) => <SkeletonCard key={i} />)}
      </div>
    );
  }
  if (items.length === 0) return <EmptyState title="Nothing to show yet." />;
  return (
    <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
      {items.map((ev) => <EventCard key={ev.id} ev={ev} />)}
    </div>
  );
}

export default function TrendingPage() {
  const nav = useNavigate();

  // FIXED TYPE: userName is optional; displayName may be present
  const [me, setMe] = useState<{ id: string; userName?: string; displayName?: string | null; email: string } | null>(null);

  const [overall, setOverall] = useState<EventDto[] | null>(null); // bigger set here (e.g., 12–15)
  const [cats, setCats] = useState<CatBlock[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Category filter bubbles
  const [activeFilter, setActiveFilter] = useState<string>("");

  useEffect(() => {
    api.auth.me().then(setMe).catch(() => setMe(null));

    let cancelled = false;
    (async () => {
      try {
        // Pull overall trending (expanded)
        const t = await api.getTrending(12, 2); // perList=12, categoriesToShow ignored here
        if (cancelled) return;
        setOverall(t.overall);

        // All categories (we’ll render every block here)
        const allCats = await api.getCategories();
        if (cancelled) return;

        setCats(allCats.map(c => ({ id: c.id, name: c.name, items: null, expanded: false })));

        // Prime each category with 6, allow “Show more” to fetch 12
        await Promise.all(allCats.map(async (c) => {
          try {
            const pack = await api.getTrendingByCategory(c.id, 6);
            if (cancelled) return;
            setCats(prev => prev.map(b => b.id === c.id ? { ...b, items: pack.events } : b));
          } catch {
            if (!cancelled) setCats(prev => prev.map(b => b.id === c.id ? { ...b, items: [] } : b));
          }
        }));
      } catch (e: any) {
        if (!cancelled) setError(e?.message ?? "Failed to load trending.");
      }
    })();

    return () => { cancelled = true; };
  }, []);

  async function toggleExpand(catId: number) {
    setCats(prev => prev.map(b => b.id === catId ? { ...b, expanded: !b.expanded } : b));
    const blk = cats.find(b => b.id === catId);
    if (blk && !blk.expanded) {
      try {
        const pack = await api.getTrendingByCategory(catId, 12);
        setCats(prev => prev.map(b => b.id === catId ? { ...b, items: pack.events, expanded: true } : b));
      } catch { /* noop */ }
    }
  }

  const header = (
    <nav className="px-4 py-4 border-b border-slate-200 mb-8 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
      <div className="max-w-[1200px] mx-auto flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">← Back</button>
          <strong className="text-lg text-slate-900">Trending</strong>
        </div>
        <div className="flex items-center gap-4 text-sm">
          <Link to="/" className="text-blue-600 hover:underline">Home</Link>
          <Link to="/recs" className="text-blue-600 hover:underline">Recommendations</Link>
          <Link to="/people" className="text-blue-600 hover:underline">People</Link>
          {me ? (
            <span className="text-slate-600">Hi, {me.displayName ?? me.userName ?? me.email}</span>
          ) : (
            <>
              <Link to="/login" className="text-blue-600 hover:underline">Log in</Link>
              <Link to="/register" className="text-blue-600 hover:underline">Register</Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );

  if (error) {
    return (
      <div className="min-h-screen bg-[hsl(var(--background))]">
        {header}
        <div className="max-w-[1200px] mx-auto px-4">
          <div className="text-red-600">Error: {error}</div>
        </div>
      </div>
    );
  }

  // Bubbles from category names
  const bubbleNames = useMemo(() => cats.map(c => c.name), [cats]);
  const eq = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;

  // Filter helpers
  const filterListByName = (list: EventDto[] | null) => {
    if (!list) return null;
    if (!activeFilter) return list;
    return list.filter(e => e.category && eq(String(e.category), activeFilter));
  };

  const overallFiltered = filterListByName(overall);
  const visibleCats = useMemo(
    () => activeFilter ? cats.filter(c => eq(c.name, activeFilter)) : cats,
    [cats, activeFilter]
  );

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      {header}

      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {/* FILTER BUBBLES */}
        <div className="mb-6 flex flex-wrap items-center gap-2">
          <button
            onClick={() => setActiveFilter("")}
            className={cn(
              "px-4 py-1.5 rounded-full text-sm border transition-colors",
              activeFilter === ""
                ? "bg-blue-600 text-white border-transparent"
                : "bg-white text-slate-700 border-slate-200 hover:bg-slate-50"
            )}
          >
            All
          </button>
          {bubbleNames.map((name) => {
            const active = eq(activeFilter, name);
            return (
              <button
                key={name}
                onClick={() => setActiveFilter(active ? "" : name)}
                className={cn(
                  "px-4 py-1.5 rounded-full text-sm border transition-colors",
                  active
                    ? "bg-blue-600 text-white border-transparent"
                    : "bg-white text-slate-800 border-slate-200 hover:bg-slate-50"
                )}
                title={name}
              >
                {name}
              </button>
            );
          })}
        </div>

        {/* Overall trending (expanded) */}
        <section className="mb-10">
          <SectionHeader title="Trending Now (expanded)" />
          <Grid items={overallFiltered} loadingCount={12} />
        </section>

        {/* All categories with collapsible blocks */}
        {visibleCats.map(block => (
          <section key={block.id} className="mb-10">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-[22px] font-semibold text-slate-900">
                Trending in {block.name}
              </h2>
              <button
                onClick={() => toggleExpand(block.id)}
                className={cn(
                  "text-sm rounded-full px-3 py-1 border transition-colors",
                  block.expanded ? "bg-slate-100 border-slate-200 text-slate-800"
                                  : "bg-white border-slate-200 text-blue-600 hover:bg-slate-50"
                )}
              >
                {block.expanded ? "Collapse" : "Show more"}
              </button>
            </div>
            <Grid items={block.items} loadingCount={block.expanded ? 12 : 6} />
          </section>
        ))}
      </main>
    </div>
  );
}
