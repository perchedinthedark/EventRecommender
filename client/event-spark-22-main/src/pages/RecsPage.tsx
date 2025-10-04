import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { api, EventDto, TrendingCategoryBlock } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";
import { CategoryChip } from "@/components/CategoryChip";
import { cn } from "@/lib/utils";

type Cat = { id: number; name: string };

export default function RecsPage() {
  const nav = useNavigate();
  const [params, setParams] = useSearchParams();
  const selectedCat = params.get("cat") ?? "";

  const [items, setItems] = useState<EventDto[] | null>(null);
  const [cats, setCats] = useState<Cat[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        setLoading(true);
        // generous topN; paging not required yet
        const [recs, trending] = await Promise.all([
          api.getRecs(200).catch(() => [] as EventDto[]),
          api.getTrending(6, 6),
        ]);
        if (cancel) return;

        // category map from trending (id -> name)
        const byId = new Map<number, string>();
        for (const b of (trending.byCategory ?? []) as TrendingCategoryBlock[]) {
          if (b?.categoryId != null) byId.set(b.categoryId, b.categoryName ?? "");
        }
        const derivedCats: Cat[] = Array.from(byId.entries()).map(([id, name]) => ({
          id,
          name: name || `Category ${id}`,
        }));

        setCats(derivedCats);
        setItems(recs);
      } finally {
        setLoading(false);
      }
    })();
    return () => {
      cancel = true;
    };
  }, []);

  const filtered = useMemo(() => {
    if (!items) return [];
    if (!selectedCat) return items;
    const catId = Number(selectedCat);
    const matchName = cats.find((c) => c.id === catId)?.name ?? "";
    if (!matchName) return items;
    return items.filter((e) => (e.category ?? "").toLowerCase() === matchName.toLowerCase());
  }, [items, selectedCat, cats]);

  function setCategory(catId: number | "") {
    const next = new URLSearchParams(params.toString());
    if (catId === "") next.delete("cat");
    else next.set("cat", String(catId));
    setParams(next, { replace: true });
  }

  return (
    <div className="min-h-screen bg-[hsl(var(--background))] text-[hsl(var(--foreground))]">
      <nav className="px-4 py-4 border-b border-slate-200 mb-6 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
        <div className="max-w-[1200px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">
              ← Back
            </button>
            <strong className="text-lg text-slate-900">Your Recommendations</strong>
          </div>
        </div>
      </nav>

      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        <SectionHeader title="Recommended for You" />

        <div className="flex flex-wrap items-center gap-2 mb-5">
          <button
            onClick={() => setCategory("")}
            className={cn(
              "rounded-full px-3 py-1 text-sm border transition-colors",
              !selectedCat
                ? "bg-primary text-primary-foreground border-transparent"
                : "bg-white border-slate-200 text-slate-700 hover:bg-slate-50"
            )}
          >
            All
          </button>
          {cats.map((c) => (
            <button
              key={c.id}
              onClick={() => setCategory(c.id)}
              className={cn(
                "rounded-full px-3 py-1 text-sm border transition-colors",
                String(c.id) === selectedCat
                  ? "bg-primary text-primary-foreground border-transparent"
                  : "bg-white border-slate-200 text-slate-700 hover:bg-slate-50"
              )}
              title={c.name}
            >
              <CategoryChip text={c.name} variant={String(c.id) === selectedCat ? "primary" : "default"} />
            </button>
          ))}
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
          <EmptyState
            headline="No recommendations yet"
            helperText="Sign in, interact with events, then run Admin → Train."
          />
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
