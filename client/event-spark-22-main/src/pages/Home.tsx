import { EventCard } from "@/components/EventCard";
import { SectionHeader } from "@/components/SectionHeader";
import { EmptyState } from "@/components/EmptyState";
import { Calendar } from "lucide-react";
import { getRecommendedEvents, getTrendingOverall, getTrendingByCategory } from "@/data/mockEvents";

export default function Home() {
  const recommendedEvents = getRecommendedEvents();
  const trendingEvents = getTrendingOverall();
  const techEvents = getTrendingByCategory("Technology");
  const musicEvents = getTrendingByCategory("Music");

  return (
    <div className="min-h-screen bg-background">
      {/* Hero Section */}
      <div className="bg-gradient-primary text-primary-foreground">
        <div className="container mx-auto px-4 py-12">
          <h1 className="text-4xl md:text-5xl font-bold mb-4">
            Discover Events That Matter
          </h1>
          <p className="text-xl opacity-90 max-w-2xl">
            Find the perfect events based on your interests and see what your friends are attending
          </p>
        </div>
      </div>

      {/* Main Content */}
      <div className="container mx-auto px-4 py-8 space-y-12">
        {/* Recommended for You */}
        <section>
          <SectionHeader 
            title="Recommended for You" 
            ctaLabel="See all recommendations"
            onCtaClick={() => console.log("View all recommendations")}
          />
          {recommendedEvents.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {recommendedEvents.map(event => (
                <EventCard key={event.id} {...event} />
              ))}
            </div>
          ) : (
            <EmptyState
              icon={Calendar}
              headline="No recommendations yet"
              helperText="We're still learning about your preferences. Check back soon!"
            />
          )}
        </section>

        {/* Trending Overall */}
        <section>
          <SectionHeader 
            title="Trending Overall" 
            ctaLabel="Explore trending"
            onCtaClick={() => console.log("View all trending")}
          />
          {trendingEvents.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {trendingEvents.map(event => (
                <EventCard key={event.id} {...event} />
              ))}
            </div>
          ) : (
            <EmptyState
              icon={Calendar}
              headline="No trending events"
              helperText="Check back later for popular events in your area"
            />
          )}
        </section>

        {/* Trending in Technology */}
        <section>
          <SectionHeader 
            title="Trending in Technology" 
            ctaLabel="More tech events"
            onCtaClick={() => console.log("View tech events")}
          />
          {techEvents.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {techEvents.map(event => (
                <EventCard key={event.id} {...event} />
              ))}
            </div>
          ) : (
            <EmptyState
              icon={Calendar}
              headline="No tech events available"
              helperText="New technology events are added regularly"
            />
          )}
        </section>

        {/* Trending in Music */}
        <section>
          <SectionHeader 
            title="Trending in Music" 
            ctaLabel="More music events"
            onCtaClick={() => console.log("View music events")}
          />
          {musicEvents.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {musicEvents.map(event => (
                <EventCard key={event.id} {...event} />
              ))}
            </div>
          ) : (
            <EmptyState
              icon={Calendar}
              headline="No music events available"
              helperText="Check back for upcoming concerts and festivals"
            />
          )}
        </section>
      </div>
    </div>
  );
}