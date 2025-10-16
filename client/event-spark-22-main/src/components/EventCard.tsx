import { useEffect, useMemo, useState } from "react";
import { Calendar, MapPin } from "lucide-react";
import { api, EventDto } from "@/lib/api";
import { StatusButtons, EventStatus } from "./StatusButtons";
import { AvatarStack } from "./AvatarStack";
import { RatingStars } from "./RatingStars";
import RatingSelect from "./RatingSelect";
import { Link } from "react-router-dom";
import { cn } from "@/lib/utils";

export default function EventCard({ ev }: { ev: EventDto }) {
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState<EventStatus>("None");
  const [myRating, setMyRating] = useState<number | undefined>(undefined);
  const [ratingBusy, setRatingBusy] = useState(false);
  const [friendsGoing, setFriendsGoing] = useState<number | null>(null);

  useEffect(() => {
    let ignore = false;

    api
      .getMyInteraction(ev.id)
      .then((m) => {
        if (ignore) return;
        if (m?.status) setStatus(m.status as EventStatus);
        if (typeof m?.rating === "number") setMyRating(m.rating);
      })
      .catch(() => {});

    api.social
      .friendsGoing(ev.id)
      .then((r) => !ignore && setFriendsGoing(r.count))
      .catch(() => !ignore && setFriendsGoing(null));

    return () => {
      ignore = true;
    };
  }, [ev.id]);

  async function handleStatusChange(next: EventStatus) {
    if (busy) return;
    setBusy(true);
    try {
      await api.setStatus(ev.id, next);
      setStatus(next);
    } finally {
      setBusy(false);
    }
  }

  async function handleRate(v: number) {
    if (ratingBusy) return;
    setRatingBusy(true);
    try {
      await api.setRating(ev.id, v);
      setMyRating(v);
    } finally {
      setRatingBusy(false);
    }
  }

  const friendsLabel = useMemo(() => {
    if (friendsGoing === null) return "";
    if (friendsGoing <= 0) return "";
    if (friendsGoing === 1) return "1 friend going";
    return `${friendsGoing} friends going`;
  }, [friendsGoing]);

  return (
    <div
      className={cn(
        "card-surface overflow-hidden rounded-[24px]",
        // subtle outer ring + hover pop
        "ring-1 ring-white/12 hover:ring-white/25 transition"
      )}
    >
      {/* Banner */}
      <div className="relative h-40 overflow-hidden rounded-t-[24px]">
        {ev.imageUrl ? (
          <img
            src={ev.imageUrl}
            alt={ev.title}
            className="h-full w-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="h-full w-full bg-gradient-to-b from-blue-500/60 to-indigo-500/60" />
        )}

        {/* Category chip – higher contrast on dark images */}
        {!!ev.category && (
          <span className="absolute top-3 left-3 inline-flex items-center px-3 py-1 rounded-full text-[12px] font-medium text-white bg-black/45 backdrop-blur border border-white/20 shadow">
            {ev.category}
          </span>
        )}

        {/* Average rating */}
        {typeof ev.avgRating === "number" && (
          <div className="absolute top-3 right-3 rounded-full px-3 py-1 bg-black/55 backdrop-blur border border-white/15 shadow">
            <RatingStars rating={ev.avgRating} size="sm" />
          </div>
        )}
      </div>

      {/* Body */}
      <div className="p-5">
        <h5 className="text-[18px] leading-6 font-semibold text-white mb-1">
          <Link to={`/event/${ev.id}`} className="hover:underline">
            {ev.title}
          </Link>
        </h5>

        <div className="space-y-1.5 text-[14px] text-white/80 mb-2.5">
          <div className="flex items-center gap-2">
            <Calendar className="w-4 h-4 text-white/70" />
            <span>{new Date(ev.dateTime).toLocaleString()}</span>
          </div>
          {ev.location && (
            <div className="flex items-center gap-2">
              <MapPin className="w-4 h-4 text-white/70" />
              <span>{ev.location}</span>
            </div>
          )}
          {ev.organizer && <div className="text-white/70">by {ev.organizer}</div>}
        </div>

        {/* Interactions */}
        <div
          className={cn(
            "mt-3 space-y-3",
            (busy || ratingBusy) && "opacity-60 pointer-events-none"
          )}
        >
          <StatusButtons currentStatus={status} onStatusChange={handleStatusChange} />
          <div className="flex items-center gap-2">
            <span className="text-sm text-white/80">Your rating:</span>
            <RatingSelect value={myRating ?? null} onChange={handleRate} size="sm" />
          </div>
        </div>

        {/* Divider */}
        <div className="card-divider my-4" />

        {/* Footer: friends + details */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 min-w-0">
            <AvatarStack count={friendsGoing ?? 0} size="sm" />
            {!!friendsLabel && (
              <span className="truncate text-xs text-white/70">{friendsLabel}</span>
            )}
          </div>
          <Link
            to={`/event/${ev.id}`}
            className="text-orange-300 hover:text-orange-200 text-sm"
          >
            Details »
          </Link>
        </div>
      </div>
    </div>
  );
}
