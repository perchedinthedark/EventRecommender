import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, EventDto, TrendingCategoryBlock } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";

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

  // Matches /api/auth/me (displayName optional; userName optional)
  const [me, setMe] = useState<{ id: string; userName?: string; displayName?: string | null; email: string } | null>(null);

  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);
  const [trendingByCat, setTrendingByCat] = useState<TrendingCategoryBlock[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.auth.me().then(setMe).catch(() => setMe(null));

    let cancelled = false;
    (async () => {
      try {
        // Home: 6 items each; only top-2 personalized category blocks
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

    return () => { cancelled = true; };
  }, []);

  const header = (
    <nav className="px-4 py-4 border-b border-slate-200 mb-8 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
      <div className="max-w-[1200px] mx-auto flex items-center justify-between">
        <div className="flex items-center gap-3">
          <strong className="text-lg text-slate-900">EventRecommender</strong>
          <span className="text-slate-500">React preview</span>
        </div>
        <div className="flex items-center gap-4 text-sm">
          <Link to="/people" className="text-blue-600 hover:underline">People</Link>
          <Link to="/recs" className="text-blue-600 hover:underline">Recommendations</Link>
          <Link to="/trending" className="text-blue-600 hover:underline">Trending</Link>
          <Link to="/saved/interested" className="text-blue-600 hover:underline">Saved: Interested</Link>
          <Link to="/saved/going" className="text-blue-600 hover:underline">Saved: Going</Link>
          {me ? (
            <>
              <span className="text-slate-600">Hi, {me.displayName ?? me.userName ?? me.email}</span>
              <button
                className="text-slate-600 hover:text-slate-900"
                onClick={() => api.auth.logout().then(() => location.reload())}
              >
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

  const isLoading = recs === null || trendingOverall === null || trendingByCat === null;

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      {header}

      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {/* Recommended */}
        <section className="mb-10">
          <SectionHeader
            title="Recommended for You"
            ctaLabel="See all recommendations"
            onCtaClick={() => nav("/recs")}
          />
          {isLoading ? <Grid items={null} /> : <Grid items={recs!} />}
        </section>

        {/* Trending overall */}
        <section className="mb-10">
          <SectionHeader
            title="Trending Now"
            ctaLabel="View full trending"
            onCtaClick={() => nav("/trending")}
          />
          {isLoading ? <Grid items={null} /> : <Grid items={trendingOverall!} />}
        </section>

        {/* Top-2 personalized category blocks */}
        {!isLoading && trendingByCat && trendingByCat.length > 0 && (
          <>
            {trendingByCat.map(block => (
              <section key={block.categoryId} className="mb-10">
                <SectionHeader
                  title={`Trending in ${block.categoryName}`}
                  ctaLabel="See more in Trending"
                  onCtaClick={() => nav("/trending")}
                />
                <Grid items={block.events} />
              </section>
            ))}
          </>
        )}
      </main>
    </div>
  );
}
