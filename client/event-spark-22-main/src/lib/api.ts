const BASE = (import.meta.env.VITE_API_BASE || "").replace(/\/$/, "");

export type StatusType = "None" | "Interested" | "Going";

export type EventDto = {
  id: number;
  title: string;
  description?: string | null;
  dateTime: string;
  location?: string | null;
  category?: string;
  venue?: string;
  organizer?: string;
  friendsGoing?: number;
};

export type TrendingCategoryBlock = {
  categoryId: number;
  categoryName: string;
  events: EventDto[];
};
export type TrendingResponse = { overall: EventDto[]; byCategory: TrendingCategoryBlock[]; };
export type MyInteraction = { status: StatusType; rating?: number | null; };

function mapEvent(e: any): EventDto {
  return {
    id: e.Id ?? e.id,
    title: e.Title ?? e.title,
    description: e.Description ?? e.description ?? null,
    dateTime: (e.DateTime ?? e.dateTime)?.toString(),
    location: e.Location ?? e.location ?? null,
    category: e.Category ?? e.category ?? "",
    venue: e.Venue ?? e.venue ?? "",
    organizer: e.Organizer ?? e.organizer ?? "",
    friendsGoing: e.FriendsGoing ?? e.friendsGoing,
  };
}
function mapTrending(res: any): TrendingResponse {
  const overallRaw = res.overall ?? res.Overall ?? [];
  const byCatRaw = res.byCategory ?? res.ByCategory ?? [];
  return {
    overall: overallRaw.map(mapEvent),
    byCategory: byCatRaw.map((b: any) => ({
      categoryId: b.CategoryId ?? b.categoryId,
      categoryName: b.CategoryName ?? b.categoryName ?? "",
      events: (b.Events ?? b.events ?? []).map(mapEvent),
    })),
  };
}
async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return (await res.json()) as T;
}

export const api = {
  async getRecs(topN = 6): Promise<EventDto[]> {
    try { const raw = await http<any[]>(`${BASE}/api/recs?topN=${topN}`); return (raw ?? []).map(mapEvent); }
    catch { return []; }
  },
  async getTrending(perList = 6, categoriesToShow = 2): Promise<TrendingResponse> {
    const raw = await http<any>(`${BASE}/api/trending?perList=${perList}&categoriesToShow=${categoriesToShow}`);
    return mapTrending(raw);
  },
  async getMyInteraction(eventId: number): Promise<MyInteraction> {
    return await http<MyInteraction>(`${BASE}/api/events/${eventId}/me`);
  },
  async setStatus(eventId: number, status: StatusType): Promise<MyInteraction> {
    return await http<MyInteraction>(`${BASE}/api/events/${eventId}/status`, {
      method: "POST", body: JSON.stringify({ status }),
    });
  },
  async setRating(eventId: number, rating: number): Promise<MyInteraction> {
    return await http<MyInteraction>(`${BASE}/api/events/${eventId}/rating`, {
      method: "POST", body: JSON.stringify({ rating }),
    });
  },
  async getEvent(eventId: number): Promise<EventDto> {
    const raw = await http<any>(`${BASE}/api/events/${eventId}`);
    return mapEvent(raw);
  },

  telemetry: {
    async click(eventId: number) {
      return await http(`${BASE}/api/telemetry/clicks`, { method: "POST", body: JSON.stringify({ eventId }) });
    },
    async dwell(eventId: number, dwellMs: number) {
      return await http(`${BASE}/api/telemetry/dwell`, { method: "POST", body: JSON.stringify({ eventId, dwellMs }) });
    },
  },

  auth: {
    async register(email: string, password: string) {
      return await http(`${BASE}/api/auth/register`, { method: "POST", body: JSON.stringify({ email, password }) });
    },
    async login(email: string, password: string) {
      return await http(`${BASE}/api/auth/login`, { method: "POST", body: JSON.stringify({ email, password }) });
    },
    async logout() {
      return await http(`${BASE}/api/auth/logout`, { method: "POST", body: "{}" });
    },
    async me(): Promise<{ id: string; userName: string; email: string }> {
      return await http(`${BASE}/api/auth/me`);
    },
  },

  social: {
    async follow(followeeId: string) {
      return await http(`${BASE}/api/social/follow`, { method: "POST", body: JSON.stringify({ followeeId }) });
    },
    async unfollow(followeeId: string) {
      return await http(`${BASE}/api/social/unfollow`, { method: "POST", body: JSON.stringify({ followeeId }) });
    },
    async following(): Promise<{ id: string; userName: string; email: string }[]> {
      return await http(`${BASE}/api/social/following`);
    },
    async friendsGoing(eventId: number): Promise<{ count: number }> {
      return await http(`${BASE}/api/social/friends-going?eventId=${eventId}`);
    },
  },
};
