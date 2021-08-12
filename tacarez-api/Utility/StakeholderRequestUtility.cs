using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using tacarez_api.Models;
using System.Security.Cryptography;
using System.IO;

namespace tacarez_api.Utility
{
    public class StakeholderRequestUtility
    {
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;
        public StakeholderRequestUtility(IConfiguration config, CosmosClient cosmosClient)
        {
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        public async Task<MergeRequest> GetMergeRequest(string mergeId)
        {
            ItemResponse<MergeRequest> mergeRequestSearch = await _container.ReadItemAsync<MergeRequest>(mergeId, new PartitionKey("merge"))
                .ConfigureAwait(false);

            MergeRequest mergeRequest = mergeRequestSearch.Resource;
            return mergeRequest;
        }

        public string FeatureLink(MergeRequest mergeRequest)
        {
            return "https://www.tacarez.com/feature/" + mergeRequest.FeatureName;
        }
        public static string MergeRequestLink(MergeRequest mergeRequest)
        {
            return "https://www.tacarez.com/mergerequest/" + mergeRequest.FeatureName + "/" + mergeRequest.Id;
        }

        //download geojson map data
        public async Task<string> GetGeoJson(string url)
        {
            //download geojson
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            IRestResponse response = await client.ExecuteAsync(request);
            return response.Content;
        }

        public string GetHashOfGeoJson(string geojson)
        {
            return sha256_hash(geojson);
        }

        public string GetScreenshotURL(MergeRequest mergeRequest)
        {
            return "https://www.tacarez.com/screenshot/" + mergeRequest.FeatureName + "/" + mergeRequest.Id;
        }

        //get hash of string
        public string sha256_hash(String value)
        {
            StringBuilder Sb = new StringBuilder();

            using (SHA256 hash = SHA256Managed.Create())
            {
                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
            }
            return Sb.ToString();
        }

        //get map image as base64 encoded string
        public async Task<string> GetMapScreenshot(string Url)
        {
            //download geojson
            var client = new RestClient("https://api.cloudmersive.com/convert/web/url/to/screenshot");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            ScreenshotRequest screenshotRequest = new ScreenshotRequest(Url);
            request.AddHeader("Apikey", _config["CloudmersiveAPIKey"]);
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(screenshotRequest);
            IRestResponse response = await client.ExecuteAsync(request);
            byte[] screenshot = response.RawBytes;
            //convert image to base64
            return Convert.ToBase64String(screenshot);
        }

        public async Task<string> SendEnvelopeRequest(EnvelopeRequest envelopeRequest)
        {
            var client = new RestClient(_config["TacarEZDocusignEndpoint"] + "/api/GenerateEnvelope");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(envelopeRequest);
            IRestResponse response = await client.ExecuteAsync(request);
            Console.WriteLine(response.Content);
            return response.Content;
        }

        public List<EnvelopeRecipient> RecipientsFromUsersList(List<User> stakeholders)
        {
            List<EnvelopeRecipient> recipients = new List<EnvelopeRecipient>();
            stakeholders.ForEach(stakeholder =>
            {
                recipients.Add(new EnvelopeRecipient
                {
                    name = stakeholder.FirstName + " " + stakeholder.LastName,
                    email = stakeholder.Email
                });
            });
            return recipients;
        }

        public async Task<MergeRequest> CompleteEnvelopeRequest(string mergeId, string senderName, string messageFromSender, List<User> stakeholders)
        {
            MergeRequest mergeRequest = await GetMergeRequest(mergeId);
            string mergeRawData = await GetGeoJson(mergeRequest.GitHubRawURL);
            string base64MapPreview = await GetMapScreenshot(GetScreenshotURL(mergeRequest));
            //generate list of recipients from list of users

            //generate envelope request 
            EnvelopeRequest newEnvelope = new EnvelopeRequest
            {
                senderName = senderName,
                messageFromSender = messageFromSender,
                mapFeatureName = mergeRequest.FeatureName,
                originalMapFeatureLink = FeatureLink(mergeRequest),
                mergeRequestLink = MergeRequestLink(mergeRequest),
                mergeRequesterNotes = mergeRequest.MergeRequesterNotes,
                stakeholderReviewStartDate = DateTime.Now.ToString(),
                hashOfMergeRequestData = GetHashOfGeoJson(mergeRawData),
                rawMergeRequestData = mergeRawData,
                mapPreviewImage = base64MapPreview,
                recipients = RecipientsFromUsersList(stakeholders)
            };
            //http call to tacarezdocusignapi
            string envelopeId = await SendEnvelopeRequest(newEnvelope);
            //save envelope data to merge request
            mergeRequest.StakeholderReview.EnvelopeId = envelopeId;
            mergeRequest.StakeholderReview.Status = "Review sent";
            mergeRequest.StakeholderReview.CreatedDate = DateTime.Now;
            mergeRequest.StakeholderReview.MessageToStakeholders = messageFromSender;
            mergeRequest.StakeholderReview.Stakeholders = stakeholders;
            var replaceItemResponse = await _container.ReplaceItemAsync<MergeRequest>(mergeRequest, mergeRequest.Id, new PartitionKey("merge"));
            return replaceItemResponse.Resource;
        }
    }

    class ScreenshotRequest
    {
        public ScreenshotRequest(string url)
        {
            Url = url;
        }
        public string Url { get; set; }
        public string ExtraLoadingWait { get; set; } = "0";
        public string ScreenshotWidth { get; set; } = "1920";
        public string ScreenshotHeight { get; set; } = "1080";
    }
}
