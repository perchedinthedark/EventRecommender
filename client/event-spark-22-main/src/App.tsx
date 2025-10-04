import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, EventDto, TrendingCategoryBlock } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";
import { CategoryChip } from "@/components/CategoryChip";
import { cn } from "@/lib/utils";

function Section({
  title,
  items,
  loading,
  emptyText,
  ctaLabel,
  onCtaClick,
}: {
  title: string;
  items: EventDto[];
  loading?: boolean;
  emptyText?: string;
  ctaLabel?: string;
  onCtaClick?: () => void;
}) {
  return (
    <section className="mb-10">
      <SectionHeader title={title} ctaLabel={ctaLabel} onCtaClick={onCtaClick} />
      {loading ? (
        <div
          className="grid gap-6"
          style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
        >
          {Array.from({ length: 6 }).map((_, i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState title={emptyText ?? "Nothing to show yet."} />
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
    </section>
  );
}

export default function App() {
  const nav = useNavigate();

  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);
  const [trendingByCat, setTrendingByCat] = useState<TrendingCategoryBlock[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [me, setMe] = useState<{ id: string; userName: string; email: string } | null>(null);

  // category filter (client-side; affects Recommended + Trending Now)
  const [activeCatName, setActiveCatName] = useState<string>("");

  useEffect(() => {
    api.auth
      .me()
      .then(setMe)
      .catch(() => setMe(null));
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
        setTrendingByCat(t.byCategory);
      } catch (e: any) {
        if (!cancelled) setError(e?.message ?? "Failed to load data.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const header = (
    <nav className="px-4 py-4 border-b border-slate-200 mb-8 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
      <div className="max-w-[1200px] mx-auto flex items-center justify-between">
        <div className="flex items-center gap-3">
          <strong className="text-lg text-slate-900">EventRecommender</strong>
          <span className="text-slate-500">React preview</span>
        </div>
        <div className="flex items-center gap-4 text-sm">
          <Link to="/people" className="text-blue-600 hover:underline">
            People
          </Link>
          {me ? (
            <>
              <span className="text-slate-600">Hi, {me.userName ?? me.email}</span>
              <button
                className="text-slate-600 hover:text-slate-900"
                onClick={() => api.auth.logout().then(() => location.reload())}
              >
                Logout
              </button>
            </>
          ) : (
            <>
              <Link to="/login" className="text-blue-600 hover:underline">
                Log in
              </Link>
              <Link to="/register" className="text-blue-600 hover:underline">
                Register
              </Link>
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

  const isLoading = recs === null || trendingOverall === null || trendingByCat === null;

  // build category chips from currently loaded data
  const allCategoryNames = useMemo(() => {
    const names = new Set<string>();
    for (const e of recs ?? []) if (e.category) names.add(e.category);
    for (const e of trendingOverall ?? []) if (e.category) names.add(e.category);
    for (const b of trendingByCat ?? []) if (b?.categoryName) names.add(b.categoryName);
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  }, [recs, trendingOverall, trendingByCat]);

  const filterByActiveName = (list: EventDto[] | null) => {
    if (!list) return [];
    if (!activeCatName) return list;
    return list.filter((e) => (e.category ?? "").toLowerCase() === activeCatName.toLowerCase());
    }

  const recsFiltered = filterByActiveName(recs);
  const trendingFiltered = filterByActiveName(trendingOverall);

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      {header}
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {/* Category filter bar */}
        <div className="flex flex-wrap items-center gap-2 mb-6">
          <button
            onClick={() => setActiveCatName("")}
            className={cn(
              "rounded-full px-3 py-1 text-sm border transition-colors",
              !activeCatName
                ? "bg-primary text-primary-foreground border-transparent"
                : "bg-white border-slate-200 text-slate-700 hover:bg-slate-50"
            )}
          >
            All
          </button>
          {allCategoryNames.map((name) => {
            const active = name === activeCatName;
            return (
              <button
                key={name}
                onClick={() => setActiveCatName(active ? "" : name)}
                className={cn(
                  "rounded-full px-3 py-1 text-sm border transition-colors",
                  active
                    ? "bg-primary text-primary-foreground border-transparent"
                    : "bg-white border-slate-200 text-slate-700 hover:bg-slate-50"
                )}
                title={name}
              >
                <CategoryChip text={name} variant={active ? "primary" : "default"} />
              </button>
            );
          })}
        </div>

        <Section
          title="Recommended for You"
          items={recsFiltered}
          loading={isLoading}
          emptyText="Sign in, click a few events, then run Admin → Train to see personalized picks."
          ctaLabel="See all recommendations"
          onCtaClick={() => nav("/recs")}
        />

        <Section
          title="Trending Now"
          items={trendingFiltered}
          loading={isLoading}
          emptyText="No trending yet. Browse events to generate activity."
        />

        {(trendingByCat ?? []).map((block) => (
          <Section
            key={block.categoryId}
            title={`Trending in ${block.categoryName}`}
            items={block.events}
            loading={isLoading}
            emptyText="—"
          />
        ))}
      </main>
    </div>
  );
}
