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
using tacarez_api.Models;

namespace tacarez_api
{
    public class ContributorsAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public ContributorsAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("UpdateFeatureContributors")]
        public async Task<IActionResult> UpdateFeatureContributors(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "contributors/feature/{featureName}")] HttpRequest req,
           string featureName)
        {
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<User> contributors = JsonConvert.DeserializeObject<List<User>>(requestBody);
            //get feature
            ItemResponse<Feature> response = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"))
              .ConfigureAwait(false);
            Feature featureToUpdate = response.Resource;
            if (featureToUpdate == null)
            {
                return new NotFoundObjectResult("No feature found with that name");
            }
            //replace stakeholders
            featureToUpdate.Contributors = contributors;
            var replaceItemResponse = await _container.ReplaceItemAsync<Feature>(featureToUpdate, featureToUpdate.Id, new PartitionKey("feature"));
            return new OkObjectResult(replaceItemResponse.Resource);
        }

        [FunctionName("UpdateRevisionContributors")]
        public async Task<IActionResult> UpdateRevisionContributors(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "contributors/revision/{featureName}/{revisionName}")] HttpRequest req,
           string featureName, string revisionName)
        {
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            if (revisionName == null)
            {
                return new BadRequestObjectResult("Please include the revision name.");
            }
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<User> contributors = JsonConvert.DeserializeObject<List<User>>(requestBody);
            //get revision
            ItemResponse<Revision> response = await _container.ReadItemAsync<Revision>(featureName + revisionName, new PartitionKey("revision"))
              .ConfigureAwait(false);
            Revision revisionToUpdate = response.Resource;
            if (revisionToUpdate == null)
            {
                return new NotFoundObjectResult("No feature found with that name");
            }
            //replace stakeholders
            revisionToUpdate.Contributors = contributors;
            var replaceItemResponse = await _container.ReplaceItemAsync<Revision>(revisionToUpdate, revisionToUpdate.Id, new PartitionKey("revision"));
            return new OkObjectResult(replaceItemResponse.Resource);
        }

        [FunctionName("UpdateMergeRequestContributors")]
        public async Task<IActionResult> UpdateMergeRequestContributors(
         [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "contributors/mergerequest/{featureName}/{mergeId}")] HttpRequest req,
         string featureName, string mergeId)
        {
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            if (mergeId == null)
            {
                return new BadRequestObjectResult("Please include the revision name.");
            }
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<User> contributors = JsonConvert.DeserializeObject<List<User>>(requestBody);
            ItemResponse<MergeRequest> response = await _container.ReadItemAsync<MergeRequest>(mergeId, new PartitionKey("merge"))
              .ConfigureAwait(false);
            MergeRequest mergeRequestToUpdate = response.Resource;
            if (mergeRequestToUpdate == null)
            {
                return new NotFoundObjectResult("No feature found with that name");
            }
            mergeRequestToUpdate.Contributors = contributors;
            var replaceItemResponse = await _container.ReplaceItemAsync<MergeRequest>(mergeRequestToUpdate, mergeRequestToUpdate.Id, new PartitionKey("merge"));
            return new OkObjectResult(replaceItemResponse.Resource);
        }

    }
}
