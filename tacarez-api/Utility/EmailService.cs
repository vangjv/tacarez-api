using Microsoft.Extensions.Configuration;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace tacarez_api.Utility
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        string emailEndpoint;
        public EmailService (IConfiguration config)
        {
            _config = config;
            emailEndpoint = _config["EmailAPIEndpoint"];
        }
        public bool SendEmail(string recipient, string subject, string body)
        {
            
            RestClient client = new RestClient(emailEndpoint);
            client.Timeout = -1;
            RestRequest request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            EmailRequest emailRequest = new EmailRequest(recipient, subject, body);
            request.AddJsonBody(emailRequest);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);
            if (response.IsSuccessful)
            {
                return true;
            } else
            {
                return false;
            }
        }

    }

    public class EmailRequest {
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public EmailRequest(string recipient, string subject, string body)
        {
            Recipient = recipient;
            Subject = subject;
            Body = body;
        }
    }
}
