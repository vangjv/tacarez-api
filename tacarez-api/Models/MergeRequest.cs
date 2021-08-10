﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class MergeRequest
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
        public string FeatureName { get; set; }
        public string RevisionName { get; set; }
        public string GitHubRawURL { get; set; }
        public string Status { get; set; }
        public User Owner { get; set; }
        public User MergeRequester { get; set; }        
        public List<User> Contributors { get; set; }
        public StakeHolderReview StakeholderReview { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
    }

    public class StakeHolderReview
    {
        public string EnvelopeId { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public List<User> Stakeholders { get; set; }

    }
}