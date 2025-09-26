namespace EventRecommender.ML
{
    // ---------------- Stage A (MF) ----------------
    // Use raw IDs and let the pipeline map them to keys.
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

    // ---------------- Stage B (Ranking) ----------------
    // GroupId is the user id (string); pipeline will MapValueToKey -> GroupKey.
    public sealed class RankRow
    {
        public string GroupId { get; set; } = default!; // user id
        public float Label { get; set; }                // 1 positive, 0 negative

        // Core features
        public float EventRecency { get; set; }   // exp(-daysAgo / 30)
        public float OrganizerScore { get; set; } // organizer popularity proxy
        public float VenueCapacity { get; set; }
        public float CategoryId { get; set; }     // numeric id
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }

        // Social (ratios among all attendees for the event)
        public float FriendsGoingRatio { get; set; }      // friendsGoing / totalGoing
        public float FriendsInterestedRatio { get; set; } // friendsInterested / totalInterested
        public float FriendsAvgRating { get; set; }       // 0..1 (avg/5)

        // User affinity
        public float UserCatAffinity { get; set; }        // user weight for this category / total

        // Recent engagement (last 30 days)
        public float EventClicks30d { get; set; }
        public float EventDwell30d { get; set; }          // sum(dwellMs)/2000

        // Timing
        public float IsUpcoming { get; set; }             // 0/1
        public float DaysToEvent { get; set; }            // clamped [-7, 60]

        // Learned blend (MF score as a feature)
        public float MFScore { get; set; }
    }

    public sealed class RankPrediction
    {
        public float Score { get; set; }
    }
}


