namespace EventRecommender.ML;

public sealed class MfRow
{
    // Stage A: implicit/explicit signal collapsed to a numeric "preference"
    // e.g. click/view=0.2, Interested=0.7, Going=1.0, optional rating normalized
    public uint UserId;     // key-encoded
    public uint EventId;    // key-encoded
    public float Label;     // preference score
}

public sealed class RankRow
{
    // Stage B: feature-expanded training rows
    public float Label;        // 0/1 or graded relevance (e.g., 1 for interacted, 0 for ignored)
    public float GroupId;      // user as group for ranking
    public float EventRecency; // days since creation (or inverted)
    public float OrganizerScore;
    public float VenueCapacity;
    public float CategoryId;   // numeric category
    public float HourOfDay;    // from event time
    public float DayOfWeek;    // from event time
    // You can add more features later (distance to venue, friend-count going, etc.)
}

public sealed class RankPrediction
{
    public float Score;
}
