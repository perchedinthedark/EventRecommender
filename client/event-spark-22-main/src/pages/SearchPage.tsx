import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import SearchBand from "@/components/SearchBand";

function Grid({ items, loadingCount = 12 }: { items: EventDto[] | null; loadingCount?: number }) {
  if (items === null) {
    return (
      <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
        {Array.from({ length: loadingCount }).map((_, i) => <SkeletonCard key={i} />)}
      </div>
    );
  }
  if (items.length === 0) return <EmptyState title="No results" text="Try a broader or different query." />;
  return (
    <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
      {items.map((ev) => <EventCard key={ev.id} ev={ev} />)}
    </div>
  );
}

export default function SearchPage() {
  const nav = useNavigate();
  const [params] = useSearchParams();
  const q = params.get("q") ?? "";
  const [items, setItems] = useState<EventDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  // run search on q change
  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!q.trim()) {
        setItems([]);
        return;
      }
      setError(null);
      setItems(null);
      try {
        const res = await api.search(q, 100);
        if (!cancelled) setItems(res);
      } catch (e: any) {
        if (!cancelled) {
          setError(e?.message ?? "Search failed");
          setItems([]);
        }
      }
    })();
    return () => { cancelled = true; };
  }, [q]);

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      <nav className="px-4 py-4 border-b border-slate-200 mb-6 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
        <div className="max-w-[1200px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">← Back</button>
            <strong className="text-lg text-slate-900">Search</strong>
          </div>
          <div className="flex items-center gap-4 text-sm">
            <Link to="/" className="text-blue-600 hover:underline">Home</Link>
            <Link to="/trending" className="text-blue-600 hover:underline">Trending</Link>
            <Link to="/recs" className="text-blue-600 hover:underline">Recommendations</Link>
          </div>
        </div>
      </nav>

      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {/* Editable prompt row */}
        <SearchBand defaultQuery={q} autoFocus className="mb-6" />

        {error && (
          <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        <Grid items={items} />
      </main>
    </div>
  );
}
