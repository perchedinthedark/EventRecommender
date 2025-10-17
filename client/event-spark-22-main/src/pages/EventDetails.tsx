// client/src/pages/EventDetails.tsx
import { useEffect, useMemo, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { api, EventDto } from "@/lib/api";
import { useDwell } from "@/pages/hooks/useDwell";
import { StatusButtons, EventStatus } from "@/components/StatusButtons";
import SkeletonCard from "@/components/SkeletonCard";
import { Calendar, MapPin, Share2, Link as LinkIcon } from "lucide-react";
import { RatingStars } from "@/components/RatingStars";
import RatingSelect from "@/components/RatingSelect";

function fmtDate(d: string) {
  try {
    const date = new Date(d);
    return new Intl.DateTimeFormat(undefined, {
      weekday: "short",
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  } catch {
    return new Date(d).toLocaleString();
  }
}

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

  function copyLink() {
    try {
      navigator.clipboard?.writeText(window.location.href);
    } catch {}
  }

  async function shareNative() {
    if (!ev) return;
    if (navigator.share) {
      try {
        await navigator.share({
          title: ev.title,
          text: ev.description ?? undefined,
          url: window.location.href,
        });
      } catch {}
    }
  }

  if (!ev) {
    return (
      <div className="max-w-[1100px] mx-auto px-4 py-8">
        <SkeletonCard />
      </div>
    );
  }

  const showShare = typeof navigator !== "undefined" && !!navigator.share;

  return (
    <div className="max-w-[1100px] mx-auto px-4 py-8 text-white">
      {/* Brand-y back link */}
      <nav className="mb-4 text-sm">
        <Link
          to="/"
          className="inline-flex items-center gap-1 text-transparent bg-clip-text
                     bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300 hover:opacity-90"
        >
          <span aria-hidden>«</span> Back
        </Link>
      </nav>

      <article
        className="
          overflow-hidden rounded-3xl bg-white/5 backdrop-blur-md
          ring-1 ring-white/10 hover:ring-white/20 transition
          shadow-[0_24px_80px_-30px_rgba(0,0,0,.65)]
        "
      >
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
          <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-black/25 to-black/60" />

          {/* Category chip */}
          {ev.category && (
            <span className="absolute top-4 left-4 inline-flex items-center rounded-full bg-black/45 text-white/95 border border-white/20 px-3 py-1 text-sm backdrop-blur">
              {ev.category}
            </span>
          )}

          {/* Average rating */}
          {typeof ev.avgRating === "number" && (
            <div className="absolute top-4 right-4 flex items-center gap-2 rounded-full bg-black/45 border border-white/20 px-3 py-1 backdrop-blur">
              <RatingStars rating={ev.avgRating} size="sm" />
              <span className="text-xs text-white/90">{ev.avgRating.toFixed(1)}</span>
            </div>
          )}
        </header>

        {/* Body */}
        <div className="p-6 md:p-8">
          <div className="flex items-start justify-between gap-4">
            <h1 className="text-2xl md:text-3xl font-semibold tracking-tight">{ev.title}</h1>

            {/* Small utility actions */}
            <div className="flex shrink-0 items-center gap-2">
              <button
                onClick={copyLink}
                className="inline-flex items-center gap-1 rounded-lg bg-white/7 px-2.5 py-1.5
                           text-xs text-white/90 border border-white/10 hover:bg-white/10"
                title="Copy link"
              >
                <LinkIcon className="h-3.5 w-3.5" />
                Copy
              </button>
              {showShare && (
                <button
                  onClick={shareNative}
                  className="inline-flex items-center gap-1 rounded-lg bg-white/7 px-2.5 py-1.5
                             text-xs text-white/90 border border-white/10 hover:bg-white/10"
                  title="Share"
                >
                  <Share2 className="h-3.5 w-3.5" />
                  Share
                </button>
              )}
            </div>
          </div>

          {/* Meta */}
          <ul className="mt-3 mb-6 flex flex-wrap items-center gap-3 text-sm">
            <li className="inline-flex items-center gap-2 rounded-full bg-white/7 border border-white/10 px-3 py-1.5">
              <Calendar className="h-4 w-4 text-slate-300" />
              <time className="leading-6" dateTime={new Date(ev.dateTime).toISOString()}>
                {fmtDate(ev.dateTime)}
              </time>
            </li>
            {!!ev.location && (
              <li className="inline-flex items-center gap-2 rounded-full bg-white/7 border border-white/10 px-3 py-1.5">
                <MapPin className="h-4 w-4 text-slate-300" />
                <span className="leading-6">{ev.location}</span>
              </li>
            )}
            {!!ev.organizer && (
              <li className="inline-flex items-center gap-2 rounded-full bg-white/7 border border-white/10 px-3 py-1.5">
                <span className="text-slate-300">by</span>
                <span className="leading-6">{ev.organizer}</span>
              </li>
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
              "mt-6 rounded-2xl border border-white/10 bg-white/6 p-4",
              busy || ratingBusy ? "opacity-70 pointer-events-none" : "",
            ].join(" ")}
          >
            <div className="mb-3">
              <StatusButtons currentStatus={status} onStatusChange={handleStatusChange} />
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
