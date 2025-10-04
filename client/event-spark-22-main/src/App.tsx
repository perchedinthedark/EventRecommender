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

function Grid({ items, loadingCount = 6 }: { items: EventDto[] | null; loadingCount?: number }) {
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

export default function App() {
  const nav = useNavigate();
  const [me, setMe] = useState<{ id: string; userName: string; email: string } | null>(null);

  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);

  const [cats, setCats] = useState<CatBlock[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Active category bubble; "" = All
  const [activeFilter, setActiveFilter] = useState<string>("");

  useEffect(() => {
    api.auth.me().then(setMe).catch(() => setMe(null));

    let cancelled = false;
    (async () => {
      try {
        const [r, t] = await Promise.all([
          api.getRecs(6).catch(() => [] as EventDto[]),
          api.getTrending(6, 2),
        ]);
        if (cancelled) return;
        setRecs(r);
        setTrendingOverall(t.overall);

        const allCats = await api.getCategories();
        if (cancelled) return;

        setCats(allCats.map(c => ({ id: c.id, name: c.name, items: null, expanded: false })));

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
        if (!cancelled) setError(e?.message ?? "Failed to load data.");
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
          <strong className="text-lg text-slate-900">EventRecommender</strong>
          <span className="text-slate-500">React preview</span>
        </div>
        <div className="flex items-center gap-4 text-sm">
          <Link to="/people" className="text-blue-600 hover:underline">People</Link>
          <Link to="/saved/interested" className="text-blue-600 hover:underline">Saved: Interested</Link>
          <Link to="/saved/going" className="text-blue-600 hover:underline">Saved: Going</Link>
          {me ? (
            <>
              <span className="text-slate-600">Hi, {me.userName ?? me.email}</span>
              <button className="text-slate-600 hover:text-slate-900" onClick={() => api.auth.logout().then(() => location.reload())}>
                Logout
              </button>
            </>
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

  const isLoading = recs === null || trendingOverall === null;

  // Bubble names from fetched categories
  const bubbleNames = useMemo(() => cats.map(c => c.name), [cats]);

  // Helper
  const eq = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;

  // Filter top sections by name
  const filterListByName = (list: EventDto[] | null) => {
    if (!list) return null;
    if (!activeFilter) return list;
    return list.filter(e => e.category && eq(String(e.category), activeFilter));
  };
  const recsFiltered = filterListByName(recs);
  const trendingFiltered = filterListByName(trendingOverall);

  // NEW: for category blocks below—hide all other categories when a filter is active
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

        {/* Recommended */}
        <section className="mb-10">
          <SectionHeader
            title="Recommended for You"
            ctaLabel="See all recommendations"
            onCtaClick={() => nav("/recs")}
          />
          {isLoading ? (
            <Grid items={null} />
          ) : (recsFiltered && recsFiltered.length > 0) ? (
            <Grid items={recsFiltered} />
          ) : (
            <EmptyState title="No recommendations for this category yet." />
          )}
        </section>

        {/* Trending overall */}
        <section className="mb-10">
          <SectionHeader title="Trending Now" />
          {isLoading ? <Grid items={null} /> : <Grid items={trendingFiltered} />}
        </section>

        {/* CATEGORY BLOCKS — only the selected category is shown when filtered */}
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

