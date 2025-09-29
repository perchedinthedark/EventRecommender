import { useEffect, useRef } from "react";
import { api } from "@/lib/api";

export function useDwell(eventId: number) {
  const start = useRef<number>(Date.now());

  useEffect(() => {
    if (!eventId) return;
    const send = () => {
      const dwellMs = Date.now() - start.current;
      api.telemetry.dwell(eventId, dwellMs).catch(()=>{});
    };
    window.addEventListener("pagehide", send);
    window.addEventListener("beforeunload", send);
    return () => {
      window.removeEventListener("pagehide", send);
      window.removeEventListener("beforeunload", send);
      send();
    };
  }, [eventId]);
}
