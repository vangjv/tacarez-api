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
using System.Net;
using tacarez_api.Models;

namespace tacarez_api
{
    public class CreateFeature
    {
        private static readonly string EndpointUri = "https://tacarez.documents.azure.com:443";
        private static readonly string PrimaryKey = "8zC8Li4QKFZYkyOrgbZClsd7g2mOrnrw46Hc2XipY5uAFypiaVuW5KMCFsYxeVASKoEP56tD001aNlVQ6ickFg==";
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "tacarez";
        private string containerId = "features";

        [FunctionName("CreateFeature")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            NewFeatureRequest featureRequest = JsonConvert.DeserializeObject<NewFeatureRequest>(requestBody);
            NewFeature feature = featureRequest.feature;

            try
            {
                //await CreateNewFeature(feature);
                // Create a new instance of the Cosmos Client
                this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "CosmosDBDotnetQuickstart" });
                this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                Console.WriteLine("Created Database: {0}\n", this.database.Id);

                this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/type");
                Console.WriteLine("Created Container: {0}\n", this.container.Id);


                try
                {
                    // Read the item to see if it exists.  
                    ItemResponse<Feature> featureResponse = await this.container.ReadItemAsync<Feature>(feature.Id, new PartitionKey("type"));
                    Console.WriteLine("Item in database with id: {0} already exists\n", featureResponse.Resource.Id);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Feature newFeature = feature.toFeature();
                    // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                    ItemResponse<Feature> featureResponse = await this.container.CreateItemAsync<Feature>(newFeature, new PartitionKey(newFeature.Type));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", featureResponse.Resource.Id, featureResponse.RequestCharge);
                }
            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
            }

            return new OkObjectResult("success");
        }

    }
}
