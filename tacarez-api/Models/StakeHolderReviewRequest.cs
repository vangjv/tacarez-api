using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class StakeHolderReviewRequest
    {
        public string MergeId { get; set; }
        public string SenderName{ get; set; }
        public string MessageFromSender{ get; set; }
        public List<User> Stakeholders { get; set; }

    }
}
