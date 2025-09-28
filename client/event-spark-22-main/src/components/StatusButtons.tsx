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
  return (
    <div className={cn("flex gap-3", className)}>
      <Button
        variant={currentStatus === "Interested" ? "default" : "outline"}
        size="lg"
        onClick={() => onStatusChange(currentStatus === "Interested" ? "None" : "Interested")}
        className={cn(
          "flex items-center gap-2",
          currentStatus === "Interested" 
            ? "bg-primary hover:bg-primary-hover text-primary-foreground" 
            : "border-primary text-primary hover:bg-primary-light"
        )}
      >
        <Heart className={cn(
          "w-4 h-4",
          currentStatus === "Interested" && "fill-current"
        )} />
        Interested
      </Button>
      
      <Button
        variant={currentStatus === "Going" ? "default" : "secondary"}
        size="lg"
        onClick={() => onStatusChange(currentStatus === "Going" ? "None" : "Going")}
        className={cn(
          "flex items-center gap-2",
          currentStatus === "Going" 
            ? "bg-success hover:bg-success/90 text-success-foreground" 
            : "bg-secondary hover:bg-secondary-hover text-secondary-foreground"
        )}
        disabled={currentStatus === "Going"}
      >
        <Check className="w-4 h-4" />
        {currentStatus === "Going" ? "You're Going!" : "Going"}
      </Button>
    </div>
  );
}