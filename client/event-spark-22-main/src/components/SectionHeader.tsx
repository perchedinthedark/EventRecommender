import { ArrowRight } from "lucide-react";
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
      <h2 className="text-[22px] font-semibold text-slate-900">{title}</h2>
      {ctaLabel && (
        <button
          onClick={onCtaClick}
          className="group inline-flex items-center gap-2 text-[14px] font-medium text-blue-600 hover:text-blue-700"
        >
          {ctaLabel}
          <ArrowRight className="w-4 h-4 transition-transform group-hover:translate-x-0.5" />
        </button>
      )}
    </div>
  );
}
