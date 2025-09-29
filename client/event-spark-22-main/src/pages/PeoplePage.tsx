import { useEffect, useState } from "react";
import { api } from "@/lib/api";

type UserLite = { id: string; userName: string; email: string };

export default function PeoplePage() {
  const [me, setMe] = useState<UserLite | null>(null);
  const [following, setFollowing] = useState<UserLite[]>([]);
  const [followers, setFollowers] = useState<UserLite[]>([]);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UserLite[]>([]);
  const [busy, setBusy] = useState(false);

  async function refreshLists() {
    const [f1, f2] = await Promise.all([
      api.social.following().catch(() => [] as UserLite[]),
      api.social.followers().catch(() => [] as UserLite[]),
    ]);
    setFollowing(f1);
    setFollowers(f2);
  }

  useEffect(() => {
    api.auth.me().then(setMe).catch(() => setMe(null));
    refreshLists();
  }, []);

  async function search() {
    setResults([]);
    const term = query.trim();
    if (!term) return;
    try {
      const found = await api.users.search(term);
      setResults(found);
    } catch {
      setResults([]);
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
    <div className="min-h-screen bg-[hsl(var(--background))]">
      <div className="max-w-[1000px] mx-auto px-4 py-8">
        <div className="bg-white border border-slate-200 rounded-2xl shadow-lg p-6">
          <h1 className="text-2xl font-semibold mb-4 text-slate-900">People</h1>
          {!me && <div className="text-slate-600 mb-4">Log in to follow people.</div>}

          {/* Search */}
          <div className="flex gap-2 mb-8">
            <input
              className="flex-1 border border-slate-300 rounded-lg p-2 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
              placeholder="Search people"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && search()}
            />
            <button
              onClick={search}
              className="px-4 rounded-lg bg-blue-600 hover:bg-blue-700 text-white"
            >
              Search
            </button>
          </div>

          {/* Columns */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Following */}
            <section className="border border-slate-200 rounded-xl p-4">
              <h2 className="text-lg font-semibold mb-3 text-slate-900">Following</h2>
              <ul className="space-y-2">
                {following.length === 0 && (
                  <li className="text-slate-500">You’re not following anyone yet.</li>
                )}
                {following.map((u) => (
                  <li
                    key={u.id}
                    className="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2 bg-white"
                  >
                    <div>
                      <div className="font-medium">{u.userName ?? u.email}</div>
                      <div className="text-sm text-slate-600">{u.email}</div>
                    </div>
                    <button
                      onClick={() => unfollow(u.id)}
                      className="text-red-600 hover:underline disabled:opacity-60"
                      disabled={busy}
                    >
                      Unfollow
                    </button>
                  </li>
                ))}
              </ul>
            </section>

            {/* Followers */}
            <section className="border border-slate-200 rounded-xl p-4">
              <h2 className="text-lg font-semibold mb-3 text-slate-900">Followers</h2>
              <ul className="space-y-2">
                {followers.length === 0 && (
                  <li className="text-slate-500">No one follows you yet.</li>
                )}
                {followers.map((u) => {
                  const iFollow = following.some((f) => f.id === u.id);
                  return (
                    <li
                      key={u.id}
                      className="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2 bg-white"
                    >
                      <div>
                        <div className="font-medium">{u.userName ?? u.email}</div>
                        <div className="text-sm text-slate-600">{u.email}</div>
                      </div>
                      {iFollow ? (
                        <span className="text-xs px-2 py-1 rounded bg-slate-100 text-slate-700">
                          Following
                        </span>
                      ) : (
                        <button
                          onClick={() => follow(u.id)}
                          className="text-blue-600 hover:underline disabled:opacity-60"
                          disabled={busy}
                        >
                          Follow back
                        </button>
                      )}
                    </li>
                  );
                })}
              </ul>
            </section>
          </div>

          {/* Results */}
          <div className="mt-8">
            <h2 className="text-lg font-semibold mb-2 text-slate-900">Results</h2>
            <ul className="space-y-2">
              {results.length === 0 && (
                <li className="text-slate-500">No results</li>
              )}
              {results.map((u) => {
                const iFollow = following.some((f) => f.id === u.id);
                return (
                  <li
                    key={u.id}
                    className="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2 bg-white"
                  >
                    <div>
                      <div className="font-medium">{u.userName ?? u.email}</div>
                      <div className="text-sm text-slate-600">{u.email}</div>
                    </div>
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
          </div>
        </div>
      </div>
    </div>
  );
}
