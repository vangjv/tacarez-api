using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Type
    {
        public string stringValue { get; set; }
        public int value { get; set; }
    }

    public class Content
    {
        public string name { get; set; }
        public string path { get; set; }
        public string sha { get; set; }
        public int size { get; set; }
        public Type type { get; set; }
        public string downloadUrl { get; set; }
        public string url { get; set; }
        public string gitUrl { get; set; }
        public string htmlUrl { get; set; }
    }

    public class Committer
    {
        public object nodeId { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public DateTime date { get; set; }
    }

    public class Tree
    {
        public object nodeId { get; set; }
        public string url { get; set; }
        public object label { get; set; }
        public object @ref { get; set; }
        public string sha { get; set; }
        public object user { get; set; }
        public object repository { get; set; }
    }

    public class Reason
    {
        public string stringValue { get; set; }
        public int value { get; set; }
    }

    public class Verification
    {
        public bool verified { get; set; }
        public Reason reason { get; set; }
        public object signature { get; set; }
        public object payload { get; set; }
    }

    public class Commit
    {
        public string message { get; set; }
        public Author author { get; set; }
        public Committer committer { get; set; }
        public Tree tree { get; set; }
        public List<object> parents { get; set; }
        public int commentCount { get; set; }
        public Verification verification { get; set; }
        public string nodeId { get; set; }
        public string url { get; set; }
        public object label { get; set; }
        public object @ref { get; set; }
        public string sha { get; set; }
        public object user { get; set; }
        public object repository { get; set; }
    }

    public class GithubNewRepoResponse
    {
        public Content content { get; set; }
        public Commit commit { get; set; }
    }

}
