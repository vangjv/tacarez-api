using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class NewBranchRequest
    {
        public string RepoName { get; set; }
        public string BranchName { get; set; }
    }
}
