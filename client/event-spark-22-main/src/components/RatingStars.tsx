import { Star } from "lucide-react";
import { cn } from "@/lib/utils";

interface RatingStarsProps {
  rating?: number;
  size?: "sm" | "md" | "lg";
  className?: string;
}

export function RatingStars({ rating, size = "md", className }: RatingStarsProps) {
  if (!rating) return null;

  const sizeClasses = {
    sm: "w-4 h-4",
    md: "w-5 h-5",
    lg: "w-6 h-6"
  };

  const fullStars = Math.floor(rating);
  const hasHalfStar = rating % 1 >= 0.5;
  const emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

  return (
    <div className={cn("flex items-center gap-0.5", className)}>
      {/* Full Stars */}
      {Array.from({ length: fullStars }).map((_, i) => (
        <Star
          key={`full-${i}`}
          className={cn(sizeClasses[size], "fill-accent text-accent")}
        />
      ))}
      
      {/* Half Star */}
      {hasHalfStar && (
        <div className="relative">
          <Star className={cn(sizeClasses[size], "text-muted")} />
          <div className="absolute inset-0 overflow-hidden w-1/2">
            <Star className={cn(sizeClasses[size], "fill-accent text-accent")} />
          </div>
        </div>
      )}
      
      {/* Empty Stars */}
      {Array.from({ length: emptyStars }).map((_, i) => (
        <Star
          key={`empty-${i}`}
          className={cn(sizeClasses[size], "text-muted")}
        />
      ))}
      
      <span className="ml-2 text-sm font-medium text-muted-foreground">
        {rating.toFixed(1)}
      </span>
    </div>
  );
}