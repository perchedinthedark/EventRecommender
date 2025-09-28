import { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface EmptyStateProps {
  icon: LucideIcon;
  headline: string;
  helperText: string;
  className?: string;
}

export function EmptyState({ icon: Icon, headline, helperText, className }: EmptyStateProps) {
  return (
    <div className={cn(
      "flex flex-col items-center justify-center py-12 px-4 text-center",
      className
    )}>
      <div className="rounded-full bg-muted p-4 mb-4">
        <Icon className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold text-foreground mb-2">{headline}</h3>
      <p className="text-sm text-muted-foreground max-w-sm">{helperText}</p>
    </div>
  );
}