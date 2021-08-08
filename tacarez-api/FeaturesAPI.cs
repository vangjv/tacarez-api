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

        [FunctionName("GetAllFeatures")]
        public async Task<IActionResult> GetAllFeatures(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "features")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                var sqlQueryText = "SELECT * FROM c WHERE c.type = 'feature'";
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
                ItemResponse<Feature> response = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"))
                .ConfigureAwait(false);

                Feature item = response.Resource;
                if (item == null)
                {
                    returnValue = new NotFoundObjectResult("No feature found with that name");
                } else
                {
                    returnValue = new OkObjectResult(item);
                }          
            }
            catch (Exception ex)
            {
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            return returnValue;
        }

        [FunctionName("GetByUser")]
        public async Task<IActionResult> GetByUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "features/user/{guid}")] HttpRequest req, string guid)
        {
            IActionResult returnValue = null;

            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.type = 'feature' AND c.Owner.GUID = @guid")
                .WithParameter("@guid", guid);
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
                    returnValue = new OkObjectResult(features);
                }
                else
                {
                    returnValue = new NotFoundObjectResult("No features found for that user");
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
                Feature newFeature = featureRequest.feature.toFeature();
                newFeature.Type = "feature";
                if (newFeature.Id == null)
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                if (await doesFeatureExist(newFeature.Id) == true)
                {
                    return new BadRequestObjectResult("A feature with that name already exist");
                }
                NewRepoRequest newRepoReq = new NewRepoRequest
                {
                    name = featureRequest.feature.Id,
                    description = featureRequest.feature.Description,
                    message = featureRequest.message,
                    content = featureRequest.content
                };
                var client = new RestClient(_config["GitHubEndpoint"] + "/api/repo");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(newRepoReq);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                if (response.IsSuccessful == false)
                {
                    return new BadRequestObjectResult("Unable to create feature. Please try another name.");
                }
                dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                if (gitHubResponse.errors != null)
                {
                    return new BadRequestObjectResult(gitHubResponse.errors);
                }
                GithubNewRepoResponse gitHubRepoResponse;
                try
                {
                    gitHubRepoResponse = JsonConvert.DeserializeObject<GithubNewRepoResponse>(response.Content);
                } catch (Exception e)
                {
                    return new BadRequestObjectResult("Unable to parse GitHub response:" + e.Message);
                }
                
                if (gitHubResponse.message != null)
                {
                    if (((string)gitHubResponse.message).StartsWith("Invalid request"))
                    {
                        return new BadRequestObjectResult(gitHubResponse.message);
                    }                   
                }

                newFeature.GitHubName = getRepoNameFromURL(gitHubRepoResponse.content.url);
                newFeature.GitHubRawURL = gitHubRepoResponse.content.downloadUrl;
                ItemResponse<Feature> item = await _container.CreateItemAsync(newFeature, new PartitionKey(newFeature.Type));
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

        [FunctionName("UpdateFeature")]
        public async Task<IActionResult> UpdateFeature(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "features/{featureName}/{branch}")] HttpRequest req, 
            string featureName, string branch)
        {
            IActionResult returnValue = null;
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            if (branch == null)
            {
                return new BadRequestObjectResult("Please include the branch name.");
            }
            //##NEEDS IMPLEMENTATION check if request is owner 
            
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                GithubContent updateRequest = JsonConvert.DeserializeObject<GithubContent>(requestBody);

                if (updateRequest.message == null || updateRequest.content == null)
                {
                    return new BadRequestObjectResult("Invalid request");
                }

                var client = new RestClient(_config["GitHubEndpoint"] + "/api/repo/" + featureName + "/" + branch);
                client.Timeout = -1;
                var request = new RestRequest(Method.PUT);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(updateRequest);
                IRestResponse response = client.Execute(request);
                if (response.IsSuccessful == false)
                {
                    return new BadRequestObjectResult("Unable to update feature.");
                }
                dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                if (gitHubResponse.errors != null)
                {
                    return new BadRequestObjectResult(gitHubResponse.errors);
                }
                returnValue = new OkObjectResult(gitHubResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new BadRequestObjectResult("Unable to update feature.");

            }
            return returnValue;
        }

        public async Task<bool> doesFeatureExist(string featureName)
        {
            try
            {
                ItemResponse<Feature> featureResponse = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"));
                if (featureResponse.Resource != null)
                {
                    return true;
                } else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }            
        }

        public string getRepoNameFromURL(string gitHubURL)
        {
            //cuts out url and parses name
            //"https://api.github.com/repos/dshackathon/usa-map/contents/data.geojson?ref=main";
            string beginningStripped = gitHubURL.Substring(41);
            int indexOfSlash = beginningStripped.IndexOf("/");
            return beginningStripped.Substring(0, indexOfSlash);
        }
    }
}