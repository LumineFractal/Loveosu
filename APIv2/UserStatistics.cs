using Newtonsoft.Json;

namespace Loveosu.APIv2
{
    class UserStatistics
    {
        [JsonProperty("grade_counts")]
        public GradeCounts? GradeCounts { get; set; }

        [JsonProperty("hit_accuracy")]
        public double HitAccuracy { get; set; }

        [JsonProperty("level")]
        public Level? Level { get; set; }

        [JsonProperty("play_count")]
        public int PlayCount { get; set; }

        [JsonProperty("play_time")]
        public int PlayTime { get; set; }

        [JsonProperty("pp")]
        public float Performance { get; set; }

        [JsonProperty("ranked_score")]
        public long RankedScore { get; set; }

        [JsonProperty("replays_watched_by_others")]
        public int ReplaysWatched { get; set; }

        [JsonProperty("total_hits")]
        public int TotalHits { get; set; }

        [JsonProperty("total_score")]
        public long TotalScore { get; set; }

        [JsonProperty("global_rank")]
        public int GlobalRank { get; set; }

        [JsonProperty("country_rank")]
        public int CountryRank { get; set; }
    }
}
