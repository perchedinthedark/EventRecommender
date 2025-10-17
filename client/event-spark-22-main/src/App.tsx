// client/src/App.tsx
import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom"; // ⟵ added Link
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
      <div
        className="grid gap-6"
        style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
      >
        {Array.from({ length: loadingCount }).map((_, i) => (
          <SkeletonCard key={i} />
        ))}
      </div>
    );
  }
  if (items.length === 0) return <EmptyState title="Nothing to show yet." />;
  return (
    <div
      className="grid gap-6"
      style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
    >
      {items.map((ev) => (
        <EventCard key={ev.id} ev={ev} />
      ))}
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
  const [personalBlocks, setPersonalBlocks] = useState<TrendingCategoryBlock[] | null>(
    null
  );
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
    return () => {
      cancelled = true;
    };
  }, []);

  const isLoading =
    recs === null || trendingOverall === null || personalBlocks === null;
  const canShowPersonalBlocks =
    !!me && (recs?.length ?? 0) > 0 && (personalBlocks?.length ?? 0) > 0;

  return (
    <div className="min-h-screen app-deep text-slate-100">
      {/* NOTE: no top padding so the hero sits snug under the header */}
      <main className="max-w-[1200px] mx-auto px-4 pt-0 pb-12">
        {/* HERO — rectangular, full bleed inside container */}
        <section className="relative overflow-hidden border border-white/10 bg-white/5 backdrop-blur-md shadow-[0_24px_80px_-30px_rgba(0,0,0,.65)] mb-8">
          <img
            className="absolute inset-0 h-full w-full object-cover"
            src="https://images.unsplash.com/photo-1506157786151-b8491531f063?q=80&w=1600&auto=format&fit=crop"
            alt="Crowd at an event"
            loading="lazy"
          />
          <div className="absolute inset-0 bg-gradient-to-b from-black/30 via-black/40 to-black/70" />
          <div className="relative px-6 py-12 md:px-10 md:py-14">
            <h1 className="text-3xl md:text-5xl font-bold tracking-tight text-white">
              Eventualno
            </h1>
            <p className="mt-3 max-w-2xl text-white/85 text-sm md:text-base">
              Make plans, not FOMO — we’ll nudge you to the gigs, meetups, and
              moments worth showing up for.
            </p>
          </div>
        </section>

        {/* Search — dark glass variant */}
        <div className="mb-8">
          <SearchBand variant="dark" />
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
            <div className="card-surface rounded-2xl border border-white/12 p-6 text-center">
              <h3 className="text-lg font-semibold mb-1">Personalized picks await ✨</h3>
              <p className="text-sm text-white/80">
                <Link
                  to="/login"
                  className="text-transparent bg-clip-text bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300
                             decoration-white/30 underline-offset-4 hover:opacity-90"
                >
                  Log in
                </Link>{" "}
                or{" "}
                <Link
                  to="/register"
                  className="text-transparent bg-clip-text bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300
                             decoration-white/30 underline-offset-4 hover:opacity-90"
                >
                  create an account
                </Link>{" "}
                to get recommendations tailored to you.
              </p>
            </div>
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
