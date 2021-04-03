using Newtonsoft.Json;

namespace Loveosu.APIv2
{
    class MonthlyPlaycounts
    {
        [JsonProperty("start_date")]
        public string? StartDate { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }
}
