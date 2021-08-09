using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tacarez_api
{
    public class UpdateFeaturePropertiesRequest
    {
        public string FeatureName{ get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
    }
}
