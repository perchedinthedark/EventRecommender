import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import SearchBand from "@/components/SearchBand";

function Grid({
  items,
  loadingCount = 12,
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
  if (items.length === 0) {
    return <EmptyState title="No results" text="Try a broader or different query." />;
  }
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

export default function SearchPage() {
  const [params] = useSearchParams();
  const q = params.get("q") ?? "";
  const [items, setItems] = useState<EventDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

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
    return () => {
      cancelled = true;
    };
  }, [q]);

  return (
    <div className="min-h-screen text-white bg-[radial-gradient(1200px_600px_at_50%_-200px,#0f172a_0%,#020617_60%)]">
      <main className="max-w-[1200px] mx-auto px-4 pb-12 pt-6">
        {/* Glassy search band (dark variant) with a bit of top spacing */}
        <div className="searchband-dark mb-6">
          <SearchBand defaultQuery={q} autoFocus />
        </div>

        {error && (
          <div className="mb-4 rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-200">
            {error}
          </div>
        )}

        <Grid items={items} />
      </main>
    </div>
  );
}
