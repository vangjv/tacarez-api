using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace tacarez_api.Utility
{
    public static class CosmosUtility
    {

        public static async Task<bool> DoesFeatureExist(string featureName, Container _container)
        {
            try
            {
                ItemResponse<Feature> featureResponse = await _container.ReadItemAsync<Feature>(featureName, new PartitionKey("feature"));
                if (featureResponse.Resource != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static async Task<bool> DoesRevisionExist(string featureName, string revisionName, Container _container)
        {
            try
            {
                ItemResponse<Revision> featureResponse = await _container.ReadItemAsync<Revision>(featureName + revisionName, new PartitionKey("revision"));
                if (featureResponse.Resource != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
