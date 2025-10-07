import Header from "@/components/Header";
import { Outlet } from "react-router-dom";

export default function AppShell() {
  return (
    <div className="min-h-screen bg-[radial-gradient(1200px_600px_at_50%_-200px,rgba(59,130,246,.15),transparent)] bg-slate-950 text-slate-100">
      <Header />
      <Outlet />
    </div>
  );
}
