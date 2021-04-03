using Newtonsoft.Json;

namespace Loveosu.APIv2
{
    class AccessToken
    {
        [JsonProperty("token_type")]
        public string? TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("access_token")]
        public string? Token { get; set; }
    }
}
