using System;
using System.Linq;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Azure.BlobStorage;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Modules.Libraries;
using Telerik.Sitefinity.Modules.Libraries.Configuration;

namespace Telerik.Sitefinity.Azure
{
    /// <summary>
    /// Defines the initialization logic for the assembly
    /// </summary>
    public static class Initializer
    {
        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public static void Initialize()
        {
            Bootstrapper.Initialized += Bootstrapper_Initialized;
        }

        private static void Bootstrapper_Initialized(object sender, Data.ExecutedEventArgs e)
        {
            using (new ElevatedConfigModeRegion())
            {
                var librariesConfig = Config.Get<LibrariesConfig>();
                var azureBlobStorageType = librariesConfig.BlobStorage.BlobStorageTypes.Values.Where(b => b.ProviderType == typeof(AzureBlobStorageProvider)).FirstOrDefault();
                if (azureBlobStorageType == null)
                {
                    azureBlobStorageType = CreateAzureBlobStorageProviderType(librariesConfig);
                    ConfigManager.GetManager().SaveSection(librariesConfig);
                }
            }
        }

        private static BlobStorageTypeConfigElement CreateAzureBlobStorageProviderType(LibrariesConfig librariesConfig)
        {
            var blobStorageTypes = librariesConfig.BlobStorage.BlobStorageTypes;
            var azureBlobStorageType = new BlobStorageTypeConfigElement(blobStorageTypes)
            {
                Name = "Azure",
                ProviderType = typeof(AzureBlobStorageProvider),
                Title = "WindowsAzure",
                Description = "BlobStorageAzureTypeDescription",
                SettingsViewTypeOrPath = typeof(AzureBlobSettingsView).FullName,
                ResourceClassId = typeof(LibrariesResources).Name
            };

            blobStorageTypes.Add(azureBlobStorageType);
            return azureBlobStorageType;
        }
    }
}
