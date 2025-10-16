import { useRef, useEffect, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { cn } from "@/lib/utils";

export default function SearchBand({
  defaultQuery = "",
  placeholder = "Search events (e.g., “live jazz this weekend”, “AI meetup”)",
  autoFocus = false,
  variant = "light",
  className,
}: {
  defaultQuery?: string;
  placeholder?: string;
  autoFocus?: boolean;
  variant?: "light" | "dark";
  className?: string;
}) {
  const [q, setQ] = useState(defaultQuery);
  const nav = useNavigate();
  const loc = useLocation();
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (autoFocus && inputRef.current) inputRef.current.focus();
  }, [autoFocus]);

  function go() {
    const term = q.trim();
    if (!term) return;
    if (loc.pathname.startsWith("/search")) {
      nav(`/search?q=${encodeURIComponent(term)}`, { replace: false });
    } else {
      nav(`/search?q=${encodeURIComponent(term)}`);
    }
  }

  const container =
    variant === "dark"
      // softer border, darker glass; no bright white edge
      ? "rounded-2xl border border-white/5 bg-white/5 backdrop-blur-md p-4"
      : "rounded-2xl border border-slate-200 bg-white p-4 shadow-sm";

  const input =
    variant === "dark"
      // remove inner white border; subtle focus ring only
      ? "flex-1 rounded-xl border border-transparent bg-white/10 px-4 py-2 text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-400/30"
      : "flex-1 rounded-xl border border-slate-300 bg-white px-4 py-2 text-slate-900 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30";

  const button =
    variant === "dark"
      ? "rounded-xl bg-blue-500 px-4 py-2 text-white hover:bg-blue-400 disabled:opacity-60"
      : "rounded-xl bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 disabled:opacity-60";

  return (
    <div className={cn(container, className)}>
      <div className="flex items-center gap-2">
        <input
          ref={inputRef}
          className={input}
          placeholder={placeholder}
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && go()}
        />
        <button onClick={go} className={button} disabled={!q.trim()}>
          Search
        </button>
      </div>
    </div>
  );
}
