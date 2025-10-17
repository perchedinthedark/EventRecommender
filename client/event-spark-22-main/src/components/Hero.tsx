import { cn } from "@/lib/utils";

type HeroProps = {
  /** Big heading */
  title: string;
  /** One-liner under the heading */
  subtitle?: string;
  /** Background image */
  imageUrl?: string;
  /** Remove rounded corners when true (full rectangle) */
  rectangular?: boolean;
  /** Optional extra classes for spacing/placement */
  className?: string;
};

export default function Hero({
  title,
  subtitle,
  imageUrl = "https://images.unsplash.com/photo-1506157786151-b8491531f063?q=80&w=1600&auto=format&fit=crop",
  rectangular = false,
  className,
}: HeroProps) {
  return (
    <section
      className={cn(
        "relative overflow-hidden border border-white/10 bg-white/5 backdrop-blur-md",
        "shadow-[0_24px_80px_-30px_rgba(0,0,0,.65)]",
        rectangular ? "rounded-none" : "rounded-3xl",
        className
      )}
    >
      <img
        className="absolute inset-0 h-full w-full object-cover"
        src={imageUrl}
        alt=""
        loading="lazy"
      />
      <div className="absolute inset-0 bg-gradient-to-b from-black/30 via-black/40 to-black/70" />
      <div className="relative px-6 py-12 md:px-10 md:py-14">
        <h1 className="text-3xl md:text-5xl font-bold tracking-tight text-white">
          {title}
        </h1>
        {subtitle && (
          <p className="mt-3 max-w-2xl text-white/85 text-sm md:text-base">
            {subtitle}
          </p>
        )}
      </div>
    </section>
  );
}
