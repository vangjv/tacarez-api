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

namespace tacarez_api
{
    public class RevisionsAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;
        public RevisionsAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("RevisionsAPI")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "revisions")] HttpRequest req,
            ILogger log)
        {
            IActionResult returnValue = null;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                NewRevisionRequest revisionRequest = JsonConvert.DeserializeObject<NewRevisionRequest>(requestBody);
                if (await CosmosUtility.DoesFeatureExist(revisionRequest.FeatureName, _container) == false)
                {
                    return new BadRequestObjectResult("No feature found with that name");
                }
                if (await CosmosUtility.DoesRevisionExist(revisionRequest.FeatureName, revisionRequest.RevisionName, _container) == true)
                {
                    return new BadRequestObjectResult("A revision with that name already exist");
                }
                NewBranchRequest newBranchRequest = new NewBranchRequest
                {
                    RepoName = revisionRequest.FeatureName,
                    BranchName = revisionRequest.RevisionName
                };
                var client = new RestClient(_config["GitHubEndpoint"] + "/api/branch");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(newBranchRequest);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                if (response.IsSuccessful == false)
                {
                    return new BadRequestObjectResult("Unable to create revision.");
                }
                dynamic gitHubResponse = JsonConvert.DeserializeObject(response.Content);
                if (gitHubResponse.errors != null)
                {
                    return new BadRequestObjectResult(gitHubResponse.errors);
                }
                Revision revision = new Revision();
                revision.Id = revisionRequest.FeatureName + revisionRequest.RevisionName;
                revision.Type = "revision";
                revision.FeatureName = revisionRequest.FeatureName;
                revision.RevisionName = revisionRequest.RevisionName;
                revision.Description = revisionRequest.Description;
                revision.GitHubRawURL = "https://raw.githubusercontent.com/dshackathon/" + revisionRequest.FeatureName + "/" + revisionRequest.RevisionName + "/data.geojson";
                revision.Owner = revisionRequest.Owner;
                ItemResponse<Revision> item = await _container.CreateItemAsync(revision, new PartitionKey(revision.Type));
                _logger.LogInformation("Item inserted");
                _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                returnValue = new OkObjectResult(item.Resource);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");

            }
            return returnValue;
        }
    }
}