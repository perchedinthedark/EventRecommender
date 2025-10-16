import { cn } from "@/lib/utils";

export function SectionHeader({
  title,
  ctaLabel,
  onCtaClick,
  className,
}: {
  title: string;
  ctaLabel?: string;
  onCtaClick?: () => void;
  className?: string;
}) {
  return (
    <div className={cn("flex items-center justify-between mb-4", className)}>
      <h2 className="text-[22px] font-semibold text-slate-900 dark:text-white">
        {title}
      </h2>

      {ctaLabel && (
        <button
          onClick={onCtaClick}
          className={cn(
            "group inline-flex items-center gap-2 text-[14px] font-semibold",
            // brand gradient like the header text
            "bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300 bg-clip-text text-transparent"
          )}
        >
          <span>{ctaLabel}</span>
          {/* use a text chevron so the gradient applies cleanly */}
          <span
            className={cn(
              "transition-transform group-hover:translate-x-0.5",
              "bg-gradient-to-r from-indigo-300 via-sky-300 to-emerald-300 bg-clip-text text-transparent"
            )}
          >
            »
          </span>
        </button>
      )}
    </div>
  );
}
