// src/App.tsx
import { useEffect, useMemo, useRef, useState } from "react";
import { api, EventDto, TrendingCategoryBlock } from "./lib/api";
import "./App.css";

function EventCard({ ev }: { ev: EventDto }) {
  // Link to your ASP.NET routing that logs the view via TrackAndShow
  const href = `/Events/TrackAndShow/${ev.id}`;

  return (
    <div className="card" style={{ borderRadius: 12, boxShadow: "0 1px 4px rgba(0,0,0,.08)" }}>
      <div className="card-body">
        <h5 className="card-title">
          <a href={href} style={{ textDecoration: "none" }}>
            {ev.title}
          </a>
        </h5>
        {ev.description && (
          <p className="card-text" style={{ color: "#444" }}>
            {ev.description.length > 140 ? ev.description.slice(0, 140) + "…" : ev.description}
          </p>
        )}
        <div style={{ fontSize: 14, color: "#666" }}>
          <div>{new Date(ev.dateTime).toLocaleString()}</div>
          {ev.location && <div>{ev.location}</div>}
          <div>
            {ev.category && <span>#{ev.category} </span>}
            {ev.venue && <span>• {ev.venue} </span>}
            {ev.organizer && <span>• {ev.organizer}</span>}
          </div>
          {typeof ev.friendsGoing === "number" && ev.friendsGoing > 0 && (
            <div style={{ marginTop: 4 }}>👥 {ev.friendsGoing} friend(s) going</div>
          )}
        </div>
      </div>
    </div>
  );
}

function Section({
  title,
  items,
  emptyText,
}: {
  title: string;
  items: EventDto[];
  emptyText?: string;
}) {
  return (
    <section style={{ marginBottom: 28 }}>
      <h3 style={{ marginBottom: 12 }}>{title}</h3>
      {items.length === 0 ? (
        <div className="text-muted">{emptyText ?? "Nothing to show yet."}</div>
      ) : (
        <div className="grid" style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))" }}>
          {items.map((ev) => (
            <EventCard key={ev.id} ev={ev} />
          ))}
        </div>
      )}
    </section>
  );
}

export default function App() {
  const [recs, setRecs] = useState<EventDto[] | null>(null);
  const [trendingOverall, setTrendingOverall] = useState<EventDto[] | null>(null);
  const [trendingByCat, setTrendingByCat] = useState<TrendingCategoryBlock[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  // fetch everything on load
  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [r, t] = await Promise.all([
          api.getRecs(6).catch(() => [] as EventDto[]), // recs may require login/models
          api.getTrending(6, 2),
        ]);
        if (cancelled) return;
        setRecs(r);
        setTrendingOverall(t.overall);
        setTrendingByCat(t.byCategory);
      } catch (e: any) {
        if (!cancelled) setError(e?.message ?? "Failed to load data.");
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  // basic nav/header
  const header = useMemo(
    () => (
      <nav style={{ padding: "12px 16px", borderBottom: "1px solid #eee", marginBottom: 24 }}>
        <strong>EventRecommender</strong>
        <span style={{ marginLeft: 16, color: "#666" }}>React preview</span>
      </nav>
    ),
    []
  );

  if (error) {
    return (
      <div>
        {header}
        <div style={{ padding: 16, color: "crimson" }}>Error: {error}</div>
      </div>
    );
  }

  const isLoading = recs === null || trendingOverall === null || trendingByCat === null;

  return (
    <div>
      {header}
      <main style={{ maxWidth: 1100, margin: "0 auto", padding: "0 16px 40px" }}>
        {isLoading ? (
          <div>Loading…</div>
        ) : (
          <>
            <Section
              title="Recommended for you"
              items={recs ?? []}
              emptyText="Sign in, click a few events, then run Admin → Train to see personalized picks."
            />

            <Section
              title="Trending now"
              items={trendingOverall ?? []}
              emptyText="No trending yet. Browse events to generate activity."
            />

            {(trendingByCat ?? []).map((block) => (
              <Section
                key={block.categoryId}
                title={`Trending in ${block.categoryName}`}
                items={block.events}
                emptyText="—"
              />
            ))}
          </>
        )}
      </main>
    </div>
  );
}
