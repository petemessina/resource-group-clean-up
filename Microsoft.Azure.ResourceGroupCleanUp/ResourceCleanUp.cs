using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.ResourceGroupCleanUp.AzureServices;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.ResourceGroupCleanUp
{
    public class ResourceInformation
    {
        public string PartitionKey { get; set; }
        
        public string RowKey { get; set; }
        
        public string Name { get; set; }

        public DateTime RemovedOn { get; set; }
    }

    /// <summary>
    /// Timer function that runs to check for old resource groups
    /// </summary>
    public static class ResourceCleanUp
    {
        // Keeps track of resource groups that are being deleted so that the process can run asynchronously
        private static List<string> _activelyDeletingResourceGroups = new List<string>();

        /// <summary>
        /// Entry point for the main timer trigger
        /// </summary>
        /// <param name="timerInfo">Configured expression used to define the timer trigger execution</param>
        /// <param name="log">Log parameter</param>
        /// <returns></returns>
        [FunctionName("ResourceCleanUp")]
        public static async Task Run(
            [TimerTrigger("%TimerExpression%")]TimerInfo timerInfo,
            [Table("RemovedResourceGroups", Connection = "StorageAccountConnectionString")] ICollector<ResourceInformation> tableBinding,
            ILogger logger
        ) {
            string clientId = Environment.GetEnvironmentVariable("ClientId");
            string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            string tenantId = Environment.GetEnvironmentVariable("TenantId");
            string defaultSubscriptionId = Environment.GetEnvironmentVariable("DefaultSubscriptionId");
            AzureCredentials azureCredentials = SdkContext
                .AzureCredentialsFactory
                .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud)
                .WithDefaultSubscription(defaultSubscriptionId);

            await RemoveGroups(azureCredentials, tableBinding, logger);
        }

        /// <summary>
        /// Searches for resource groups with an expiration date and that its less than today.
        /// </summary>
        /// <param name="azureCredentials">Credentials used to log into Azure</param>
        /// <returns></returns>
        private static async Task RemoveGroups(
            AzureCredentials azureCredentials,
            ICollector<ResourceInformation> tableBinding,
            ILogger logger
        ) {
            string expirationDateTagName = Environment.GetEnvironmentVariable("ExpirationDateTagName");
            var resourceGroupService = new ResourceGroupService(azureCredentials);
            var expirationDate = DateTime.Now.AddDays(-1);
            IEnumerable<IResourceGroup> resourceGroups = await resourceGroupService
                .ListResourceGroups(r => r.Tags.ContainsKey(expirationDateTagName) && DateTime.Parse(r.Tags[expirationDateTagName]) < expirationDate);

            foreach(var resourceGroup in resourceGroups)
            {
                if (!_activelyDeletingResourceGroups.Contains(resourceGroup.Name))
                {
                    var resourceInformation = new ResourceInformation()
                    {
                        PartitionKey = resourceGroup.Key,
                        RowKey = Guid.NewGuid().ToString(),
                        Name = resourceGroup.Name,
                        RemovedOn = DateTime.Now
                    };

                    //I left off the await here because functions have a limited run time and we can execute this asynchronously.
                    resourceGroupService.DeleteResourceGroup(resourceInformation.Name);
                    _activelyDeletingResourceGroups.Add(resourceInformation.Name);
                    tableBinding.Add(resourceInformation);
                    logger.LogInformation("{Name} was deleted on {RemovedOn}", resourceInformation.Name, resourceInformation.RemovedOn);
                }
            }

            _activelyDeletingResourceGroups.RemoveAll(a => !resourceGroups.Any(r => r.Name == a));
        }
    }
}
