import { useEffect, useState } from "react";
import { NavLink, Link, useNavigate } from "react-router-dom";
import { api, authEvents } from "@/lib/api";
import { cn } from "@/lib/utils";

const linkBase =
  "inline-flex items-center h-9 px-3 rounded-full text-sm transition-colors";
const linkIdle = "text-slate-300 hover:text-white hover:bg-white/10";
const linkActive = "text-white bg-white/15";

export default function Header() {
  const nav = useNavigate();
  const [me, setMe] = useState<{
    id: string;
    userName?: string;
    displayName?: string | null;
    email: string;
  } | null>(null);

  async function refreshMe() {
    try {
      const u = await api.auth.me();
      setMe(u);
    } catch {
      setMe(null);
    }
  }

  useEffect(() => {
    let cancel = false;
    api.auth.me().then((u) => !cancel && setMe(u)).catch(() => {});
    const off = authEvents.on(() => refreshMe());
    return () => {
      cancel = true;
      off();
    };
  }, []);

  const displayName = me?.displayName ?? me?.userName ?? me?.email ?? "";

  return (
    <header className="sticky top-0 z-50 bg-slate-950/70 backdrop-blur border-b border-white/10">
      <div className="mx-auto max-w-[1200px] px-4">
        <div className="h-14 flex items-center justify-between gap-3">
          <Link to="/" className="flex items-baseline gap-2">
            <span className="text-lg font-semibold tracking-tight bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300 bg-clip-text text-transparent">
              Eventualno
            </span>
          </Link>

          <nav className="hidden md:flex items-center gap-1">
            <NavLink to="/search" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>Search</NavLink>
            <NavLink to="/trending" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>Trending</NavLink>
            <NavLink to="/recs" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>Recommendations</NavLink>
            <NavLink to="/people" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>People</NavLink>
            <NavLink to="/saved/interested" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>Interested</NavLink>
            <NavLink to="/saved/going" className={({ isActive }) => cn(linkBase, isActive ? linkActive : linkIdle)}>Going</NavLink>
          </nav>

          <div className="flex items-center gap-2">
            {me ? (
              <>
                <span className="hidden sm:block text-sm text-slate-300">
                  Hi, <span className="text-white font-medium">{displayName}</span>
                </span>
                <button
                  onClick={async () => {
                    await api.auth.logout(); // emits auth:changed
                    nav("/"); // ensures redirect even if already on /
                  }}
                  className="inline-flex h-9 items-center rounded-full px-3 text-sm text-white bg-white/10 hover:bg-white/15 transition-colors"
                >
                  Logout
                </button>
              </>
            ) : (
              <>
                <Link
                  to="/login"
                  className="inline-flex h-9 items-center rounded-full px-3 text-sm text-slate-200 hover:text-white hover:bg-white/10 transition-colors"
                >
                  Log in
                </Link>
                <Link
                  to="/register"
                  className="inline-flex h-9 items-center rounded-full px-3 text-sm text-slate-900 bg-white hover:bg-slate-100 transition-colors"
                >
                  Register
                </Link>
              </>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
