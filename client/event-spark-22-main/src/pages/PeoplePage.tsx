import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { SectionHeader } from "@/components/SectionHeader";
import { cn } from "@/lib/utils";

type UserLite = { id: string; userName?: string; displayName?: string; email?: string };

const displayOrEmail = (u: UserLite) => (u.displayName?.trim() || u.email || u.userName || "User");

function Avatar({ user }: { user: UserLite }) {
  const initial = (displayOrEmail(user).trim()[0] || "?").toUpperCase();
  return (
    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white/10 border border-white/15 text-slate-100">
      {initial}
    </div>
  );
}

function PersonRow({
  user,
  right,
  muted,
}: {
  user: UserLite;
  right?: React.ReactNode;
  muted?: boolean;
}) {
  return (
    <li
      className={cn(
        "rounded-2xl px-4 py-3 border flex items-center justify-between gap-3 transition-colors",
        muted
          ? // results (slightly lighter)
            "bg-gradient-to-b from-indigo-300/6 to-sky-300/4 border-white/10 hover:from-indigo-300/10 hover:to-sky-300/7"
          : // main lists (a touch richer)
            "bg-gradient-to-b from-indigo-300/10 to-sky-300/6 border-white/12 hover:from-indigo-300/14 hover:to-sky-300/9 shadow-[0_8px_20px_-12px_rgba(0,0,0,.5)]"
      )}
    >
      <div className="min-w-0 flex items-center gap-3">
        <Avatar user={user} />
        <div className="truncate font-medium text-slate-100">{displayOrEmail(user)}</div>
      </div>
      <div className="flex items-center gap-2">{right}</div>
    </li>
  );
}

export default function PeoplePage() {
  const [following, setFollowing] = useState<UserLite[]>([]);
  const [followers, setFollowers] = useState<UserLite[]>([]);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UserLite[]>([]);
  const [busy, setBusy] = useState(false);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refreshLists() {
    try {
      const [f1, f2] = await Promise.all([
        api.social.following().catch(() => [] as UserLite[]),
        api.social.followers().catch(() => [] as UserLite[]),
      ]);
      setFollowing(f1);
      setFollowers(f2);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load people.");
    }
  }

  useEffect(() => {
    refreshLists();
  }, []);

  async function search() {
    const term = query.trim();
    if (!term) {
      setResults([]);
      return;
    }
    setSearching(true);
    try {
      const found = await api.users.search(term);
      setResults(found);
    } finally {
      setSearching(false);
    }
  }

  async function follow(id: string) {
    if (busy) return;
    setBusy(true);
    try {
      await api.social.follow(id);
      await refreshLists();
      if (query.trim()) await search();
    } finally {
      setBusy(false);
    }
  }

  async function unfollow(id: string) {
    if (busy) return;
    setBusy(true);
    try {
      await api.social.unfollow(id);
      await refreshLists();
      if (query.trim()) await search();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen page-surface text-[hsl(var(--foreground))]">
      <main className="mx-auto max-w-[1100px] px-4 py-8">
        <SectionHeader title="People" className="mb-6" />

        {!!error && (
          <div className="mb-4 rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-200">
            {error}
          </div>
        )}

        {/* Search */}
        <div className="mb-6 flex items-center gap-3">
          <input
            className="flex-1 rounded-xl bg-white/[.04] border border-white/12 px-4 py-2 text-slate-100 placeholder-slate-400
                       focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400/40"
            placeholder="Search people"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && search()}
          />
          <button onClick={search} className="pill-button disabled:opacity-60" disabled={searching}>
            {searching ? "Searching…" : "Search"}
          </button>
        </div>

        {/* Following / Followers */}
        <div className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-2">
          <section className="card-surface rounded-2xl border border-white/12 p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-100">Following</h2>
              <span className="rounded-full bg-white/10 px-2.5 py-0.5 text-xs text-slate-200 border border-white/15">
                {following.length}
              </span>
            </div>
            <ul className="space-y-2">
              {following.length === 0 && (
                <li className="text-slate-300/80">You’re not following anyone yet.</li>
              )}
              {following.map((u) => (
                <PersonRow
                  key={u.id}
                  user={u}
                  right={
                    <button
                      onClick={() => unfollow(u.id)}
                      className="text-red-300 hover:text-red-200 disabled:opacity-60"
                      disabled={busy}
                    >
                      Unfollow
                    </button>
                  }
                />
              ))}
            </ul>
          </section>

          <section className="card-surface rounded-2xl border border-white/12 p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-100">Followers</h2>
              <span className="rounded-full bg-white/10 px-2.5 py-0.5 text-xs text-slate-200 border border-white/15">
                {followers.length}
              </span>
            </div>
            <ul className="space-y-2">
              {followers.length === 0 && <li className="text-slate-300/80">No followers yet.</li>}
              {followers.map((u) => {
                const iFollow = following.some((f) => f.id === u.id);
                return (
                  <PersonRow
                    key={u.id}
                    user={u}
                    right={
                      iFollow ? (
                        <span className="rounded-md bg-white/10 px-2 py-1 text-xs text-slate-200 border border-white/15">
                          Following
                        </span>
                      ) : (
                        <button
                          onClick={() => follow(u.id)}
                          className="pill-button px-3 py-1 text-sm disabled:opacity-60"
                          disabled={busy}
                        >
                          Follow back
                        </button>
                      )
                    }
                  />
                );
              })}
            </ul>
          </section>
        </div>

        {/* Results */}
        <section className="card-surface rounded-2xl border border-white/12 p-5">
          <h2 className="mb-4 text-lg font-semibold text-slate-100">Results</h2>
          <ul className="space-y-2">
            {results.length === 0 && <li className="text-slate-300/80">No results</li>}
            {results.map((u) => {
              const iFollow = following.some((f) => f.id === u.id);
              return (
                <PersonRow
                  key={u.id}
                  user={u}
                  muted
                  right={
                    iFollow ? (
                      <button
                        onClick={() => unfollow(u.id)}
                        className="text-red-300 hover:text-red-200 disabled:opacity-60"
                        disabled={busy}
                      >
                        Unfollow
                      </button>
                    ) : (
                      <button
                        onClick={() => follow(u.id)}
                        className="pill-button px-3 py-1 text-sm disabled:opacity-60"
                        disabled={busy}
                      >
                        Follow
                      </button>
                    )
                  }
                />
              );
            })}
          </ul>
        </section>
      </main>
    </div>
  );
}
