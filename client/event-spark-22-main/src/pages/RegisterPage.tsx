// client/src/pages/RegisterPage.tsx
import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";

export default function RegisterPage() {
  const nav = useNavigate();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await api.auth.register(email, password, displayName || undefined);
      nav("/");
    } catch (e: any) {
      setErr(e?.message ?? "Register failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen page-surface text-[hsl(var(--foreground))]">
      <div className="max-w-md mx-auto px-4 py-14">
        <div className="card-surface rounded-2xl border border-white/12 shadow-lg p-6">
          <h1 className="text-2xl font-semibold mb-4 text-slate-100">Create account</h1>

          {err && (
            <div className="mb-4 rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-200">
              {err}
            </div>
          )}

          <form onSubmit={onSubmit} className="space-y-3">
            <input
              className="w-full rounded-xl bg-white/[.06] border border-white/12 px-3 py-2
                         text-slate-100 placeholder-slate-400
                         focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400/40"
              placeholder="Name (public)"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              autoComplete="nickname"
            />
            <input
              className="w-full rounded-xl bg-white/[.06] border border-white/12 px-3 py-2
                         text-slate-100 placeholder-slate-400
                         focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400/40"
              placeholder="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
            />
            <input
              type="password"
              className="w-full rounded-xl bg-white/[.06] border border-white/12 px-3 py-2
                         text-slate-100 placeholder-slate-400
                         focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400/40"
              placeholder="Password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
            />

            <button
              disabled={busy}
              className="w-full rounded-xl bg-blue-500 text-white font-semibold py-2.5
                         shadow-[0_8px_20px_rgba(59,130,246,.35)]
                         hover:bg-blue-400 active:bg-blue-500/90
                         focus:outline-none focus:ring-2 focus:ring-blue-400/50
                         disabled:opacity-60"
            >
              {busy ? "Creating..." : "Register"}
            </button>
          </form>

          <div className="mt-4 text-sm text-slate-300">
            Have an account?{" "}
            <Link to="/login" className="text-blue-300 hover:text-blue-200">
              Log in
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
