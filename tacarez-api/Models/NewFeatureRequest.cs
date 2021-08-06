using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class NewFeatureRequest
    {
        public Feature feature { get; set; }
        public string message { get; set; }
        public string content { get; set; }
    }
}
