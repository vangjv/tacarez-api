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
                featureName = featureName.ToLower();
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
        public async Task<IActionResult> AddFeature(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "features")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                NewFeatureRequest featureRequest = JsonConvert.DeserializeObject<NewFeatureRequest>(requestBody);
                //replace spaces with hyphen
                featureRequest.feature.Id = featureRequest.feature.Id.Replace(" ", "-");
                featureRequest.feature.Id = featureRequest.feature.Id.ToLower();
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
                newFeature.CreatedDate = DateTime.Now;
                newFeature.LastModifiedDate = DateTime.Now;
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
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            if (branch == null)
            {
                return new BadRequestObjectResult("Please include the branch name.");
            }
            //##NEEDS IMPLEMENTATION check if request is owner 
            featureName = featureName.ToLower();
            featureName = featureName.Replace(" ", "-");
            branch = branch.ToLower();
            branch = branch.Replace(" ", "-");
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

                //update last modified date
                //get copy
                //get feature
                if (branch == "main")
                {
                    ItemResponse<Feature> featureSearch = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"))
                        .ConfigureAwait(false);
                    Feature featureToUpdate = featureSearch.Resource;
                    if (featureToUpdate == null)
                    {
                        return new NotFoundObjectResult("No feature found with that name");
                    }
                    featureToUpdate.LastModifiedDate = DateTime.Now;
                    //cache bust
                    featureToUpdate.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + featureToUpdate.Id + "/" + gitHubResponse.commit.sha + "/data.geojson";
                    var replaceItemResponse = await _container.ReplaceItemAsync<Feature>(featureToUpdate, featureToUpdate.Id, new PartitionKey("feature"));
                } else
                {
                    ItemResponse<Revision> revisionSearch = await _container.ReadItemAsync<Revision>(featureName + branch, new PartitionKey("revision"))
                       .ConfigureAwait(false);
                    Revision revisionToUpdate = revisionSearch.Resource;
                    if (revisionToUpdate == null)
                    {
                        return new NotFoundObjectResult("No feature found with that name");
                    }
                    revisionToUpdate.LastModifiedDate = DateTime.Now;
                    //cache bust
                    revisionToUpdate.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + revisionToUpdate.FeatureName + "/" + gitHubResponse.commit.sha + "/data.geojson";
                    var replaceItemResponse = await _container.ReplaceItemAsync<Revision>(revisionToUpdate, featureName + branch, new PartitionKey("revision"));
                }
               
                //modify copy

                return new OkObjectResult(gitHubResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new BadRequestObjectResult("Unable to update feature.");
            }
        }

        [FunctionName("UpdateFeatureProperties")]
        public async Task<IActionResult> UpdateFeatureProperties(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "featureproperties")] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateFeaturePropertiesRequest featureProperties;
            try
            {
                featureProperties = JsonConvert.DeserializeObject<UpdateFeaturePropertiesRequest>(requestBody);
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult("Invalid request");
            }
            if (featureProperties == null || featureProperties.FeatureName == null)
            {
                return new BadRequestObjectResult("Invalid request");
            }
            if (featureProperties.Description == null && featureProperties.Tags == null)
            {
                return new BadRequestObjectResult("Nothing to update");
            }

                //get feature
                ItemResponse<Feature> response = await _container.ReadItemAsync<Feature>(featureProperties.FeatureName, new PartitionKey("feature"))
              .ConfigureAwait(false);
            Feature featureToUpdate = response.Resource;
            if (featureToUpdate == null)
            {
                return new NotFoundObjectResult("No feature found with that name");
            }
            //replace stakeholders
            if (featureProperties.Description != null)
            {
                featureToUpdate.Description = featureProperties.Description;
            }

            if (featureProperties.Tags != null)
            {
                featureToUpdate.Tags = featureProperties.Tags;
            }

            var replaceItemResponse = await _container.ReplaceItemAsync<Feature>(featureToUpdate, featureToUpdate.Id, new PartitionKey("feature"));

            return new OkObjectResult(replaceItemResponse.Resource);
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