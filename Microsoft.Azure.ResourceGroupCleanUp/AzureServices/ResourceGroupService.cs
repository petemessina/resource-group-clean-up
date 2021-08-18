using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using AzureConfiguration = Microsoft.Azure.Management.Fluent.Azure;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.Azure.ResourceGroupCleanUp.AzureServices
{
    /// <summary>
    /// Resource group service for processing actions against the resource group api in Azure.
    /// </summary>
    public class ResourceGroupService
    {
        /// <summary>
        /// Client used to connect to Azure
        /// </summary>
        public IAzure AzureClient { get; }

        /// <summary>
        /// Resource group service for processing actions against the resource group api in Azure.
        /// </summary>
        /// <param name="azureClient">Client used to connect to Azure</param>
        public ResourceGroupService(IAzure azureClient)
        {
            AzureClient = azureClient;
        }

        /// <summary>
        /// Resource group service for processing actions against the resource group api in Azure.
        /// </summary>
        /// <param name="credentials">Credentials used to connect to Azure</param>
        public ResourceGroupService(AzureCredentials credentials)
        {
            AzureClient = AzureConfiguration.Configure()
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .Authenticate(credentials)
                    .WithDefaultSubscription();
        }

        /// <summary>
        /// Search for resource groups
        /// </summary>
        /// <param name="predicate">Search value used to filter down resource groups</param>
        /// <returns></returns>
        public async Task<IEnumerable<IResourceGroup>> ListResourceGroups(Func<IResourceGroup, bool> predicate)
            => (await AzureClient.ResourceGroups.ListAsync())
                .ToList()
                .Where(predicate);


        /// <summary>
        /// Asynchronously delete a resource group
        /// </summary>
        /// <param name="name">Name used to delete the resource group</param>
        /// <returns></returns>
        public Task DeleteResourceGroup(string name)
            => AzureClient.ResourceGroups.DeleteByNameAsync(name);
    }
}
