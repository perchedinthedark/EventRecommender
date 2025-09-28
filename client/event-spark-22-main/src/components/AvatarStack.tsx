import { cn } from "@/lib/utils";

interface AvatarStackProps {
  count: number;
  size?: "sm" | "md" | "lg";
  className?: string;
}

export function AvatarStack({ count, size = "sm", className }: AvatarStackProps) {
  const displayCount = Math.min(count, 5);
  const remainingCount = count - displayCount;
  
  const sizeClasses = {
    sm: "w-8 h-8 text-xs",
    md: "w-10 h-10 text-sm",
    lg: "w-12 h-12 text-base"
  };

  const avatarColors = [
    "bg-avatar-1",
    "bg-avatar-2",
    "bg-avatar-3",
    "bg-avatar-4",
    "bg-avatar-5"
  ];

  return (
    <div className={cn("flex -space-x-2", className)}>
      {Array.from({ length: displayCount }).map((_, i) => (
        <div
          key={i}
          className={cn(
            "rounded-full border-2 border-background flex items-center justify-center font-medium text-white relative z-10",
            sizeClasses[size],
            avatarColors[i % avatarColors.length]
          )}
          style={{ zIndex: displayCount - i }}
        >
          {String.fromCharCode(65 + i)}
        </div>
      ))}
      {remainingCount > 0 && (
        <div
          className={cn(
            "rounded-full border-2 border-background bg-muted flex items-center justify-center font-medium text-muted-foreground",
            sizeClasses[size]
          )}
        >
          +{remainingCount}
        </div>
      )}
    </div>
  );
}