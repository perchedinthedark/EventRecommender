import { ArrowRight } from "lucide-react";
import { cn } from "@/lib/utils";

interface SectionHeaderProps {
  title: string;
  ctaLabel?: string;
  onCtaClick?: () => void;
  className?: string;
}

export function SectionHeader({ title, ctaLabel, onCtaClick, className }: SectionHeaderProps) {
  return (
    <div className={cn("flex items-center justify-between mb-6", className)}>
      <h2 className="text-2xl font-bold text-foreground">{title}</h2>
      {ctaLabel && (
        <button
          onClick={onCtaClick}
          className="group flex items-center gap-2 text-sm font-medium text-primary hover:text-primary-hover transition-colors duration-fast"
        >
          {ctaLabel}
          <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform duration-fast" />
        </button>
      )}
    </div>
  );
}