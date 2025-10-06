import { useEffect, useRef, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { cn } from "@/lib/utils";

export default function SearchBand({
  defaultQuery = "",
  placeholder = "Search events (e.g., “live jazz this weekend”, “AI meetup”)",
  autoFocus = false,
  className,
}: {
  defaultQuery?: string;
  placeholder?: string;
  autoFocus?: boolean;
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
    // avoid double-push when already on /search
    if (loc.pathname.startsWith("/search")) {
      nav(`/search?q=${encodeURIComponent(term)}`, { replace: false });
    } else {
      nav(`/search?q=${encodeURIComponent(term)}`);
    }
  }

  return (
    <div
      className={cn(
        "rounded-2xl border border-slate-200 bg-white p-4 shadow-sm",
        className
      )}
    >
      <div className="flex items-center gap-2">
        <input
          ref={inputRef}
          className="flex-1 rounded-xl border border-slate-300 bg-white px-4 py-2 text-slate-900 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
          placeholder={placeholder}
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && go()}
        />
        <button
          onClick={go}
          className="rounded-xl bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 disabled:opacity-60"
          disabled={!q.trim()}
        >
          Search
        </button>
      </div>
    </div>
  );
}
