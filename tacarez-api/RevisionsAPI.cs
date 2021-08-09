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

        [FunctionName("CreateRevision")]
        public async Task<IActionResult> CreateRevision(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "revisions")] HttpRequest req,
            ILogger log)
        {
            IActionResult returnValue = null;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                NewRevisionRequest revisionRequest = JsonConvert.DeserializeObject<NewRevisionRequest>(requestBody);
                revisionRequest.FeatureName = revisionRequest.FeatureName.Replace(" ", "-");
                revisionRequest.RevisionName = revisionRequest.RevisionName.Replace(" ", "-");
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
                revision.CreatedDate = DateTime.Now;
                revision.LastModifiedDate = DateTime.Now;
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

        [FunctionName("GetRevisionsByUser")]
        public async Task<IActionResult> GetRevisionsByUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "revisions/user/{guid}")] HttpRequest req, string guid)
        {
            IActionResult returnValue = null;
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.type = 'revision' AND c.Owner.GUID = @guid")
                .WithParameter("@guid", guid);
                FeedIterator<Revision> queryResultSetIterator = _container.GetItemQueryIterator<Revision>(queryDefinition);
                List<Revision> revisions = new List<Revision>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<Revision> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    if (currentResultSet.Count > 0)
                    {
                        foreach (Revision revision in currentResultSet)
                        {
                            revisions.Add(revision);
                            Console.WriteLine("\tRead {0}\n", revision);
                        }
                    }
                }
                if (revisions.Count > 0)
                {
                    returnValue = new OkObjectResult(revisions);
                }
                else
                {
                    returnValue = new NotFoundObjectResult("No revisions found for that user");
                }
            }
            catch (Exception ex)
            {
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            return returnValue;
        }

        [FunctionName("GetByRevisionByName")]
        public async Task<IActionResult> GetByFeatureName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "revisions/feature/{featureName}/{revisionName}")] HttpRequest req, string featureName,
            string revisionName)
        {
            try
            {
                ItemResponse<Revision> revisionSearch = await _container.ReadItemAsync<Revision>(featureName + revisionName, new PartitionKey("revision"))
                .ConfigureAwait(false);

                Revision revision = revisionSearch.Resource;
                if (revision == null)
                {
                    return new NotFoundObjectResult("No feature found with that name");
                }
                else
                {
                    return new OkObjectResult(revision);
                }
            }
            catch (Exception ex)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
