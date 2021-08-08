using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tacarez_api
{
    public class Feature
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string GitHubName { get; set; }
        public string GitHubRawURL { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public User Owner { get; set; }
        public List<User> Contributors { get; set; }
        public List<User> Stakeholders { get; set; }
        public List<Feature> Branches { get; set; }
        public List<string> Tags { get; set; }
    }
}
