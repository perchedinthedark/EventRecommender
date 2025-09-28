import { cn } from "@/lib/utils";

interface CategoryChipProps {
  text: string;
  variant?: "default" | "primary" | "accent" | "overlay";
  className?: string;
}

export function CategoryChip({ text, variant = "default", className }: CategoryChipProps) {
  const variants = {
    default: "bg-secondary text-secondary-foreground hover:bg-secondary-hover",
    primary: "bg-primary text-primary-foreground hover:bg-primary-hover",
    accent: "bg-accent text-accent-foreground hover:bg-accent-hover",
    overlay: "bg-background/90 text-foreground backdrop-blur-sm"
  };

  return (
    <span
      className={cn(
        "inline-flex items-center px-3 py-1 rounded-full text-xs font-medium transition-colors duration-fast",
        variants[variant],
        className
      )}
    >
      {text}
    </span>
  );
}