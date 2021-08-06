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
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        public string Description { get; set; }
        public User Owner { get; set; }
        public List<User> Contributors { get; set; }
        public List<User> Stakeholders { get; set; }
        public List<Feature> Branches { get; set; }
        public bool IsMain { get; set; }
        public List<string> Tags { get; set; }
    }
}
