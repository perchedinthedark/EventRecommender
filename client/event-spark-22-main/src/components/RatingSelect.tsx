import { useState } from "react";
import { Star } from "lucide-react";
import { cn } from "@/lib/utils";

export default function RatingSelect({
  value,
  onChange,
  size = "md",
  disabled,
  className,
}: {
  value?: number | null;
  onChange: (v: number) => void;
  size?: "sm" | "md" | "lg";
  disabled?: boolean;
  className?: string;
}) {
  const [hover, setHover] = useState<number | null>(null);

  const sizeClasses = {
    sm: "w-4 h-4",
    md: "w-5 h-5",
    lg: "w-6 h-6",
  };

  const current = hover ?? (value ?? 0);

  return (
    <div className={cn("flex items-center gap-1", className)}>
      {Array.from({ length: 5 }).map((_, i) => {
        const idx = i + 1;
        const active = current >= idx;
        return (
          <button
            key={idx}
            type="button"
            className="p-0.5"
            disabled={disabled}
            onMouseEnter={() => !disabled && setHover(idx)}
            onMouseLeave={() => !disabled && setHover(null)}
            onClick={() => !disabled && onChange(idx)}
            aria-label={`Rate ${idx} star${idx > 1 ? "s" : ""}`}
            title={`${idx} / 5`}
          >
            <Star
              className={cn(
                sizeClasses[size],
                active ? "fill-accent text-accent" : "text-slate-300"
              )}
            />
          </button>
        );
      })}
      {typeof value === "number" && (
        <span className="ml-2 text-sm text-slate-600">{value.toFixed(1)}</span>
      )}
    </div>
  );
}
