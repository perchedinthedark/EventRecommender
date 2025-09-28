// src/components/EmptyState.tsx
import type { LucideIcon } from "lucide-react";
import { Info } from "lucide-react";
import { cn } from "@/lib/utils";

interface EmptyStateProps {
  icon?: LucideIcon;        // now optional
  headline?: string;        // original name
  helperText?: string;
  /** aliases so <EmptyState title="…"/> also works */
  title?: string;
  text?: string;
  className?: string;
}

export default function EmptyState({
  icon: Icon = Info,
  headline,
  helperText,
  title,
  text,
  className,
}: EmptyStateProps) {
  const h = headline ?? title ?? "Nothing here yet.";
  const t = helperText ?? text ?? "";

  return (
    <div className={cn("flex flex-col items-center justify-center py-12 px-4 text-center", className)}>
      <div className="rounded-full bg-muted p-4 mb-4">
        <Icon className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold text-foreground mb-2">{h}</h3>
      {!!t && <p className="text-sm text-muted-foreground max-w-sm">{t}</p>}
    </div>
  );
}

