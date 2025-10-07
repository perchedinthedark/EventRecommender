// client/src/components/StatusButtons.tsx
import { Check, Heart } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

export type EventStatus = "None" | "Interested" | "Going";

interface StatusButtonsProps {
  currentStatus: EventStatus;
  onStatusChange: (status: EventStatus) => void;
  className?: string;
}

export function StatusButtons({ currentStatus, onStatusChange, className }: StatusButtonsProps) {
  const isInterested = currentStatus === "Interested";
  const isGoing = currentStatus === "Going";

  // click again to turn it off
  const toggleTo = (target: EventStatus) =>
    onStatusChange(currentStatus === target ? "None" : target);

  return (
    <div className={cn("flex gap-3", className)}>
      {/* Interested */}
      <Button
        aria-pressed={isInterested}
        size="lg"
        onClick={() => toggleTo("Interested")}
        className={cn(
          "flex items-center gap-2 rounded-xl px-5 py-2 text-sm font-medium transition-all",
          "backdrop-blur-sm border",
          isInterested
            ? "bg-primary text-primary-foreground border-transparent shadow-[0_0_10px_rgba(37,99,235,.45)]"
            : "bg-white/10 border-white/20 text-slate-200 hover:bg-white/15"
        )}
      >
        <Heart className={cn("w-4 h-4", isInterested && "fill-current")} />
        Interested
      </Button>

      {/* Going */}
      <Button
        aria-pressed={isGoing}
        size="lg"
        onClick={() => toggleTo("Going")}
        className={cn(
          "flex items-center gap-2 rounded-xl px-5 py-2 text-sm font-medium transition-all",
          "backdrop-blur-sm border",
          isGoing
            ? "bg-emerald-500 text-white border-transparent shadow-[0_0_10px_rgba(16,185,129,.5)]"
            : "bg-white/10 border-white/20 text-slate-200 hover:bg-white/15"
        )}
      >
        <Check className="w-4 h-4" />
        {isGoing ? "You're Going!" : "Going"}
      </Button>
    </div>
  );
}
