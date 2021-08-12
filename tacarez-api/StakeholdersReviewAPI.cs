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

namespace tacarez_api
{
    public class StakeholderReviewAPI
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public StakeholderReviewAPI(ILogger<Feature> logger, IConfiguration config, CosmosClient cosmosClient)
        {
            _logger = logger;
            _config = config;
            _cosmosClient = cosmosClient;
            _database = _cosmosClient.GetDatabase(_config["Database"]);
            _container = _database.GetContainer(_config["Container"]);
        }

        [FunctionName("InitiateStakeholderReview")]
        public async Task<IActionResult> InitiateStakeholderReview(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stakeholderreview")] HttpRequest req)
        {
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            StakeHolderReviewRequest stakeholderReviewRequest = JsonConvert.DeserializeObject<StakeHolderReviewRequest>(requestBody);

            StakeholderRequestUtility stakeholderUtil = new StakeholderRequestUtility(_config, _cosmosClient);
            try
            {
                MergeRequest updateMergeRequest = await stakeholderUtil.CompleteEnvelopeRequest(stakeholderReviewRequest.MergeId, stakeholderReviewRequest.SenderName,
                stakeholderReviewRequest.MessageFromSender, stakeholderReviewRequest.Stakeholders);
                return new OkObjectResult(updateMergeRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.InnerException.Message);
                return new BadRequestObjectResult(e.Message);
            }                       
        }

    }
}
