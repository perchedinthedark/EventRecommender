import { useEffect, useState } from "react";
import { api } from "@/lib/api";

type UserLite = { id: string; userName: string; email: string };

export default function PeoplePage() {
  const [me, setMe] = useState<UserLite | null>(null);
  const [following, setFollowing] = useState<UserLite[]>([]);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UserLite[]>([]);

  useEffect(() => {
    api.auth.me().then(setMe).catch(()=>setMe(null));
    api.social.following().then(setFollowing).catch(()=>setFollowing([]));
  }, []);

  async function search() {
    // ultra-simple demo search: reuse following list if you don't have a users endpoint yet
    // replace with api.users.search(query) when you add it
    setResults([]);
    if (!query.trim()) return;
    // fallback: show following as "results"
    setResults(following.filter(u => (u.userName ?? u.email).toLowerCase().includes(query.toLowerCase())));
  }

  async function follow(id: string) {
    await api.social.follow(id);
    const list = await api.social.following();
    setFollowing(list);
  }
  async function unfollow(id: string) {
    await api.social.unfollow(id);
    const list = await api.social.following();
    setFollowing(list);
  }

  return (
    <div className="max-w-[900px] mx-auto px-4 py-6">
      <h1 className="text-2xl font-semibold mb-4">People</h1>
      {!me && <div className="text-slate-600 mb-3">Log in to follow people.</div>}

      <div className="flex gap-2 mb-6">
        <input className="flex-1 border border-slate-300 rounded-lg p-2" placeholder="Search people (demo)"
               value={query} onChange={e=>setQuery(e.target.value)} />
        <button onClick={search} className="px-3 rounded-lg bg-slate-900 text-white">Search</button>
      </div>

      <h2 className="text-lg font-semibold mb-2">Following</h2>
      <ul className="space-y-2 mb-6">
        {following.length === 0 && <li className="text-slate-600">You’re not following anyone yet.</li>}
        {following.map(u => (
          <li key={u.id} className="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2 bg-white">
            <div>
              <div className="font-medium">{u.userName ?? u.email}</div>
              <div className="text-sm text-slate-600">{u.email}</div>
            </div>
            <button onClick={() => unfollow(u.id)} className="text-red-600 hover:underline">Unfollow</button>
          </li>
        ))}
      </ul>

      <h2 className="text-lg font-semibold mb-2">Results</h2>
      <ul className="space-y-2">
        {results.length === 0 && <li className="text-slate-600">No results</li>}
        {results.map(u => (
          <li key={u.id} className="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2 bg-white">
            <div>
              <div className="font-medium">{u.userName ?? u.email}</div>
              <div className="text-sm text-slate-600">{u.email}</div>
            </div>
            <button onClick={() => follow(u.id)} className="text-blue-600 hover:underline">Follow</button>
          </li>
        ))}
      </ul>
    </div>
  );
}
