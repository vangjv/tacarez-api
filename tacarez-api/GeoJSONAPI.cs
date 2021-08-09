using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using RestSharp;
using System.Net.Http;
using System.Text;
using System.Net;

namespace tacarez_api
{
    public class GeoJSONAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public GeoJSONAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("GetFeatureGeoJson")]
        public async Task<HttpResponseMessage> GetFeatureGeoJson(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "geojson/{featureName}")] HttpRequest req,
            string featureName, ILogger log)
        {
            try
            {
                ItemResponse<Feature> featureSearch = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"))
                  .ConfigureAwait(false);

                Feature feature = featureSearch.Resource;
                if (feature == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
        
                //download geojson
                var client = new RestClient(feature.GitHubRawURL);
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response.Content, Encoding.UTF8, "application/json")
                };
                
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        [FunctionName("GetRevisionGeoJson")]
        public async Task<HttpResponseMessage> GetRevisionGeoJson(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "geojson/{featureName}/{revisionName}")] HttpRequest req,
            string featureName, string revisionName, ILogger log)
        {
            try
            {
                ItemResponse<Revision> revisionSearch = await _container.ReadItemAsync<Revision>(featureName + revisionName, new PartitionKey("revision"))
                  .ConfigureAwait(false);

                Revision revision = revisionSearch.Resource;
                if (revision == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                //download geojson
                var client = new RestClient(revision.GitHubRawURL);
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response.Content, Encoding.UTF8, "application/json")
                };

            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}
