import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";

export default function SavedPage() {
  const { mode } = useParams<{ mode: "interested" | "going" }>();
  const nav = useNavigate();
  const [items, setItems] = useState<EventDto[] | null>(null);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        const status = (mode === "going" ? "Going" : "Interested") as "Interested" | "Going";
        const data = await api.getSaved(status);
        if (!cancel) setItems(data);
      } catch {
        if (!cancel) setItems([]);
      }
    })();
    return () => { cancel = true; };
  }, [mode]);

  const title = mode === "going" ? "Going" : "Interested";

  return (
    <div className="min-h-screen bg-[hsl(var(--background))]">
      <nav className="px-4 py-4 border-b border-slate-200 mb-6 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
        <div className="max-w-[1200px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">← Back</button>
            <strong className="text-lg text-slate-900">{title} events</strong>
          </div>
        </div>
      </nav>
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        {items === null ? (
          <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
            {Array.from({ length: 9 }).map((_, i) => <SkeletonCard key={i} />)}
          </div>
        ) : items.length === 0 ? (
          <div className="text-slate-600">Nothing here yet.</div>
        ) : (
          <div className="grid gap-6" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
            {items.map(ev => <EventCard key={ev.id} ev={ev} />)}
          </div>
        )}
      </main>
    </div>
  );
}
