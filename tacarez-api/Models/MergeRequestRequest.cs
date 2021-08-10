using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class MergeRequestRequest
    {
        public string FeatureName { get; set; }
        public string RevisionName { get; set; }
    }
}
