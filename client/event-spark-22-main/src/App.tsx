import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, EventDto, TrendingCategoryBlock } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";
import SearchBand from "@/components/SearchBand";

function Grid({
  items,
  loadingCount = 6,
}: {
  items: EventDto[] | null;
  loadingCount?: number;
}) {
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

  const [me, setMe] = useState<{
    id: string;
    userName?: string;
    displayName?: string | null;
    email: string;
  } | null>(null);

  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);
  const [personalBlocks, setPersonalBlocks] = useState<TrendingCategoryBlock[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const user = await api.auth.me().catch(() => null);
      if (cancelled) return;
      setMe(user);

      try {
        const [r, t] = await Promise.all([
          api.getRecs(6).catch(() => [] as EventDto[]),
          api.getTrending(6, user ? 2 : 0),
        ]);
        if (cancelled) return;
        setRecs(r);
        setTrendingOverall(t.overall);
        const cats = user ? (t.byCategory ?? []).slice(0, 2) : [];
        setPersonalBlocks(cats);
      } catch (e: any) {
        if (!cancelled) setError(e?.message ?? "Failed to load data.");
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const isLoading = recs === null || trendingOverall === null || personalBlocks === null;
  const canShowPersonalBlocks = !!me && (recs?.length ?? 0) > 0 && (personalBlocks?.length ?? 0) > 0;

  return (
    <div className="min-h-screen app-deep text-slate-100">
      <main className="max-w-[1200px] mx-auto px-4 pt-8 pb-12">
        <div className="searchband-dark mb-8">
          <SearchBand />
        </div>

        {error && (
          <div className="mb-6 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-red-200">
            {error}
          </div>
        )}

        <section className="mb-10">
          <SectionHeader
            title="Recommended for You"
            ctaLabel="See all recommendations"
            onCtaClick={() => nav("/recs")}
            className="[&>h2]:text-white"
          />
          {isLoading ? (
            <Grid items={null} />
          ) : me ? (
            <Grid items={recs!} />
          ) : (
            <EmptyState title="Sign in to see personalized picks." />
          )}
        </section>

        <section className="mb-10">
          <SectionHeader
            title="Trending Now"
            ctaLabel="View full trending"
            onCtaClick={() => nav("/trending")}
            className="[&>h2]:text-white"
          />
        {isLoading ? <Grid items={null} /> : <Grid items={trendingOverall!} />}
        </section>

        {canShowPersonalBlocks &&
          personalBlocks!.map((block) => (
            <section key={block.categoryId} className="mb-10">
              <SectionHeader
                title={`Trending in ${block.categoryName}`}
                ctaLabel="See more in Trending"
                onCtaClick={() => nav("/trending")}
                className="[&>h2]:text-white"
              />
              <Grid items={block.events} />
            </section>
          ))}
      </main>
    </div>
  );
}
