using System;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI.WebControls;
using Telerik.Sitefinity.Web.UI;
using Telerik.Sitefinity.Web.UI.Fields;
using System.Web;
using System.Web.UI;
using Telerik.Sitefinity.Modules.Libraries.Web.UI.BlobStorage;

namespace Telerik.Sitefinity.Azure.BlobStorage
{
    /// <summary>
    /// Basic settings wizard view for configuring the connection to Azure blob storage
    /// </summary>
    /// <remarks>
    /// Expected provider parameters connectionString, containerName
    /// </remarks>
    public class AzureBlobSettingsView : AjaxDialogBase, IBlobSettingsView
    {

        /// <summary>
        /// Gets or sets the path of the external template to be used by the control.
        /// </summary>
        /// <value></value>
        public override string LayoutTemplatePath
        {
            get
            {
                return layoutTemplatePath;
            }
            set
            {
                return;
            }
        }

        protected override string LayoutTemplateName
        {
            get { return null; }
        }


        /// <summary>
        /// Gets the name of the account UI control.
        /// </summary>
        /// <value>The name of the account.</value>
        public TextField AccountName
        {
            get
            {
                return this.Container.GetControl<TextField>("accountName", true);
            }
        }

        /// <summary>
        /// Gets the account key UI control.
        /// </summary>
        /// <value>The account key.</value>
        public TextField AccountKey
        {
            get
            {
                return this.Container.GetControl<TextField>("accountKey", true);
            }
        }

        /// <summary>
        /// Gets the name of the container UI control.
        /// </summary>
        /// <value>The name of the container.</value>
        public TextField ContainerName
        {
            get
            {
                return this.Container.GetControl<TextField>("containerName", true);
            }
        }

        /// <summary>
        /// Gets the public host UI control.
        /// </summary>
        public TextField PublicHost
        {
            get
            {
                return this.Container.GetControl<TextField>("publicHost", true);
            }
        }

        /// <summary>
        /// Gets the default endpoints protocol UI control.
        /// </summary>
        /// <value>The default endpoints protocol.</value>
        public CheckBox DefaultEndpointsProtocol
        {
            get
            {
                return this.Container.GetControl<CheckBox>("defaultEndpointsProtocol", true);
            }
        }

        public CheckBox UseDevStorage
        {
            get
            {
                return this.Container.GetControl<CheckBox>("useDevStorage", true);
            }
        }

        /// <summary>
        /// Initializes the settings view controls.
        /// </summary>
        /// <param name="container"></param>
        protected override void InitializeControls(GenericContainer container)
        {
            if (!this.Page.IsPostBack)
            {
                if (settings[AzureBlobStorageProvider.ConnectionStringKey] != null)
                {
                    PopulateUIFromConnectionString(
                        settings[AzureBlobStorageProvider.ConnectionStringKey],
                        settings[AzureBlobStorageProvider.SharedAccessSignatureKey]);
                }

                if (settings[AzureBlobStorageProvider.ContainerNameKey] != null)
                    this.ContainerName.Value = settings[AzureBlobStorageProvider.ContainerNameKey];

                if (settings[AzureBlobStorageProvider.PublicHostKey] != null)
                    this.PublicHost.Value = settings[AzureBlobStorageProvider.PublicHostKey];
            }
        }

        protected override HtmlTextWriterTag TagKey
        {
            get
            {
                //we use div wrapper tag to make easier common styling
                return HtmlTextWriterTag.Div;
            }
        }

        /// <summary>
        /// Gets the azure connection string from the UI Controls
        /// </summary>
        /// <returns></returns>
        private string GetAzureConnectionStringFromUI()
        {
            if (UseDevStorage.Checked)
            {
                return devStoreConnectionString;
            }

            string protocol = DefaultEndpointsProtocol.Checked ? "https" : "http";
            string accountName = AccountName.Value.ToString();
            string key = AccountKey.Value.ToString();

            var signature = GetSignature(key);
            if (signature != null)
            {
                return string.Format("BlobEndpoint={0}://{1}.blob.core.windows.net/;SharedAccessSignature={2}",
                    protocol, accountName, signature);
            }

            return string.Format("DefaultEndpointsProtocol={0};AccountName={1};AccountKey={2}",
                protocol, accountName, key);    
        }

        private string GetSignature(string key)
        {
            var sasMatch = sasRegex.Match(key);
            if (!sasMatch.Success)
                return null;

            return HttpUtility.UrlDecode(sasMatch.Groups["sig"].Value);
        }

        /// <summary>
        /// Populates the UI controls from connection string.
        /// </summary>
        /// <param name="azureConnectionString">The azure connection string.</param>
        private void PopulateUIFromConnectionString(string azureConnectionString, string sas = null)
        {
            if (azureConnectionString == devStoreConnectionString)
            {
                this.UseDevStorage.Checked = true;
                return;
            }
            
            var settings = azureConnectionString.Split(';');
            foreach (var setting in settings)
            {
                var settingsPair = setting.Split(new char[] {'='}, 2, StringSplitOptions.None);
                string value = settingsPair[1];

                switch (settingsPair[0])
                {
                    case "AccountName":
                        this.AccountName.Value = value;
                        break;
                    case "AccountKey":
                        this.AccountKey.Value = value;
                        break;
                    case "DefaultEndpointsProtocol":
                        this.DefaultEndpointsProtocol.Checked = value.ToLowerInvariant() == "https";
                        break;
                    case "BlobEndpoint":
                        var uri = new Uri(value);
                        this.DefaultEndpointsProtocol.Checked = uri.Scheme == "https";
                        this.AccountName.Value = uri.Host.Substring(0, uri.Host.IndexOf('.'));
                        break;
                    case "SharedAccessSignature":
                        if (sas == null)
                            this.AccountKey.Value = string.Format("?sig={0}", HttpUtility.UrlEncode(value));
                        break;
                }
            }

            if (sas != null)
                this.AccountKey.Value = sas;
        }

        /// <summary>
        /// Gets or sets the blob storage settings.
        /// </summary>
        /// <value>The settings.</value>
        /// <remarks>
        /// Expected provider parameters connectionString, containerName
        /// </remarks>
        public NameValueCollection Settings
        {
            get
            {
                var key = this.AccountKey.Value.ToString();
                var sas = GetSignature(key) == null ? null : key;

                return new NameValueCollection()
                {
                    {
                        AzureBlobStorageProvider.ConnectionStringKey,
                        GetAzureConnectionStringFromUI() 
                    },
                    {
                        AzureBlobStorageProvider.ContainerNameKey,
                        this.ContainerName.Value.ToString()
                    },
                    {
                        AzureBlobStorageProvider.SharedAccessSignatureKey,
                        sas
                    },
                    {
                        AzureBlobStorageProvider.PublicHostKey,
                        this.PublicHost.Value.ToString()
                    }
                };
            }
            set
            {
                settings = value;
            }
        }

        private NameValueCollection settings;        

        private const string devStoreConnectionString = "UseDevelopmentStorage=true";
        private static readonly Regex sasRegex = new Regex(@"sig=(?<sig>[a-zA-Z0-9\+/%]+[=]{0,2})", RegexOptions.Compiled);

        public static readonly string layoutTemplatePath = ControlUtilities.ToVppPath("Telerik.Sitefinity.Resources.Templates.Backend.Configuration.Basic.AzureBlobSettingsView.ascx");
        //public static readonly string layoutTemplatePath = "~/AzureBlobSettingsView.ascx";        
    }
}
