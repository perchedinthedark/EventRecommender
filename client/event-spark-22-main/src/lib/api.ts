// API functions will go here
// src/lib/api.ts

export type InteractionStatus = "Interested" | "Going";

export interface EventDto {
  id: number;
  title: string;
  description?: string;
  dateTime: string;           // ISO string
  location?: string;
  category?: string;          // category name
  venue?: string;             // venue name
  organizer?: string;         // organizer name
  friendsGoing?: number;      // optional social signal
}

export interface TrendingCategoryBlock {
  categoryId: number;
  categoryName: string;
  events: EventDto[];
}

export interface TrendingResponse {
  overall: EventDto[];
  byCategory: TrendingCategoryBlock[];
}

// Configure your backend base URL.
// Example for local dev: VITE_API_BASE="http://localhost:5210"
const API_BASE = (import.meta as any).env?.VITE_API_BASE ?? "";

// Tiny helper for JSON requests
async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(API_BASE + path, {
    credentials: "include", // send cookies (Identity auth)
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers || {}),
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  // Some endpoints (like POSTs) may return empty; handle gracefully.
  if (res.status === 204) return undefined as unknown as T;
  const ct = res.headers.get("content-type") || "";
  return ct.includes("application/json") ? res.json() : (undefined as unknown as T);
}

export const api = {
  // Personalized recommendations for the current logged-in user
  getRecs: (topN = 6) =>
    http<EventDto[]>(`/api/recs?topN=${encodeURIComponent(topN)}`),

  // Trending overall + category-aware (great for cold-start)
  getTrending: (perList = 6, categoriesToShow = 2) =>
    http<TrendingResponse>(
      `/api/trending?perList=${encodeURIComponent(perList)}&categoriesToShow=${encodeURIComponent(categoriesToShow)}`
    ),

  // (Optional) Event details if you want a client-side details page later
  getEvent: (id: number) => http<EventDto>(`/api/events/${id}`),

  // Log an interaction (Interested / Going) and optional rating
  postInteraction: (id: number, status: InteractionStatus, rating?: number) =>
    http<void>(`/api/events/${id}/interact`, {
      method: "POST",
      body: JSON.stringify({ status, rating }),
    }),

  // Lightweight click/dwell logging via Beacon (non-blocking)
  beaconClick: (id: number, dwellMs?: number) => {
    try {
      const body = new Blob([JSON.stringify({ dwellMs })], {
        type: "application/json",
      });
      return navigator.sendBeacon(API_BASE + `/api/events/${id}/click`, body);
    } catch {
      // Fallback
      return fetch(API_BASE + `/api/events/${id}/click`, {
        method: "POST",
        body: JSON.stringify({ dwellMs }),
        headers: { "Content-Type": "application/json" },
        keepalive: true,
        credentials: "include",
      }).then(() => true, () => false);
    }
  },
};
