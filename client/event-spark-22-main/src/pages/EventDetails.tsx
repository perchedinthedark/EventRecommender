import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { Calendar, MapPin, DollarSign, ArrowLeft, Users } from "lucide-react";
import { format } from "date-fns";
import { getEventById } from "@/data/mockEvents";
import { CategoryChip } from "@/components/CategoryChip";
import { AvatarStack } from "@/components/AvatarStack";
import { RatingStars } from "@/components/RatingStars";
import { StatusButtons, EventStatus } from "@/components/StatusButtons";
import { EmptyState } from "@/components/EmptyState";
import { Button } from "@/components/ui/button";

export default function EventDetails() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [status, setStatus] = useState<EventStatus>("None");
  
  const event = id ? getEventById(id) : null;

  if (!event) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <EmptyState
          icon={Calendar}
          headline="Event not found"
          helperText="The event you're looking for doesn't exist or has been removed"
        />
      </div>
    );
  }

  const date = new Date(event.dateTime);
  const isPastEvent = date < new Date();
  const totalFriends = event.friendsGoing + event.friendsInterested;

  return (
    <div className="min-h-screen bg-background">
      {/* Header Image */}
      <div className="relative h-64 md:h-96 bg-gradient-to-br from-primary/20 to-accent/20">
        {event.imageUrl ? (
          <img 
            src={event.imageUrl} 
            alt={event.title}
            className="w-full h-full object-cover"
          />
        ) : (
          <div className="w-full h-full bg-gradient-primary" />
        )}
        <div className="absolute inset-0 bg-gradient-overlay" />
        
        {/* Back Button */}
        <Button
          onClick={() => navigate("/")}
          variant="ghost"
          size="icon"
          className="absolute top-4 left-4 bg-background/90 backdrop-blur-sm hover:bg-background"
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
      </div>

      {/* Content */}
      <div className="container mx-auto px-4 py-8">
        <div className="max-w-4xl mx-auto">
          {/* Title Section */}
          <div className="mb-6">
            <h1 className="text-3xl md:text-4xl font-bold text-foreground mb-4">
              {event.title}
            </h1>
            
            {/* Chips */}
            <div className="flex flex-wrap gap-2 mb-4">
              <CategoryChip text={event.categoryName} variant="primary" />
              <CategoryChip text={event.organizerName} variant="accent" />
            </div>

            {/* Event Details */}
            <div className="space-y-3 text-foreground">
              <div className="flex items-center gap-2">
                <Calendar className="w-5 h-5 text-muted-foreground" />
                <span className="font-medium">
                  {format(date, "EEEE, MMMM d, yyyy • h:mm a")}
                </span>
              </div>
              
              <div className="flex items-center gap-2">
                <MapPin className="w-5 h-5 text-muted-foreground" />
                <div>
                  <span className="font-medium">{event.venueName}</span>
                  {event.address && (
                    <span className="text-muted-foreground block text-sm mt-1">
                      {event.address}
                    </span>
                  )}
                </div>
              </div>

              {event.price && (
                <div className="flex items-center gap-2">
                  <DollarSign className="w-5 h-5 text-muted-foreground" />
                  <span className="font-medium">{event.price}</span>
                </div>
              )}
            </div>
          </div>

          {/* Social Proof */}
          {totalFriends > 0 && (
            <div className="bg-card rounded-lg p-4 mb-6 shadow-card">
              <div className="flex items-center gap-4">
                <AvatarStack count={totalFriends} size="md" />
                <div>
                  <div className="flex items-center gap-1 text-sm text-muted-foreground">
                    <Users className="w-4 h-4" />
                    <span>{totalFriends} friends are interested</span>
                  </div>
                  <div className="text-sm mt-1">
                    {event.friendsGoing > 0 && (
                      <span className="text-success font-medium">
                        {event.friendsGoing} going
                      </span>
                    )}
                    {event.friendsGoing > 0 && event.friendsInterested > 0 && (
                      <span className="text-muted-foreground"> • </span>
                    )}
                    {event.friendsInterested > 0 && (
                      <span className="text-primary font-medium">
                        {event.friendsInterested} interested
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Action Buttons */}
          {!isPastEvent && (
            <div className="mb-8">
              <StatusButtons 
                currentStatus={status}
                onStatusChange={setStatus}
              />
            </div>
          )}

          {/* Rating (for past events) */}
          {isPastEvent && event.rating && (
            <div className="bg-accent-light rounded-lg p-4 mb-6">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-foreground">
                  Event Rating
                </span>
                <RatingStars rating={event.rating} size="md" />
              </div>
            </div>
          )}

          {/* Description */}
          {event.description && (
            <div className="prose prose-lg max-w-none">
              <h2 className="text-xl font-semibold text-foreground mb-3">
                About this event
              </h2>
              <p className="text-muted-foreground leading-relaxed">
                {event.description}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}