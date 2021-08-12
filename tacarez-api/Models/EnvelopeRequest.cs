using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class EnvelopeRequest
    {
        public string senderName { get; set; }
        public string messageFromSender { get; set; }
        public string mapFeatureName { get; set; }
        public string originalMapFeatureLink { get; set; }
        public string mergeRequestLink { get; set; }
        public string mergeRequester { get; set; }
        public string mergeRequesterNotes { get; set; }
        public string stakeholderReviewStartDate { get; set; }
        public string hashOfMergeRequestData { get; set; }
        public string rawMergeRequestData { get; set; }
        public string mapPreviewImage { get; set; }
        public List<EnvelopeRecipient> recipients { get; set; }
    }

    public class EnvelopeRecipient
    {
        public string name { get; set; }
        public string email { get; set; }
    }
}
