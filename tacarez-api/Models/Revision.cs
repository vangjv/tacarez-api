using Newtonsoft.Json;
using System.Collections.Generic;

namespace tacarez_api
{
    public class Revision
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
        public string FeatureName { get; set; }
        public string RevisionName { get; set; }
        public string GitHubRawURL { get; set; }
        public string Description { get; set; }
        public User Owner { get; set; }
        public List<User> Contributors { get; set; }
    }
}
