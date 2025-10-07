// client/src/pages/RecsPage.tsx
import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import EmptyState from "@/components/EmptyState";
import { SectionHeader } from "@/components/SectionHeader";
import { cn } from "@/lib/utils";

type Cat = { id: number; name: string };

export default function RecsPage() {
  const [params, setParams] = useSearchParams();
  // store category NAME in the URL so it matches EventDto.category
  const selectedCatName = params.get("cat") ?? "";

  const [items, setItems] = useState<EventDto[] | null>(null);
  const [cats, setCats] = useState<Cat[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        setLoading(true);
        const [recs, allCats] = await Promise.all([
          api.getRecs(200).catch(() => [] as EventDto[]),
          api.getCategories().catch(() => [] as Cat[]),
        ]);
        if (cancel) return;
        setItems(recs);
        setCats(allCats);
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
    if (!selectedCatName) return items;
    const wanted = selectedCatName.toLowerCase();
    return items.filter((e) => (e.category ?? "").toLowerCase() === wanted);
  }, [items, selectedCatName]);

  function setCategory(name: string | "") {
    const next = new URLSearchParams(params.toString());
    if (name === "") next.delete("cat");
    else next.set("cat", name);
    setParams(next, { replace: true });
  }

  return (
    <div className="min-h-screen page-surface text-[hsl(var(--foreground))]">
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        <SectionHeader title="Your Recommendations" />

        {/* Category bubbles: ALL categories from API; filter by NAME */}
        <div className="flex flex-wrap items-center gap-2 mb-6">
          <button
            onClick={() => setCategory("")}
            className={cn(
              "px-4 py-1.5 rounded-full text-sm border transition-colors",
              selectedCatName === ""
                ? "bg-blue-600 text-white border-transparent"
                : "bg-white/5 text-slate-200 border-white/10 hover:bg-white/10"
            )}
          >
            All
          </button>

          {cats.map((c) => {
            const active = c.name === selectedCatName;
            return (
              <button
                key={c.id}
                onClick={() => setCategory(c.name)}
                className={cn(
                  "px-4 py-1.5 rounded-full text-sm border transition-colors",
                  active
                    ? "bg-blue-600 text-white border-transparent"
                    : "bg-white/5 text-slate-200 border-white/10 hover:bg-white/10"
                )}
                title={c.name}
              >
                {c.name}
              </button>
            );
          })}
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
            helperText="Try another category or interact with more events."
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
