// src/App.tsx
import { useEffect, useState } from "react";
import { api, EventDto, TrendingCategoryBlock } from "@/lib/api";

import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader"; // uses the light header we made

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
  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);
  const [trendingByCat, setTrendingByCat] = useState<TrendingCategoryBlock[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [r, t] = await Promise.all([
          api.getRecs(6).catch(() => [] as EventDto[]), // may require auth/models
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
        {/* placeholder for account / theme toggle if you add later */}
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

  const isLoading =
    recs === null || trendingOverall === null || trendingByCat === null;

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      {header}
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        <Section
          title="Recommended for You"
          items={recs ?? []}
          loading={isLoading}
          emptyText="Sign in, click a few events, then run Admin → Train to see personalized picks."
          ctaLabel="See all recommendations"
          onCtaClick={() => {
            // optional: route to a /recs page later
          }}
        />

        <Section
          title="Trending Now"
          items={trendingOverall ?? []}
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
