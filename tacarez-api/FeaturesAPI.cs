using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using tacarez_api.Models;
using RestSharp;

namespace tacarez_api
{
    public class FeaturesAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public FeaturesAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("GetAllFeature")]
        public async Task<IActionResult> GetAllFeatures(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "features")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                var sqlQueryText = "SELECT * FROM c";
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                FeedIterator<Feature> queryResultSetIterator = _container.GetItemQueryIterator<Feature>(queryDefinition);
                List<Feature> features = new List<Feature>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<Feature> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (Feature feature in currentResultSet)
                    {
                        features.Add(feature);
                        Console.WriteLine("\tRead {0}\n", feature);
                    }
                }
                returnValue = new OkObjectResult(features);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            return returnValue;
        }

        [FunctionName("GetByFeatureName")]
        public async Task<IActionResult> GetByFeatureName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "features/{featureName}")] HttpRequest req, string featureName)
        {
            IActionResult returnValue = null;

            try
            {
                var sqlQueryText = $"SELECT * FROM c WHERE c.name = '{featureName}'";
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
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
                    returnValue = new OkObjectResult(features[0]);
                } else
                {
                    returnValue = new NotFoundObjectResult("No feature found with that name");
                }                
            }
            catch (Exception ex)
            {
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            return returnValue;
        }

        [FunctionName("AddFeature")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "features")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                NewFeatureRequest featureRequest = JsonConvert.DeserializeObject<NewFeatureRequest>(requestBody);
                Feature newFeature = featureRequest.feature;                
                if (newFeature.Name == null)
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
                newFeature.Id = newFeature.Name;
                NewRepoRequest newRepoReq = new NewRepoRequest
                {
                    name = featureRequest.feature.Name,
                    description = featureRequest.feature.Description,
                    is_private = false,
                    has_issues = false,
                    has_projects = false,
                    has_wiki = false,
                    message = featureRequest.message,
                    content = featureRequest.content
                };
                var client = new RestClient(_config["GitHubEndpoint"] + "/api/CreateRepo");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(newRepoReq);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                if (gitHubResponse.errors != null)
                {
                    return new BadRequestObjectResult(gitHubResponse.errors);
                }
                if (gitHubResponse.message != null)
                {
                    if (((string)gitHubResponse.message).StartsWith("Invalid request"))
                    {
                        return new BadRequestObjectResult(gitHubResponse.message);
                    }                   
                }
                if (response.IsSuccessful == false)
                {
                    return new BadRequestObjectResult("Unable to create feature");
                }
                ItemResponse<Feature> item = await _container.CreateItemAsync(newFeature, new PartitionKey(newFeature.Name));
                _logger.LogInformation("Item inserted");
                _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                returnValue = new OkObjectResult(newFeature);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                
            }
            return returnValue;
        }
    }
}