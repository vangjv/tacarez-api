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

        [FunctionName("GeoJSONAPI")]
        public async Task<HttpResponseMessage> GeoJSON(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "geojson/{featureName}")] HttpRequest req,
            string featureName, ILogger log)
        {
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.type = 'feature' AND c.id = @featureName")
                .WithParameter("@featureName", featureName);
                FeedIterator<Feature> queryResultSetIterator = _container.GetItemQueryIterator<Feature>(queryDefinition);
                List<Feature> features = new List<Feature>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<Feature> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    if (currentResultSet.Count > 0)
                    {
                        foreach (Feature feature in currentResultSet)
                        {
                            features.Add(feature);
                            Console.WriteLine("\tRead {0}\n", feature);
                        }
                    }
                }
                if (features.Count > 0)
                {
                    //download geojson
                    var client = new RestClient(features[0].GitHubRawURL);
                    client.Timeout = -1;
                    var request = new RestRequest(Method.GET);
                    IRestResponse response = client.Execute(request);
                    Console.WriteLine(response.Content);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(response.Content, Encoding.UTF8, "application/json")
                    };
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}
