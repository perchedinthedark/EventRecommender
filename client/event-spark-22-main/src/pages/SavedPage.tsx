import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import EventCard from "@/components/EventCard";
import SkeletonCard from "@/components/SkeletonCard";
import { SectionHeader } from "@/components/SectionHeader";

export default function SavedPage() {
  const { mode } = useParams<{ mode: "interested" | "going" }>();
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
    return () => {
      cancel = true;
    };
  }, [mode]);

  const title = mode === "going" ? "Going" : "Interested";

  return (
    <div className="min-h-screen page-surface text-[hsl(var(--foreground))]">
      <main className="max-w-[1200px] mx-auto px-4 pb-12">
        <SectionHeader title={`${title} events`} />

        {items === null ? (
          <div
            className="grid gap-6"
            style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}
          >
            {Array.from({ length: 9 }).map((_, i) => (
              <SkeletonCard key={i} />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="text-slate-300">Nothing here yet.</div>
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
      </main>
    </div>
  );
}
