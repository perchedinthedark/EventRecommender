import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";

export default function CategoryTrendingPage() {
  const { id } = useParams<{ id: string }>(); // we pass category name in URL
  const nav = useNavigate();
  const [title, setTitle] = useState<string>("");
  const [items, setItems] = useState<EventDto[] | null>(null);

  const categoryName = useMemo(() => decodeURIComponent(id ?? ""), [id]);

  useEffect(() => {
    let cancel = false;
    (async () => {
      // We need a categoryId to call the API; we look it up by name using homepage data fallback:
      // Quick approach: call /api/trending with many categories to discover IDs by name, then /by-category
      try {
        const t = await api.getTrending(6, 20);
        const found = (t.byCategory ?? []).find(b => b.categoryName?.toLowerCase() === categoryName.toLowerCase());
        if (!found) { setTitle(categoryName || "Category"); setItems([]); return; }
        setTitle(found.categoryName);
        const full = await api.getTrendingByCategory(found.categoryId, 100);
        if (cancel) return;
        setItems(full.events);
      } catch {
        if (!cancel) setItems([]);
      }
    })();
    return () => { cancel = true; };
  }, [categoryName]);

  return (
    <div className="min-h-screen bg-[hsl(var(--background))]">
      <nav className="px-4 py-4 border-b border-slate-200 mb-6 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
        <div className="max-w-[1200px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">← Back</button>
            <strong className="text-lg text-slate-900">Trending in {title || "Category"}</strong>
          </div>
        </div>
      </nav>
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {items === null ? (
          <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
            {Array.from({ length: 12 }).map((_, i) => <SkeletonCard key={i} />)}
          </div>
        ) : items.length === 0 ? (
          <div className="text-slate-600">No events in this category yet.</div>
        ) : (
          <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
            {items.map(ev => <EventCard key={ev.id} ev={ev} />)}
          </div>
        )}
      </main>
    </div>
  );
}
