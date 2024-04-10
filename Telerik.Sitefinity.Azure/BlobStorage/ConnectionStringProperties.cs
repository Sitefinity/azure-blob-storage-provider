using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Telerik.Sitefinity.Azure.BlobStorage
{
    internal class ConnectionStringProperties
    {
        public string AccountName { get; set; }

        public string AccountKey { get; set; }

        public bool DefaultEndpointsProtocol { get; set; }

        public ConnectionStringProperties(string connectionString, string sas)
        {
            var settings = connectionString.Split(';');
            foreach (var setting in settings)
            {
                var settingsPair = setting.Split(new char[] { '=' }, 2, StringSplitOptions.None);
                string value = settingsPair[1];

                switch (settingsPair[0])
                {
                    case "AccountName":
                        this.AccountName = value;
                        break;
                    case "AccountKey":
                        this.AccountKey = value;
                        break;
                    case "DefaultEndpointsProtocol":
                        this.DefaultEndpointsProtocol = value.ToLowerInvariant() == "https";
                        break;
                    case "BlobEndpoint":
                        var uri = new Uri(value);
                        this.DefaultEndpointsProtocol = uri.Scheme == "https";
                        this.AccountName = uri.Host.Substring(0, uri.Host.IndexOf('.'));
                        break;
                    case "SharedAccessSignature":
                        if (sas == null)
                            this.AccountKey = string.Format("?sig={0}", HttpUtility.UrlEncode(value));
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(sas))
                this.AccountKey = sas;
        }
    }
}
