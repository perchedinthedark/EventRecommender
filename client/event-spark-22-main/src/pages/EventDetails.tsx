// client/src/pages/EventDetails.tsx
import { useEffect, useMemo, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import { useDwell } from "@/pages/hooks/useDwell";
import { StatusButtons, EventStatus } from "@/components/StatusButtons";
import SkeletonCard from "@/components/SkeletonCard";
import { Calendar, MapPin } from "lucide-react";
import { RatingStars } from "@/components/RatingStars";
import RatingSelect from "@/components/RatingSelect";

export default function EventDetails() {
  const { id } = useParams<{ id: string }>();
  const eventId = useMemo(() => Number(id), [id]);

  const [ev, setEv] = useState<EventDto | null>(null);
  const [status, setStatus] = useState<EventStatus>("None");
  const [busy, setBusy] = useState(false);
  const [friendsGoing, setFriendsGoing] = useState<number | null>(null);
  const [rating, setRating] = useState<number | null>(null);
  const [ratingBusy, setRatingBusy] = useState(false);

  useEffect(() => {
    if (!eventId) return;
    api.getEvent(eventId).then(setEv);
    api
      .getMyInteraction(eventId)
      .then((m) => {
        setStatus((m?.status as EventStatus) ?? "None");
        setRating(typeof m?.rating === "number" ? m.rating! : null);
      })
      .catch(() => {});
    api.social
      .friendsGoing(eventId)
      .then((r) => setFriendsGoing(r.count))
      .catch(() => setFriendsGoing(null));
    api.telemetry.click(eventId).catch(() => {});
  }, [eventId]);

  useDwell(eventId);

  async function handleStatusChange(next: EventStatus) {
    if (busy) return;
    setBusy(true);
    try {
      await api.setStatus(eventId, next);
      setStatus(next);
    } finally {
      setBusy(false);
    }
  }

  async function handleRate(v: number) {
    if (ratingBusy) return;
    setRatingBusy(true);
    try {
      await api.setRating(eventId, v);
      setRating(v);
    } finally {
      setRatingBusy(false);
    }
  }

  if (!ev) {
    return (
      <div className="max-w-[1100px] mx-auto px-4 py-8">
        <SkeletonCard />
      </div>
    );
  }

  return (
    <div className="max-w-[1100px] mx-auto px-4 py-8 text-white">
      <nav className="mb-4 text-sm">
        <Link to="/" className="text-sky-300 hover:text-sky-200">← Back</Link>
      </nav>

      <article className="rounded-3xl border border-white/10 bg-white/5 backdrop-blur-md shadow-[0_20px_60px_-25px_rgba(0,0,0,.6)] overflow-hidden">
        {/* Banner */}
        <header className="relative h-72 md:h-80">
          {ev.imageUrl ? (
            <img
              src={ev.imageUrl}
              alt={ev.title}
              className="absolute inset-0 h-full w-full object-cover"
              loading="lazy"
            />
          ) : (
            <div className="absolute inset-0 bg-gradient-to-br from-indigo-500/40 via-sky-500/40 to-purple-500/40" />
          )}
          <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-black/20 to-black/60" />

          {/* Top-left: category */}
          {ev.category && (
            <span className="absolute top-4 left-4 inline-flex items-center rounded-full bg-black/40 text-white/95 border border-white/20 px-3 py-1 text-sm backdrop-blur">
              {ev.category}
            </span>
          )}

          {/* Top-right: average rating */}
          {typeof ev.avgRating === "number" && (
            <div className="absolute top-4 right-4 rounded-full bg-black/40 border border-white/20 px-3 py-1 backdrop-blur">
              <RatingStars rating={ev.avgRating} size="sm" />
            </div>
          )}
        </header>

        {/* Body */}
        <div className="p-6 md:p-8">
          <h1 className="text-2xl md:text-3xl font-semibold tracking-tight mb-3">
            {ev.title}
          </h1>

          {/* Meta */}
          <ul className="space-y-1.5 text-slate-200/90 mb-6">
            <li className="flex items-center gap-2">
              <Calendar className="h-4 w-4 text-slate-300" />
              <span className="leading-6">
                {new Date(ev.dateTime).toLocaleString()}
              </span>
            </li>
            {!!ev.location && (
              <li className="flex items-center gap-2">
                <MapPin className="h-4 w-4 text-slate-300" />
                <span className="leading-6">{ev.location}</span>
              </li>
            )}
            {!!ev.organizer && (
              <li className="leading-6 text-slate-300">by {ev.organizer}</li>
            )}
          </ul>

          {/* Description */}
          {!!ev.description && (
            <section className="mb-6">
              <h2 className="text-lg font-semibold mb-2">About</h2>
              <p className="leading-7 text-slate-200">{ev.description}</p>
            </section>
          )}

          {/* Actions */}
          <section
            className={[
              "mt-6 rounded-2xl border border-white/10 bg-white/5 p-4",
              (busy || ratingBusy) ? "opacity-70 pointer-events-none" : "",
            ].join(" ")}
          >
            <div className="mb-3">
              <StatusButtons
                currentStatus={status}
                onStatusChange={handleStatusChange}
              />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm text-slate-300">Your rating:</span>
              <RatingSelect value={rating} onChange={handleRate} />
            </div>
          </section>

          {/* Social hint */}
          {typeof friendsGoing === "number" && (
            <p className="mt-4 text-sm text-slate-300">
              👥 {friendsGoing} {friendsGoing === 1 ? "friend is" : "friends are"} going
            </p>
          )}
        </div>
      </article>
    </div>
  );
}
