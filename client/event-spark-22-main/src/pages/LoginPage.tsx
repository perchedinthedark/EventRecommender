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
    setBusy(true); setErr(null);
    try {
      await api.auth.login(email, password);
      nav("/");
    } catch (e: any) {
      setErr(e?.message ?? "Login failed");
    } finally { setBusy(false); }
  }

  return (
    <div className="max-w-md mx-auto px-4 py-10">
      <h1 className="text-2xl font-semibold mb-4">Log in</h1>
      {err && <div className="text-red-600 mb-3">{err}</div>}
      <form onSubmit={onSubmit} className="space-y-3">
        <input className="w-full border border-slate-300 rounded-lg p-2" placeholder="Email" value={email} onChange={e=>setEmail(e.target.value)} />
        <input type="password" className="w-full border border-slate-300 rounded-lg p-2" placeholder="Password" value={password} onChange={e=>setPassword(e.target.value)} />
        <button disabled={busy} className="w-full bg-blue-600 hover:bg-blue-700 text-white rounded-lg py-2 font-medium">
          {busy ? "Signing in..." : "Sign in"}
        </button>
      </form>
      <div className="mt-3 text-sm">
        No account? <Link to="/register" className="text-blue-600 hover:underline">Register</Link>
      </div>
    </div>
  );
}
