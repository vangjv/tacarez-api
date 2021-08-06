using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tacarez_api;

[assembly: FunctionsStartup(typeof(Startup))]
namespace tacarez_api
{

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            string cosmosDbConnString = System.Environment.GetEnvironmentVariable("CosmosDbConnString", EnvironmentVariableTarget.Process);
            builder.Services.AddSingleton((s) =>
            {
                CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(cosmosDbConnString);

                return cosmosClientBuilder.WithConnectionModeDirect()
                    .WithBulkExecution(true)
                    .Build();
            });
        }
    }
}
