// File: ML/Schemas.cs
namespace EventRecommender.ML
{
    // ---------- Stage A (MF) ----------
    // Use raw IDs (UserId string, EventId string) and let the pipeline map them to keys.
    public sealed class MfRow
    {
        public string UserId { get; set; } = default!;
        public string EventId { get; set; } = default!;
        public float Label { get; set; }
    }

    public sealed class MfScore
    {
        public float Score { get; set; }
    }

    // ---------- Stage B (Ranking) ----------
    // GroupId is the user id (string); pipeline will MapValueToKey -> GroupKey for FastTreeRanking.
    public sealed class RankRow
    {
        public string GroupId { get; set; } = default!; // user id (string)
        public float Label { get; set; }                // 1 for positive, 0 for sampled negative

        // Existing features
        public float EventRecency { get; set; }   // exp(-daysAgo / 30)
        public float OrganizerScore { get; set; } // organizer popularity proxy
        public float VenueCapacity { get; set; }
        public float CategoryId { get; set; }     // numeric
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }

        // NEW: social +
        public float FriendsGoing { get; set; }
        public float FriendsInterested { get; set; }
        public float FriendsAvgRating { get; set; }

        // NEW: user affinity to event category
        public float UserCatAffinity { get; set; }

        // NEW: recent engagement (30 days)
        public float EventClicks30d { get; set; }
        public float EventDwell30d { get; set; } // sum dwell/2000

        // NEW: timing
        public float IsUpcoming { get; set; }    // 0/1
        public float DaysToEvent { get; set; }   // clamped

        // (Optional, recommended) learned blend: include MF score as a feature
        public float MFScore { get; set; }
    }

    public sealed class RankPrediction
    {
        public float Score { get; set; }
    }
}
