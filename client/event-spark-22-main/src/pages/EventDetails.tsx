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
      <div className="max-w-[1000px] mx-auto px-4 py-6">
        <SkeletonCard />
      </div>
    );
  }

  return (
    <div className="max-w-[1000px] mx-auto px-4 py-6">
      <nav className="mb-4 text-sm">
        <Link to="/" className="text-blue-600 hover:underline">
          ← Back
        </Link>
      </nav>

      <div className="bg-white border border-slate-200 rounded-2xl shadow-lg overflow-hidden">
        <div className="h-44 bg-gradient-to-b from-blue-400 to-blue-300" />
        <div className="p-6">
          <h1 className="text-2xl font-semibold text-slate-900 mb-2">{ev.title}</h1>

          {!!rating && (
            <div className="mb-3">
              <RatingStars rating={rating} size="md" />
            </div>
          )}

          <div className="space-y-1.5 text-[14px] text-slate-600 mb-4">
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

          {!!ev.description && (
            <>
              <h2 className="text-lg font-semibold text-slate-900 mb-1">About</h2>
              <p className="text-slate-700 leading-7">{ev.description}</p>
            </>
          )}

          <div className={(busy || ratingBusy) ? "opacity-70 pointer-events-none mt-5" : "mt-5"}>
            <StatusButtons currentStatus={status} onStatusChange={handleStatusChange} />
            <div className="mt-3 flex items-center gap-2">
              <span className="text-sm text-slate-600">Your rating:</span>
              <RatingSelect value={rating} onChange={handleRate} />
            </div>
          </div>

          <div className="mt-4 text-sm text-slate-600">
            {typeof friendsGoing === "number" && <span>👥 {friendsGoing} of your friends are going</span>}
          </div>
        </div>
      </div>
    </div>
  );
}
