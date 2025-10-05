// client/src/components/EventCard.tsx
import { useEffect, useState } from "react";
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

  return (
    <div
      className={cn(
        "bg-white rounded-[24px] border border-slate-200 shadow-lg overflow-hidden",
        "transition-all duration-200 hover:shadow-xl"
      )}
    >
      {/* Banner: image if present, else gradient */}
      <div className="h-36 relative overflow-hidden">
        {ev.imageUrl ? (
          <img
            src={ev.imageUrl}
            alt={ev.title}
            className="h-full w-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="h-full w-full bg-gradient-to-b from-blue-400 to-blue-300" />
        )}

        {ev.category && (
          <span className="absolute top-3 left-3 inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-white/90 text-slate-800 shadow">
            {ev.category}
          </span>
        )}

        {typeof ev.avgRating === "number" && (
          <div className="absolute top-3 right-3 bg-white/90 rounded-full px-3 py-1 shadow">
            <RatingStars rating={ev.avgRating} size="sm" />
          </div>
        )}
      </div>

      <div className="p-5">
        <h5 className="text-[18px] leading-6 font-semibold text-slate-900 mb-1">
          <Link to={`/event/${ev.id}`} className="hover:underline">
            {ev.title}
          </Link>
        </h5>

        <div className="space-y-1.5 text-[14px] text-slate-600 mb-2.5">
          <div className="flex items-center gap-2">
            <Calendar className="w-4 h-4 text-slate-500" />
            <span>{new Date(ev.dateTime).toLocaleString()}</span>
          </div>
          {ev.location && (
            <div className="flex items-center gap-2">
              <MapPin className="w-4 h-4 text-slate-500" />
              <span>{ev.location}</span>
            </div>
          )}
          {ev.organizer && <div className="text-slate-500">by {ev.organizer}</div>}
        </div>

        <div className={cn("mt-3 space-y-3", (busy || ratingBusy) && "opacity-70 pointer-events-none")}>
          <StatusButtons currentStatus={status} onStatusChange={handleStatusChange} />
          <div className="flex items-center gap-2">
            <span className="text-sm text-slate-600">Your rating:</span>
            <RatingSelect value={myRating ?? null} onChange={handleRate} size="sm" />
          </div>
        </div>

        <div className="border-t border-slate-200 mt-4 pt-3 flex items-center justify-between">
          <AvatarStack count={(ev as any).friendsGoing ?? 5} size="sm" />
          <Link to={`/event/${ev.id}`} className="text-blue-600 hover:underline text-sm">
            Details →
          </Link>
        </div>
      </div>
    </div>
  );
}


