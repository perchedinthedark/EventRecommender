import { Calendar, MapPin, Users, Star } from "lucide-react";
import { Link } from "react-router-dom";
import { AvatarStack } from "./AvatarStack";
import { CategoryChip } from "./CategoryChip";
import { format } from "date-fns";

interface EventCardProps {
  id: string;
  title: string;
  dateTime: string;
  venueName: string;
  categoryName: string;
  organizerName: string;
  friendsGoing: number;
  friendsInterested: number;
  rating?: number;
  imageUrl?: string;
}

export function EventCard({
  id,
  title,
  dateTime,
  venueName,
  categoryName,
  organizerName,
  friendsGoing,
  friendsInterested,
  rating,
  imageUrl
}: EventCardProps) {
  const totalFriends = friendsGoing + friendsInterested;
  const date = new Date(dateTime);
  const isPastEvent = date < new Date();

  return (
    <Link 
      to={`/event/${id}`}
      className="group block bg-card rounded-lg overflow-hidden shadow-card hover:shadow-card-hover transition-all duration-base transform hover:scale-[1.02]"
    >
      {/* Image Container */}
      <div className="relative h-48 bg-gradient-to-br from-primary/10 to-accent/10 overflow-hidden">
        {imageUrl ? (
          <img 
            src={imageUrl} 
            alt={title}
            className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-slow"
          />
        ) : (
          <div className="w-full h-full bg-gradient-primary opacity-80" />
        )}
        <div className="absolute inset-0 bg-gradient-overlay opacity-0 group-hover:opacity-100 transition-opacity duration-base" />
        
        {/* Category Badge */}
        <div className="absolute top-3 left-3">
          <CategoryChip text={categoryName} variant="overlay" />
        </div>
        
        {/* Rating Badge (if past event) */}
        {isPastEvent && rating && (
          <div className="absolute top-3 right-3 bg-background/90 backdrop-blur-sm px-2 py-1 rounded-md flex items-center gap-1">
            <Star className="w-4 h-4 fill-accent text-accent" />
            <span className="text-sm font-medium text-foreground">{rating.toFixed(1)}</span>
          </div>
        )}
      </div>

      {/* Content */}
      <div className="p-4 space-y-3">
        {/* Title */}
        <h3 className="font-semibold text-lg text-foreground line-clamp-2 group-hover:text-primary transition-colors duration-fast">
          {title}
        </h3>

        {/* Date & Time */}
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Calendar className="w-4 h-4" />
          <span>{format(date, "MMM d, yyyy • h:mm a")}</span>
        </div>

        {/* Venue */}
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <MapPin className="w-4 h-4" />
          <span className="line-clamp-1">{venueName}</span>
        </div>

        {/* Organizer */}
        <div className="text-sm text-muted-foreground">
          <span>by {organizerName}</span>
        </div>

        {/* Social Proof */}
        {totalFriends > 0 && (
          <div className="flex items-center gap-3 pt-2 border-t border-border">
            <AvatarStack count={totalFriends} />
            <div className="text-sm text-muted-foreground">
              {friendsGoing > 0 && (
                <span className="text-success font-medium">{friendsGoing} going</span>
              )}
              {friendsGoing > 0 && friendsInterested > 0 && <span> • </span>}
              {friendsInterested > 0 && (
                <span className="text-primary font-medium">{friendsInterested} interested</span>
              )}
            </div>
          </div>
        )}
      </div>
    </Link>
  );
}