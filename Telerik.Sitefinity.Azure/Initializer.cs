using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Telerik.Microsoft.Practices.Unity;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Azure.BlobStorage;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Modules.Libraries;
using Telerik.Sitefinity.Modules.Libraries.Configuration;

namespace Telerik.Sitefinity.Azure
{
    /// <summary>
    /// Registers Azure blob storage provider.
    /// </summary>
    public static class Initializer
    {
        /// <summary>
        /// Attach to ObjectFactory_RegisteringIoCTypes event.
        /// </summary>
        public static void Initialize()
        {
            ObjectFactory.RegisteringIoCTypes += ObjectFactory_RegisteringIoCTypes;
        }

        private static void ObjectFactory_RegisteringIoCTypes(object sender, EventArgs e)
        {
            ObjectFactory.Container.RegisterType(typeof(IBlobStorageConfiguration), typeof(AzureBlobStorageConfiguration), typeof(AzureBlobStorageConfiguration).Name);
        }
    }

    /// <summary>
    /// Register Azure blob strorage providers and types
    /// </summary>
    class AzureBlobStorageConfiguration : IBlobStorageConfiguration
    {
        public IEnumerable<BlobStorageTypeConfigElement> GetProviderTypeConfigElements(ConfigElement parent)
        {
            var providerTypes = new List<BlobStorageTypeConfigElement>();

            providerTypes.Add(new BlobStorageTypeConfigElement(parent)
            {
                Name = "Azure",
                ProviderType = typeof(AzureBlobStorageProvider),
                Title = "WindowsAzure",
                Description = "BlobStorageAzureTypeDescription",
                SettingsViewTypeOrPath = typeof(AzureBlobSettingsView).FullName,
                Parameters = new NameValueCollection()
                    {
                        { AzureBlobStorageProvider.ConnectionStringKey, "#sf_Secret" },
                        { AzureBlobStorageProvider.ContainerNameKey, string.Empty },
                        { AzureBlobStorageProvider.PublicHostKey, string.Empty },
                        { AzureBlobStorageProvider.SharedAccessSignatureKey, string.Empty },
                        { AzureBlobStorageProvider.ParallelOperationThreadCountKey, string.Empty }
                   }
            });

            return providerTypes;
        }

        public IEnumerable<DataProviderSettings> GetProviderConfigElements(ConfigElement parent)
        {
            return new List<DataProviderSettings>();
        }
    }
}
