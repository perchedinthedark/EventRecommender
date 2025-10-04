import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";

export default function LoginPage() {
  const nav = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await api.auth.login(email, password);
      nav("/");
    } catch (e: any) {
      setErr(e?.message ?? "Login failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen bg-[hsl(var(--background))]">
      <div className="max-w-md mx-auto px-4 py-12">
        <div className="bg-white border border-slate-200 rounded-2xl shadow-lg p-6">
          <h1 className="text-2xl font-semibold mb-4 text-slate-900">Log in</h1>

          {err && (
            <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {err}
            </div>
          )}

          <form onSubmit={onSubmit} className="space-y-3">
            <input
              className="w-full bg-slate-50 text-slate-900 placeholder-slate-500 border border-slate-300 rounded-lg p-2 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400"
              placeholder="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
            />
            <input
              type="password"
              className="w-full bg-slate-50 text-slate-900 placeholder-slate-500 border border-slate-300 rounded-lg p-2 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-400"
              placeholder="Password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
            <button
              disabled={busy}
              className="w-full bg-blue-600 hover:bg-blue-700 disabled:opacity-70 text-white rounded-lg py-2 font-medium transition-colors"
            >
              {busy ? "Signing in..." : "Sign in"}
            </button>
          </form>

          <div className="mt-4 text-sm text-slate-600">
            No account?{" "}
            <Link to="/register" className="text-blue-600 hover:underline">
              Register
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
