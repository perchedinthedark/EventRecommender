namespace EventRecommender.ML;

public sealed class RecommenderConfig
{
    public string ModelDir { get; set; } = "App_Data/models"; // create this folder
    public string MfModelPath => System.IO.Path.Combine(ModelDir, "mf.zip");
    public string RankModelPath => System.IO.Path.Combine(ModelDir, "rank.fstr.zip");

    // Label mapping (tune as you like)
    public float ScoreView = 0.2f;
    public float ScoreInterested = 0.7f;
    public float ScoreGoing = 1.0f;
    public float ScoreRated(int rating) => rating switch
    {
        <= 0 => 0f,
        1 => 0.4f,
        2 => 0.55f,
        3 => 0.7f,
        4 => 0.85f,
        5 => 1.0f,
        _ => 0.7f
    };

    // Candidate pool size from Stage A → Stage B
    public int CandidatesPerUser = 100;
}
