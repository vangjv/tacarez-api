using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    public class GithubNewRepoResponse
    {
        public Content content { get; set; }
        public Commit commit { get; set; }
    }

    public class Links
    {
        public string self { get; set; }
        public string git { get; set; }
        public string html { get; set; }
    }

    public class Content
    {
        public string name { get; set; }
        public string path { get; set; }
        public string sha { get; set; }
        public int size { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string git_url { get; set; }
        public string download_url { get; set; }
        public string type { get; set; }
        public Links _links { get; set; }
    }

    public class Author
    {
        public string name { get; set; }
        public string email { get; set; }
        public DateTime date { get; set; }
    }

    public class Committer
    {
        public string name { get; set; }
        public string email { get; set; }
        public DateTime date { get; set; }
    }

    public class Tree
    {
        public string sha { get; set; }
        public string url { get; set; }
    }

    public class Verification
    {
        public bool verified { get; set; }
        public string reason { get; set; }
        public object signature { get; set; }
        public object payload { get; set; }
    }

    public class Commit
    {
        public string sha { get; set; }
        public string node_id { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public Author author { get; set; }
        public Committer committer { get; set; }
        public Tree tree { get; set; }
        public string message { get; set; }
        public List<object> parents { get; set; }
        public Verification verification { get; set; }
    }

}
