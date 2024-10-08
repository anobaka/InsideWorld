using Newtonsoft.Json;

namespace Bakabase.Modules.ThirdParty.ThirdParties.Bilibili.Models
{
    public class Favorites
    {
        public long Id { get; set; }
        public string Title { get; set; }
        [JsonProperty("media_count")]
        public int MediaCount { get; set; }
    }
}