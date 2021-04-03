using Newtonsoft.Json;
using System.Collections.Generic;

namespace Loveosu.APIv2
{
    class User
    {
        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("scores_first_count")]
        public int FirstPlacesCount { get; set; }

        [JsonProperty("follower_count")]
        public int FollowerCount { get; set; }

        [JsonProperty("monthly_playcounts")]
        public IList<MonthlyPlaycounts>? MonthlyPlaycounts { get; set; }

        [JsonProperty("statistics")]
        public UserStatistics? Statistics { get; set; }
    }
}
