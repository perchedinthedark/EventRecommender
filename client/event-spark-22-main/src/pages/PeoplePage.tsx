import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useNavigate } from "react-router-dom";

type UserLite = { id: string; userName?: string; displayName?: string; email?: string };

const nameOf = (u: UserLite) => u.displayName || u.userName || "User";

export default function PeoplePage() {
  const nav = useNavigate();

  const [me, setMe] = useState<UserLite | null>(null);
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
    api.auth.me().then(setMe).catch(() => setMe(null));
    refreshLists();
  }, []);

  async function search() {
    const term = query.trim();
    if (!term) { setResults([]); return; }
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
    } finally { setBusy(false); }
  }

  async function unfollow(id: string) {
    if (busy) return;
    setBusy(true);
    try {
      await api.social.unfollow(id);
      await refreshLists();
      if (query.trim()) await search();
    } finally { setBusy(false); }
  }

  const Card = ({ u, right }: { u: UserLite; right?: React.ReactNode }) => (
    <li className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
      <div className="font-medium text-slate-900">{nameOf(u)}</div>
      {right}
    </li>
  );

  return (
    <div className="min-h-screen bg-[hsl(var(--background))]">
      <nav className="px-4 py-4 border-b border-slate-200 bg-white/70 backdrop-blur supports-[backdrop-filter]:bg-white/50">
        <div className="max-w-[1100px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button onClick={() => nav(-1)} className="text-blue-600 hover:underline">← Back</button>
            <strong className="text-lg text-slate-900">People</strong>
          </div>
          {me && <span className="text-sm text-slate-600">Signed in as {nameOf(me)}</span>}
        </div>
      </nav>

      <main className="max-w-[1100px] mx-auto px-4 py-8">
        {!!error && (
          <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        <div className="mb-6 flex items-center gap-3">
          <input
            className="flex-1 rounded-xl border border-slate-300 bg-white px-4 py-2 text-slate-900 placeholder-slate-500
                       focus:outline-none focus:ring-2 focus:ring-blue-500/30"
            placeholder="Search people"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && search()}
          />
          <button
            onClick={search}
            className="rounded-xl bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 disabled:opacity-60"
            disabled={searching}
          >
            {searching ? "Searching…" : "Search"}
          </button>
        </div>

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2 mb-10">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-3 text-lg font-semibold text-slate-900">Following</h2>
            <ul className="space-y-2">
              {following.length === 0 && <li className="text-slate-500">You’re not following anyone yet.</li>}
              {following.map((u) => (
                <Card
                  key={u.id}
                  u={u}
                  right={
                    <button
                      onClick={() => unfollow(u.id)}
                      className="text-red-600 hover:underline disabled:opacity-60"
                      disabled={busy}
                    >
                      Unfollow
                    </button>
                  }
                />
              ))}
            </ul>
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-3 text-lg font-semibold text-slate-900">Followers</h2>
            <ul className="space-y-2">
              {followers.length === 0 && <li className="text-slate-500">No followers yet.</li>}
              {followers.map((u) => {
                const iFollow = following.some((f) => f.id === u.id);
                return (
                  <Card
                    key={u.id}
                    u={u}
                    right={
                      iFollow ? (
                        <span className="rounded-md bg-slate-100 px-2 py-1 text-xs text-slate-700">Following</span>
                      ) : (
                        <button
                          onClick={() => follow(u.id)}
                          className="text-blue-600 hover:underline disabled:opacity-60"
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

        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="mb-3 text-lg font-semibold text-slate-900">Results</h2>
          <ul className="space-y-2">
            {results.length === 0 && <li className="text-slate-500">No results</li>}
            {results.map((u) => {
              const iFollow = following.some((f) => f.id === u.id);
              return (
                <li key={u.id} className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-3">
                  <div className="font-medium text-slate-900">{nameOf(u)}</div>
                  {iFollow ? (
                    <button
                      onClick={() => unfollow(u.id)}
                      className="text-red-600 hover:underline disabled:opacity-60"
                      disabled={busy}
                    >
                      Unfollow
                    </button>
                  ) : (
                    <button
                      onClick={() => follow(u.id)}
                      className="text-blue-600 hover:underline disabled:opacity-60"
                      disabled={busy}
                    >
                      Follow
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      </main>
    </div>
  );
}
