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
using tacarez_api.Models;
using tacarez_api.Utility;
using RestSharp;
using System.Collections.Generic;

namespace tacarez_api
{
    public class MergeAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;
        public MergeAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("CreateMergeRequest")]
        public async Task<IActionResult> CreateMergeRequest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mergerequest")] HttpRequest req,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                MergeRequestRequest mergeRequest = JsonConvert.DeserializeObject<MergeRequestRequest>(requestBody);
                if (mergeRequest.FeatureName == null)
                {
                    return new BadRequestObjectResult("A feature name is required");
                }
                if (mergeRequest.RevisionName == null)
                {
                    return new BadRequestObjectResult("A revision name is required");
                }
                mergeRequest.FeatureName = mergeRequest.FeatureName.Replace(" ", "-");
                mergeRequest.FeatureName = mergeRequest.FeatureName.ToLower();
                mergeRequest.RevisionName = mergeRequest.RevisionName.Replace(" ", "-");
                mergeRequest.RevisionName = mergeRequest.RevisionName.ToLower();                
                string mergeId = "merge-" + Guid.NewGuid().ToString();
                ItemResponse<Feature> getFeatureResponse = await _container.ReadItemAsync<Feature>(mergeRequest.FeatureName, new PartitionKey("feature"))
                    .ConfigureAwait(false);
                Feature feature = getFeatureResponse.Resource;
                if (feature == null)
                {
                    return new BadRequestObjectResult("No feature with that name exist.");
                }
                ItemResponse<Revision> getRevisionResponse = await _container.ReadItemAsync<Revision>(mergeRequest.FeatureName + mergeRequest.RevisionName, new PartitionKey("revision"));
                Revision revision = getRevisionResponse.Resource;
                if (revision == null)
                {
                    return new BadRequestObjectResult("No feature with that name exist.");
                }
                NewBranchRequest newBranchRequest = new NewBranchRequest
                {
                    RepoName = mergeRequest.FeatureName,
                    BranchName = mergeId
                };
                var client = new RestClient(_config["GitHubEndpoint"] + "/api/branch/" + mergeRequest.RevisionName);
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(newBranchRequest);
                IRestResponse response = client.Execute(request);
                Console.WriteLine("Response from creating merge from Github: " + response.Content);
                if (response.IsSuccessful == false)
                {
                    return new BadRequestObjectResult("Unable to create revision.");
                }
                dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                if (gitHubResponse.errors != null)
                {
                    return new BadRequestObjectResult(gitHubResponse.errors);
                }
                //get owner and stakeholder of original feature

                MergeRequest newMergeRequest = new MergeRequest
                {
                    Id = mergeId,
                    Type = "merge",
                    FeatureName = mergeRequest.FeatureName,
                    RevisionName = mergeRequest.RevisionName,
                    Owner = feature.Owner,
                    MergeRequester = revision.Owner,
                    MergeRequesterNotes = mergeRequest.MergeRequesterNotes,
                    CreatedDate = DateTime.Now,
                    GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + mergeRequest.FeatureName + "/" + mergeId + "/data.geojson",
                    StakeholderReview = new StakeHolderReview
                    {
                        Stakeholders = feature.Stakeholders
                    }
                };                
                ItemResponse<MergeRequest> item = await _container.CreateItemAsync(newMergeRequest, new PartitionKey(newMergeRequest.Type));
                _logger.LogInformation("Item inserted");
                _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                return new OkObjectResult(item.Resource);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("GetMergeRequestByUser")]
        public async Task<IActionResult> GetMergesByUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mergerequest/user/{guid}")] HttpRequest req, string guid)
        {
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.type = 'merge' AND c.Owner.GUID = @guid")
                .WithParameter("@guid", guid);
                FeedIterator<MergeRequest> queryResultSetIterator = _container.GetItemQueryIterator<MergeRequest>(queryDefinition);
                List<MergeRequest> mergeRequests = new List<MergeRequest>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<MergeRequest> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    if (currentResultSet.Count > 0)
                    {
                        foreach (MergeRequest mergeRequest in currentResultSet)
                        {
                            mergeRequests.Add(mergeRequest);
                            Console.WriteLine("\tRead {0}\n", mergeRequest);
                        }
                    }
                }
                if (mergeRequests.Count > 0)
                {
                    return new OkObjectResult(mergeRequests);
                }
                else
                {
                    return new NotFoundObjectResult("No merge requests found for that user");
                }
            }
            catch (Exception ex)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetMergeRequestByName")]
        public async Task<IActionResult> GetMergeRequestByName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mergerequest/feature/{featureName}/{mergeId}")] HttpRequest req, string featureName,
            string mergeId)
        {
            try
            {
                featureName = featureName.ToLower();
                featureName = featureName.Replace(" ", "-");
                mergeId = mergeId.ToLower();
                mergeId = mergeId.Replace(" ", "-");
                ItemResponse<MergeRequest> mergeRequestSearch = await _container.ReadItemAsync<MergeRequest>(mergeId, new PartitionKey("merge"))
                .ConfigureAwait(false);

                MergeRequest mergeRequest = mergeRequestSearch.Resource;
                if (mergeRequest == null)
                {
                    return new NotFoundObjectResult("No merge request found with that id");
                }
                else
                {
                    return new OkObjectResult(mergeRequest);
                }
            }
            catch (Exception ex)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
