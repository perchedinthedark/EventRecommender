// File: ML/Schemas.cs
namespace EventRecommender.ML
{
    // ---------- Stage A (MF) ----------
    // Use raw IDs; pipeline maps them to keys.
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
    // GroupId is the user id (string) → MapValueToKey at train time.
    public sealed class RankRow
    {
        public string GroupId { get; set; } = default!;
        public float Label { get; set; }

        // Existing base features
        public float EventRecency { get; set; }
        public float OrganizerScore { get; set; }
        public float VenueCapacity { get; set; }
        public float CategoryId { get; set; }
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }

        // Social (normalized)
        public float FriendsGoingRate { get; set; }       // friends going ÷ total going
        public float FriendsInterestedRate { get; set; }  // friends interested ÷ total interested

        // User-specific affinities
        public float UserCatAffinity { get; set; }   // preference for this category
        public float UserHourAffinity { get; set; }  // preference for this hour-of-day
        public float UserDowAffinity { get; set; }   // preference for this day-of-week

        // Organizer familiarity for this user
        public float OrganizerUserPrior { get; set; }

        // Recent engagement (popularity)
        public float EventClicks30d { get; set; }
        public float EventDwell30d { get; set; }

        // Timing
        public float IsUpcoming { get; set; }
        public float DaysToEvent { get; set; }

        // Learned blend from MF score
        public float MFScore { get; set; }
    }

    public sealed class RankPrediction
    {
        public float Score { get; set; }
    }
}
