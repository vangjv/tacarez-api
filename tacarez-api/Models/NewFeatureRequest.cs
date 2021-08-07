using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class NewFeatureRequest
    {
        public NewFeature feature { get; set; }
        public string message { get; set; }
        public string content { get; set; }

    }

    public class NewFeature
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Description { get; set; }
        public User Owner { get; set; }
        public List<string> Tags { get; set; }
        public Feature toFeature()
        {
            Feature newFeature = new Feature();
            newFeature.Id = Id;
            newFeature.Owner = Owner;
            newFeature.Tags = Tags;
            newFeature.Description = Description;
            newFeature.Type = "feature";
            return newFeature;
        }
    }
}
