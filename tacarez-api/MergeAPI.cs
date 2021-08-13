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

        [FunctionName("UpdateMergeRequest")]
        public async Task<IActionResult> UpdateMergeRequest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "mergerequest/feature/{featureName}/{mergeId}")] HttpRequest req,
            string featureName, string mergeId)
        {
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            if (mergeId == null)
            {
                return new BadRequestObjectResult("Please include the merge Id.");
            }
            //##NEEDS IMPLEMENTATION check if request is owner 
            featureName = featureName.ToLower();
            featureName = featureName.Replace(" ", "-");
            mergeId = mergeId.ToLower();
            mergeId = mergeId.Replace(" ", "-");
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                GithubContent updateRequest = JsonConvert.DeserializeObject<GithubContent>(requestBody);
                if (updateRequest.message == null || updateRequest.content == null)
                {
                    return new BadRequestObjectResult("Invalid request");
                }

                //check if merge request has a stakeholder revew in progress
                //get merge request
                ItemResponse<MergeRequest> mergeRequestSearch = await _container.ReadItemAsync<MergeRequest>(mergeId, new PartitionKey("merge"))
                .ConfigureAwait(false);

                MergeRequest mergeRequest = mergeRequestSearch.Resource;
                if (mergeRequest == null)
                {
                    return new NotFoundObjectResult("No merge request found with that id");
                }
                if (mergeRequest.StakeholderReview.EnvelopeId == null)
                {
                    //update the content in github only
                    var client = new RestClient(_config["GitHubEndpoint"] + "/api/repo/" + featureName + "/" + mergeId);
                    client.Timeout = -1;
                    var request = new RestRequest(Method.PUT);
                    request.AddHeader("Content-Type", "application/json");
                    request.AddJsonBody(updateRequest);
                    IRestResponse response = client.Execute(request);
                    if (response.IsSuccessful == false)
                    {
                        return new BadRequestObjectResult("Unable to update merge request.");
                    }
                    dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                    if (gitHubResponse.errors != null)
                    {
                        return new BadRequestObjectResult(gitHubResponse.errors);
                    }
                    mergeRequest.LastModifiedDate = DateTime.Now;
                    //update githubrawurl to specific commit file to bust cacheing
                    //mergeRequest.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/west-africa/1ef791185335166d5af0439c97fb8cfd23fdc967/data.geojson";
                    mergeRequest.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + mergeRequest.FeatureName + "/" + gitHubResponse.commit.sha + "/data.geojson";                    
                    var replaceItemResponse = await _container.ReplaceItemAsync<MergeRequest>(mergeRequest, mergeRequest.Id, new PartitionKey("merge"));
                    return new OkObjectResult(gitHubResponse);
                } else
                {
                    //stakeholder review is in progress. update the mergerequest in github then add new contents to the envelope
                    return new OkObjectResult("");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new BadRequestObjectResult("Unable to update feature.");
            }
        }

        [FunctionName("ApproveMergeRequest")]
        public async Task<IActionResult> ApproveMergeRequest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "merge")] HttpRequest req,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                MergeRequestAction mergeRequestAction = JsonConvert.DeserializeObject<MergeRequestAction>(requestBody);
                if (mergeRequestAction.Action == null || mergeRequestAction.MergeId == null)
                {
                    return new BadRequestObjectResult("Invalid request");
                }
                if (mergeRequestAction.Action == "approve")
                {
                    ItemResponse<MergeRequest> mergeRequestSearch = await _container.ReadItemAsync<MergeRequest>(mergeRequestAction.MergeId, new PartitionKey("merge"))
                      .ConfigureAwait(false);

                    MergeRequest mergeRequest = mergeRequestSearch.Resource;
                    if (mergeRequest == null)
                    {
                        return new NotFoundObjectResult("No merge request found with that id");
                    }
                    else
                    {
                        //download merge geojson
                        var httpClient = new RestClient(mergeRequest.GitHubRawURL);
                        httpClient.Timeout = -1;
                        var httpRequest = new RestRequest(Method.GET);
                        IRestResponse githubResponse = httpClient.Execute(httpRequest);
                        string mergeGeoJSON = githubResponse.Content;
                        byte[] mergeGeoJSONBytes = System.Text.Encoding.UTF8.GetBytes(mergeGeoJSON);
                        string base64MergeGeoJSONSystem = Convert.ToBase64String(mergeGeoJSONBytes);
                        //send changes to Github for feature
                        GithubContent updateRequest = new GithubContent
                        {
                            message = "Merge from merge request:" + mergeRequest.Id,
                            content = base64MergeGeoJSONSystem
                        };

                        var gitHubHttpClient = new RestClient(_config["GitHubEndpoint"] + "/api/repo/" + mergeRequest.FeatureName + "/main");
                        gitHubHttpClient.Timeout = -1;
                        var githubUpdateRequest = new RestRequest(Method.PUT);
                        githubUpdateRequest.AddHeader("Content-Type", "application/json");
                        githubUpdateRequest.AddJsonBody(updateRequest);
                        IRestResponse githubUpdateResponse = gitHubHttpClient.Execute(githubUpdateRequest);
                        if (githubUpdateResponse.IsSuccessful == false)
                        {
                            return new BadRequestObjectResult("Unable to update feature.");
                        }
                        dynamic gitHubUpdateResponseData = JsonConvert.DeserializeObject(githubUpdateResponse.Content);
                        if (gitHubUpdateResponseData.errors != null)
                        {
                            return new BadRequestObjectResult(gitHubUpdateResponseData.errors);
                        }

                        //update last modified date
              
                        ItemResponse<Feature> featureSearch = await _container.ReadItemAsync<Feature>(mergeRequest.FeatureName, new PartitionKey("feature"))
                            .ConfigureAwait(false);
                        Feature featureToUpdate = featureSearch.Resource;
                        if (featureToUpdate == null)
                        {
                            return new NotFoundObjectResult("No feature found with that name");
                        }
                        featureToUpdate.LastModifiedDate = DateTime.Now;
                        //cache bust
                        featureToUpdate.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + featureToUpdate.Id + "/" + gitHubUpdateResponseData.commit.sha + "/data.geojson";
                        var replaceItemResponse = await _container.ReplaceItemAsync<Feature>(featureToUpdate, featureToUpdate.Id, new PartitionKey("feature"));

                        mergeRequest.LastModifiedDate = DateTime.Now;
                        mergeRequest.Status = "Approved";
                        //save changes to merge request
                        var replaceMergeRequestResponse = await _container.ReplaceItemAsync<MergeRequest>(mergeRequest, mergeRequest.Id, new PartitionKey("merge"));
                        return new OkObjectResult(replaceMergeRequestResponse.Resource);
                    }
                }
                if (mergeRequestAction.Action == "deny")
                {
                    ItemResponse<MergeRequest> mergeRequestSearch = await _container.ReadItemAsync<MergeRequest>(mergeRequestAction.MergeId, new PartitionKey("merge"))
                    .ConfigureAwait(false);
                    MergeRequest mergeRequest = mergeRequestSearch.Resource;
                    if (mergeRequest == null)
                    {
                        return new NotFoundObjectResult("No merge request found with that id");
                    }
                    else
                    {
                        mergeRequest.LastModifiedDate = DateTime.Now;
                        mergeRequest.Status = "Denied";
                        //save changes to merge request
                        var replaceMergeRequestResponse = await _container.ReplaceItemAsync<MergeRequest>(mergeRequest, mergeRequest.Id, new PartitionKey("merge"));
                        return new OkObjectResult(replaceMergeRequestResponse.Resource);
                    }
                }
                return new BadRequestObjectResult("Invalid request");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new BadRequestObjectResult(ex.Message);
            }
        }
    }
}
