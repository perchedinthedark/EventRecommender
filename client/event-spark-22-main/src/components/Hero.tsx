// src/components/Hero.tsx
import { cn } from "@/lib/utils";

export default function Hero({ className }: { className?: string }) {
  return (
    <section
      className={cn(
        "relative overflow-hidden rounded-3xl border border-white/10 bg-white/5 backdrop-blur-md",
        "shadow-[0_24px_80px_-30px_rgba(0,0,0,.65)]",
        className
      )}
    >
      <img
        className="absolute inset-0 h-full w-full object-cover"
        src="https://images.unsplash.com/photo-1506157786151-b8491531f063?q=80&w=1600&auto=format&fit=crop"
        alt="Crowd at an event"
        loading="lazy"
      />
      <div className="absolute inset-0 bg-gradient-to-b from-black/30 via-black/40 to-black/70" />
      <div className="relative px-6 py-14 md:px-10 md:py-16">
        <h1 className="text-3xl md:text-5xl font-bold tracking-tight text-white">
          Eventualno
        </h1>
        <p className="mt-3 max-w-2xl text-white/85 text-sm md:text-base">
          Smart picks for your next week out — discover concerts, meetups, and happenings
          you’ll actually enjoy.
        </p>
      </div>
    </section>
  );
}
