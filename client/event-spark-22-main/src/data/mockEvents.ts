export interface Event {
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
  description?: string;
  address?: string;
  price?: string;
}

// Generate future dates
const getFutureDate = (daysFromNow: number, hour: number = 19) => {
  const date = new Date();
  date.setDate(date.getDate() + daysFromNow);
  date.setHours(hour, 0, 0, 0);
  return date.toISOString();
};

// Generate past dates for events with ratings
const getPastDate = (daysAgo: number, hour: number = 19) => {
  const date = new Date();
  date.setDate(date.getDate() - daysAgo);
  date.setHours(hour, 0, 0, 0);
  return date.toISOString();
};

export const mockEvents: Event[] = [
  // Recommended Events
  {
    id: "1",
    title: "React & TypeScript Workshop: Advanced Patterns",
    dateTime: getFutureDate(3, 18),
    venueName: "Tech Hub Downtown",
    categoryName: "Technology",
    organizerName: "Dev Community SF",
    friendsGoing: 5,
    friendsInterested: 12,
    description: "Join us for an intensive workshop on advanced React and TypeScript patterns. We'll cover performance optimization, custom hooks, and type-safe component patterns that will level up your development skills.",
    address: "123 Market St, San Francisco, CA 94105",
    price: "$45"
  },
  {
    id: "2",
    title: "Summer Music Festival 2024",
    dateTime: getFutureDate(10, 14),
    venueName: "Golden Gate Park",
    categoryName: "Music",
    organizerName: "SF Music Events",
    friendsGoing: 23,
    friendsInterested: 45,
    description: "Experience the best of live music with 20+ bands across 3 stages. Food trucks, art installations, and good vibes all day long!",
    address: "Golden Gate Park, San Francisco, CA 94122",
    price: "$120"
  },
  {
    id: "3",
    title: "Startup Networking Night",
    dateTime: getFutureDate(5, 19),
    venueName: "Innovation Lab",
    categoryName: "Business",
    organizerName: "Startup SF",
    friendsGoing: 8,
    friendsInterested: 19,
    description: "Connect with fellow entrepreneurs, investors, and innovators in the Bay Area startup ecosystem. Pitch your ideas and make meaningful connections.",
    address: "456 Mission St, San Francisco, CA 94105",
    price: "Free"
  },

  // Trending Overall
  {
    id: "4",
    title: "AI & Machine Learning Conference",
    dateTime: getFutureDate(15, 9),
    venueName: "Moscone Center",
    categoryName: "Technology",
    organizerName: "AI Summit",
    friendsGoing: 34,
    friendsInterested: 89,
    description: "The premier AI conference featuring keynotes from industry leaders, hands-on workshops, and the latest in ML research.",
    address: "747 Howard St, San Francisco, CA 94103",
    price: "$299"
  },
  {
    id: "5",
    title: "Food & Wine Festival",
    dateTime: getFutureDate(7, 12),
    venueName: "Ferry Building",
    categoryName: "Food & Drink",
    organizerName: "SF Culinary Events",
    friendsGoing: 15,
    friendsInterested: 32,
    description: "Taste wines from 50+ local wineries paired with gourmet food from the Bay Area's top chefs.",
    address: "1 Ferry Building, San Francisco, CA 94111",
    price: "$85"
  },
  {
    id: "6",
    title: "Stand-up Comedy Night",
    dateTime: getFutureDate(2, 20),
    venueName: "The Punchline",
    categoryName: "Entertainment",
    organizerName: "Comedy Central Live",
    friendsGoing: 7,
    friendsInterested: 14,
    description: "Laugh the night away with rising comedy stars and surprise special guests!",
    address: "444 Battery St, San Francisco, CA 94111",
    price: "$25"
  },

  // Past Events with Ratings (Trending in Technology)
  {
    id: "7",
    title: "Web3 & Blockchain Summit",
    dateTime: getPastDate(5, 10),
    venueName: "Convention Center",
    categoryName: "Technology",
    organizerName: "Crypto SF",
    friendsGoing: 45,
    friendsInterested: 0,
    rating: 4.7,
    description: "Explore the future of decentralized technology with industry pioneers.",
    address: "747 Howard St, San Francisco, CA 94103",
    price: "$150"
  },
  {
    id: "8",
    title: "UX/UI Design Workshop",
    dateTime: getPastDate(10, 18),
    venueName: "Design Studio SF",
    categoryName: "Technology",
    organizerName: "Design Collective",
    friendsGoing: 12,
    friendsInterested: 0,
    rating: 4.9,
    description: "Master the principles of user-centered design with hands-on projects.",
    address: "789 Valencia St, San Francisco, CA 94110",
    price: "$75"
  },
  {
    id: "9",
    title: "Cloud Architecture Bootcamp",
    dateTime: getFutureDate(20, 9),
    venueName: "Tech Campus",
    categoryName: "Technology",
    organizerName: "Cloud Academy",
    friendsGoing: 9,
    friendsInterested: 21,
    description: "Deep dive into AWS, Azure, and GCP architecture patterns.",
    address: "101 California St, San Francisco, CA 94111",
    price: "$199"
  },

  // Trending in Music
  {
    id: "10",
    title: "Jazz Night at the Blue Note",
    dateTime: getFutureDate(4, 21),
    venueName: "Blue Note Jazz Club",
    categoryName: "Music",
    organizerName: "Jazz Society SF",
    friendsGoing: 6,
    friendsInterested: 11,
    description: "An intimate evening of smooth jazz featuring local and touring artists.",
    address: "2030 Union St, San Francisco, CA 94123",
    price: "$35"
  },
  {
    id: "11",
    title: "Electronic Music Festival",
    dateTime: getFutureDate(12, 16),
    venueName: "Pier 70",
    categoryName: "Music",
    organizerName: "EDM Events Bay Area",
    friendsGoing: 28,
    friendsInterested: 67,
    description: "Dance until dawn with world-class DJs and immersive light shows.",
    address: "Pier 70, San Francisco, CA 94107",
    price: "$95"
  },
  {
    id: "12",
    title: "Classical Symphony Night",
    dateTime: getPastDate(3, 19),
    venueName: "Davies Symphony Hall",
    categoryName: "Music",
    organizerName: "SF Symphony",
    friendsGoing: 18,
    friendsInterested: 0,
    rating: 4.8,
    description: "Experience Beethoven's 9th Symphony performed by the SF Symphony Orchestra.",
    address: "201 Van Ness Ave, San Francisco, CA 94102",
    price: "$65"
  }
];

// Helper functions to get events by category
export const getRecommendedEvents = () => mockEvents.slice(0, 3);
export const getTrendingOverall = () => mockEvents.slice(3, 6);
export const getTrendingByCategory = (category: string) => 
  mockEvents.filter(e => e.categoryName === category).slice(0, 3);
export const getEventById = (id: string) => 
  mockEvents.find(e => e.id === id);