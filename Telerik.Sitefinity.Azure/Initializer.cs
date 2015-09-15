using System;
using System.Linq;
using Telerik.Sitefinity.Azure.BlobStorage;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Modules.Libraries;
using Telerik.Sitefinity.Modules.Libraries.Configuration;
using Telerik.Sitefinity.Services;

namespace Telerik.Sitefinity.Azure
{
    public static class Initializer
    {
        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public static void Initialize()
        {
            SystemManager.ModulesInitialized += SystemManager_ModulesInitialized;
        }

        static void SystemManager_ModulesInitialized(object sender, Services.Events.SystemInitializationEventArgs e)
        {
            if (e.InstallContext.UpgradeInfo != null && e.InstallContext.UpgradeInfo.UpgradeFromBuild < 5900)
            {
                UpgradeTo5900();
            }
        }

        static void UpgradeTo5900()
        {
            Type oldProviderType = typeof(Telerik.Sitefinity.Modules.Libraries.BlobStorage.AzureBlobStorageProvider);
            using (new ElevatedConfigModeRegion())
            {
                var librariesConfig = Config.Get<LibrariesConfig>();
                var providersConfig = librariesConfig.BlobStorage.Providers;

                var azureProvider = providersConfig.Values.Where(p => p.ProviderType == oldProviderType).FirstOrDefault();
                if (azureProvider == null)
                {
                    azureProvider = new DataProviderSettings(providersConfig) { Name = "Azure blob storage provider" };
                }

                azureProvider.ProviderType = typeof(AzureBlobStorageProvider);

                var azureBlobStorageType = librariesConfig.BlobStorage.BlobStorageTypes.Values.Where(b => b.ProviderType == oldProviderType).FirstOrDefault();
                if (azureBlobStorageType == null)
                {
                    azureBlobStorageType = new BlobStorageTypeConfigElement(providersConfig)
                    {
                        Name = "Azure",
                        Title = "WindowsAzure",
                        Description = "BlobStorageAzureTypeDescription",
                        ResourceClassId = typeof(LibrariesResources).Name
                    };
                }

                azureBlobStorageType.ProviderType = typeof(AzureBlobStorageProvider);
                azureBlobStorageType.SettingsViewTypeOrPath = typeof(AzureBlobSettingsView).FullName;

                ConfigManager.GetManager().SaveSection(librariesConfig);
            }
        }
    }
}
