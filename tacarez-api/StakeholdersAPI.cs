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

namespace tacarez_api
{
    public class StakeholdersAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public StakeholdersAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("UpdateStakeholders")]
        public async Task<IActionResult> UpdateStakeholders(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "stakeholders/{featureName}")] HttpRequest req,
           string featureName)
        {
            if (featureName == null)
            {
                return new BadRequestObjectResult("Please include the feature name.");
            }
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<User> stakeholders = JsonConvert.DeserializeObject<List<User>>(requestBody);

            //get feature
            ItemResponse<Feature> response = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"))
              .ConfigureAwait(false);
            Feature featureToUpdate = response.Resource;
            if (featureToUpdate == null)
            {
                return new NotFoundObjectResult("No feature found with that name");
            }
            //replace stakeholders
            featureToUpdate.Stakeholders = stakeholders;
            var replaceItemResponse = await _container.ReplaceItemAsync<Feature>(featureToUpdate, featureToUpdate.Id, new PartitionKey("feature"));

            return new OkObjectResult(replaceItemResponse.Resource);
        }

    }
}
