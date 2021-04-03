using Newtonsoft.Json;

namespace Loveosu.APIv2
{
    class Level
    {
        [JsonProperty("current")]
        public int Current { get; set; }

        [JsonProperty("progress")]
        public int Progress { get; set; }
    }
}
