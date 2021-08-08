using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class GithubContent
    {
        public string message { get; set; }
        public string content { get; set; }
        public Committer committer { get; set; }

    }

}
