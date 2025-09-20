namespace EventRecommender.ML
{

public sealed class MfRow
{
    // Stage A: implicit/explicit signal collapsed to a numeric "preference"
    // e.g. click/view=0.2, Interested=0.7, Going=1.0, optional rating normalized
    public string UserId { get; set; } = default!;     // key-encoded
    public int EventId { get; set; }    // key-encoded
    public float Label { get; set; }   // preference score
}

public sealed class RankRow
{
        // Stage B: feature-expanded training rows
        public float Label { get; set; }
        public string GroupId { get; set; } = default!;   // <-- raw userId, will map to Key
        public float EventRecency { get; set; }
        public float OrganizerScore { get; set; }
        public float VenueCapacity { get; set; }
        public float CategoryId { get; set; }
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }
    }

    public sealed class RankPrediction { public float Score { get; set; } }

}