using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class NewRevisionRequest
    {
        public string FeatureName { get; set; }
        public string RevisionName { get; set; }
        public string Description{ get; set; }
        public User Owner { get; set; }
    }
}
