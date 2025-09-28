import { useEffect, useState } from "react";
import { Calendar, MapPin } from "lucide-react";
import { api, EventDto } from "@/lib/api";
import { StatusButtons, EventStatus } from "./StatusButtons";
import { AvatarStack } from "./AvatarStack";
import { cn } from "@/lib/utils";

const WEB_BASE = (import.meta.env.VITE_WEB_BASE || "").replace(/\/$/, "");

export default function EventCard({ ev }: { ev: EventDto }) {
  const href = `${WEB_BASE}/Events/TrackAndShow/${ev.id}`;

  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState<EventStatus>("None");

  // optional hydration (works if you added /api/events/{id}/me)
  useEffect(() => {
    let ignore = false;
    api.getMyInteraction(ev.id).then((m) => {
      if (!ignore && m?.status) setStatus(m.status as EventStatus);
    }).catch(()=>{});
    return () => { ignore = true; };
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

  // fake social numbers if you don’t have them yet (omit if you do)
  const going = (ev as any).going ?? undefined;
  const interested = (ev as any).interested ?? undefined;

  return (
    <div
      className={cn(
        "bg-white rounded-[24px] border border-slate-200 shadow-lg overflow-hidden",
        "transition-all duration-200 hover:shadow-xl"
      )}
    >
      {/* gradient banner */}
      <div className="h-36 bg-gradient-to-b from-blue-400 to-blue-300 relative">
        {ev.category && (
          <span className="absolute top-3 left-3 inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-white/90 text-slate-800 shadow">
            {ev.category}
          </span>
        )}
      </div>

      {/* content */}
      <div className="p-5">
        <h5 className="text-[18px] leading-6 font-semibold text-slate-900 mb-1">
          <a href={href} className="hover:underline">
            {ev.title}
          </a>
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
          {ev.organizer && (
            <div className="text-slate-500">by {ev.organizer}</div>
          )}
        </div>

        {/* actions (Interested / Going) */}
        <div className={cn("mt-3", busy && "opacity-70 pointer-events-none")}>
          <StatusButtons currentStatus={status} onStatusChange={handleStatusChange} />
        </div>

        {/* divider */}
        <div className="border-t border-slate-200 mt-4 pt-3 flex items-center justify-between">
          {/* avatar stack – replace count with your real friendsGoing if you have it */}
          <AvatarStack count={(ev as any).friendsGoing ?? 5} size="sm" />

          {/* simple stats */}
          <div className="flex items-center gap-4 text-[14px]">
            {typeof going === "number" && (
              <span className="text-emerald-600 font-medium">{going} going</span>
            )}
            {typeof interested === "number" && (
              <a href={href} className="text-blue-600 hover:underline">
                {interested} interested
              </a>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
