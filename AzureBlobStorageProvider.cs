using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Telerik.Sitefinity.BlobStorage;
using Telerik.Sitefinity.Modules.Libraries.BlobStorage;
using Telerik.Sitefinity.Web;
using Storage = Microsoft.WindowsAzure.Storage;

namespace Telerik.Sitefinity.Azure.BlobStorage
{
    /// <summary>
    /// Implements a logic for persisting BLOB data in Azure Blob Storage
    /// </summary>
    public class AzureBlobStorageProvider : CloudBlobStorageProvider
    {
        #region Properties

        public const string ConnectionStringKey = "connectionString";
        public const string ContainerNameKey = "containerName";
        public const string PublicHostKey = "publicHost";
        public const string SharedAccessSignatureKey = "SAS";
        public const string ParallelOperationThreadCountKey = "parallelOperationThreadCount";

        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the storage.
        /// </summary>
        /// <param name="config">The config.</param>
        protected override void InitializeStorage(NameValueCollection config)
        {
            this.client = this.CreateBlobClient(config);

            this.containerName = config[ContainerNameKey];
            if (String.IsNullOrEmpty(containerName))
                containerName = this.Name.ToLower();
            // TODO: Azure - container name should be validated 

            config.Remove(ContainerNameKey);

            this.rootUrl = UrlPath.GetAbsoluteHost(config[PublicHostKey])
                ?? this.client.BaseUri.ToString();
            if (this.rootUrl[this.rootUrl.Length - 1] != '/')
                this.rootUrl += "/";
        }

        protected virtual CloudBlobClient CreateBlobClient(NameValueCollection config)
        {
            this.connectionString = config[ConnectionStringKey];
            if (String.IsNullOrEmpty(this.connectionString))
                throw new ConfigurationErrorsException("'{0}' is not specified.".Arrange(ConnectionStringKey));
            config.Remove(ConnectionStringKey);
            CloudStorageAccount account = null;
            try
            {
                account = CloudStorageAccount.Parse(this.connectionString);
            }
            catch (FormatException e)
            {
                throw new BlobStorageException("Invalid blob storage credentials", e);
            }

            string sas = config[SharedAccessSignatureKey];
            if (sas != null)
            {
                config.Remove(SharedAccessSignatureKey);
                return new CloudBlobClient(account.BlobEndpoint, new StorageCredentials(sas));
            }

            var blobClient = account.CreateCloudBlobClient();

            var val = config[ParallelOperationThreadCountKey];
            if (!String.IsNullOrEmpty(val))
                blobClient.DefaultRequestOptions.ParallelOperationThreadCount = int.Parse(val);
            config.Remove(ParallelOperationThreadCountKey);

            return blobClient;
        }

        #endregion

        /// <summary>
        /// Gets the upload stream.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        public override Stream GetUploadStream(IBlobContent content)
        {
            var container = this.GetOrCreateContainer(this.containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(GetBlobName(content));
            return blob.OpenWrite();
        }

        /// <summary>
        /// Gets the download stream.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        public override Stream GetDownloadStream(IBlobContent content)
        {
            CloudBlob blob = this.GetBlob(content);
            return blob.OpenRead();
        }

        public void DownloadToStream(IBlobContent content, Stream target)
        {
            CloudBlob blob = this.GetBlob(content);
            blob.DownloadToStream(target);
        }

        /// <inheritdoc />
        public override void Delete(IBlobContentLocation content)
        {
            CloudBlob blob = this.GetBlob(content);
            try
            {
                var requestOptions = new Storage.Blob.BlobRequestOptions()
                {
                    RetryPolicy = new Storage.RetryPolicies.NoRetry() // RetryPolicies.Retry(1, TimeSpan.FromSeconds(1))
                };
                blob.DeleteIfExists(DeleteSnapshotsOption.None, null, requestOptions, null);
            }
            catch (Exception e)
            {
                throw new BlobStorageException(String.Format("Cannot delete BLOB '{0}' from library storage '{1}'. {2}: {3}.", this.GetBlobName(content), this.Name, e.Source, e.Message), e);
            }
        }

        /// <inheritdoc />
        public override bool BlobExists(IBlobContentLocation location)
        {
            CloudBlob blob = this.GetBlob(location);
            return blob.Exists();
        }

        /// <inheritdoc />
        public override void SetProperties(IBlobContentLocation content, IBlobProperties properties)
        {
            var blob = this.GetBlob(content);

            blob.Properties.ContentType = properties.ContentType;
            blob.Properties.CacheControl = properties.CacheControl;

            blob.BeginSetProperties(null, null);
        }

        /// <inheritdoc />
        public override IBlobProperties GetProperties(IBlobContentLocation location)
        {
            var blob = this.GetBlob(location);
            blob.FetchAttributes();

            return new Telerik.Sitefinity.Modules.Libraries.BlobStorage.BlobProperties
            {
                ContentType = blob.Properties.ContentType,
                CacheControl = blob.Properties.CacheControl
            };
        }

        /// <inheritdoc />
        public override string GetItemUrl(IBlobContentLocation content)
        {
            return string.Concat(this.rootUrl, this.GetBlobPath(content));
        }

        /// <inheritdoc />
        public override void Copy(IBlobContentLocation source, IBlobContentLocation destination)
        {
            var sourceBlob = this.GetBlob(source);
            var destBlob = this.GetBlob(destination);

            if (!(this.client.Credentials is StorageCredentials))
            {
                using (var uploadStream = sourceBlob.OpenRead())
                {
                    destBlob.UploadFromStream(uploadStream);
                }
            }
            else
            {
                // This is a workaround for the SAS authorization case, for which CopyFromBlob throws an exception.
                // It imposes PERFORMANCE, NETWORK and FINANCIAL costs. :(
                using (var uploadSteam = destBlob.OpenWrite())
                    sourceBlob.DownloadToStream(uploadSteam);

                this.SetProperties(destination, this.GetProperties(source));
            }
        }

        /// <inheritdoc />
        public override void Move(IBlobContentLocation source, IBlobContentLocation destination)
        {
            this.Copy(source, destination);
            var sourceBlob = this.GetBlob(source);
            sourceBlob.BeginDelete(null, null);
        }

        /// <inheritdoc />
        public override bool HasSameLocation(BlobStorageProvider other)
        {
            var otherAureProvider = other as AzureBlobStorageProvider;
            if (otherAureProvider != null)
                return this.connectionString.Equals(otherAureProvider.connectionString, StringComparison.OrdinalIgnoreCase) &&
                    this.containerName.Equals(otherAureProvider.containerName, StringComparison.OrdinalIgnoreCase) &&
                    this.rootUrl.Equals(otherAureProvider.rootUrl, StringComparison.OrdinalIgnoreCase);

            return base.HasSameLocation(other);
        }

        #region Private

        private string GetBlobPath(IBlobContentLocation content)
        {
            return String.Concat(this.containerName, "/", this.GetBlobName(content));
        }

        private CloudBlockBlob GetBlob(IBlobContentLocation blobLocation)
        {
            var container = this.client.GetContainerReference(this.containerName);
            var blob = container.GetBlockBlobReference(this.GetBlobName(blobLocation));
            return blob;
        }

        private CloudBlobContainer GetOrCreateContainer(string name)
        {
            CloudBlobContainer container = this.client.GetContainerReference(name);

            // Note: For some reason this returns false even the container did not already exists (even in the 5.0.2 this behavior is same)
            container.CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            return container;
        }

        #endregion

        #region Fields

        private string connectionString;
        private CloudBlobClient client;
        private string rootUrl;
        private string containerName;

        #endregion
    }

    /// <summary>
    /// Implements a wrapper for Azure Blob Sream to fix an issue with CloudBlob.OpenRead() not returning the whole data
    /// </summary>
    internal class AzureBlobDownloadStream : Stream
    {
        public AzureBlobDownloadStream(CloudBlobStream stream)
        {
            this.originalStream = stream;
        }

        public override bool CanRead
        {
            get { return originalStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return originalStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return originalStream.CanWrite; }
        }

        public override void Flush()
        {
            originalStream.Flush();
        }

        public override long Length
        {
            get { return originalStream.Length; }
        }

        public override long Position
        {
            get
            {
                return this.originalStream.Position;
            }
            set
            {
                this.originalStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = this.originalStream.Read(buffer, offset, count);
            if (offset == 0 && bytesRead > 0 && bytesRead < count)
            {
                var bytesLeft = count - bytesRead;
                var extendedBuffer = new byte[bytesLeft];
                var bytesLeftRead = this.originalStream.Read(extendedBuffer, 0, bytesLeft);

                if (bytesLeftRead > 0)
                {
                    Array.Copy(extendedBuffer, 0, buffer, bytesRead, bytesLeftRead);
                    bytesRead += bytesLeftRead;
                }
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.originalStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.originalStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.originalStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            originalStream.Dispose();
        }

        private readonly CloudBlobStream originalStream;
    }
}
