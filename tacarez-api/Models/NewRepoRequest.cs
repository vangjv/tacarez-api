using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class NewRepoRequest
    {
        [JsonProperty("name")]
        public string name { get; set; }
        [JsonProperty("description")]
        public string description { get; set; }
        [JsonProperty("private")]
        public bool? is_private { get; set; } = false;
        public bool? has_issues { get; set; } = false;
        public bool? has_projects { get; set; } = false;
        public bool? has_wiki { get; set; } = false;
        public string fileName { get; set; } = "data.geojson";
        public string message { get; set; }
        public string content { get; set; }
    }
}
