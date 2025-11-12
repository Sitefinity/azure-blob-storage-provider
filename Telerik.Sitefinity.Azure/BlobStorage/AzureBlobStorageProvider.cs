using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Web;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.BlobStorage;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Libraries.Model;
using Telerik.Sitefinity.Modules.Libraries.BlobStorage;
using Telerik.Sitefinity.Modules.Libraries.BlobStorage.Dto;
using Telerik.Sitefinity.Modules.Libraries.Configuration;
using Telerik.Sitefinity.Services;
using Telerik.Sitefinity.Web;
using Telerik.Sitefinity.Web.Services.Contracts.Operations;
using Storage = Microsoft.WindowsAzure.Storage;

namespace Telerik.Sitefinity.Azure.BlobStorage
{
    /// <summary>
    /// Implements a logic for persisting BLOB data in Azure Blob Storage
    /// </summary>
    public class AzureBlobStorageProvider : CloudBlobStorageProvider
    {
        #region Properties

        /// <summary>
        /// The connection string key constant
        /// </summary>
        public const string ConnectionStringKey = "connectionString";

        /// <summary>
        /// The container name constant
        /// </summary>
        public const string ContainerNameKey = "containerName";

        /// <summary>
        /// The public host key constant
        /// </summary>
        public const string PublicHostKey = "publicHost";

        /// <summary>
        /// The shared access signature key constant
        /// </summary>
        public const string SharedAccessSignatureKey = "SAS";

        /// <summary>
        /// The parallel operations count key constant
        /// </summary>
        public const string ParallelOperationThreadCountKey = "parallelOperationThreadCount";

        /// <summary>
        /// The configuration constant for relative urls in SaaS
        /// </summary>
        public const string ЕnableRelativeUrls = "enableRelativeUrls";

        #endregion

        /// <summary>
        /// Gets the upload stream.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>The upload stream</returns>
        public override Stream GetUploadStream(IBlobContent content)
        {
            var container = this.GetOrCreateContainer(this.containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(GetBlobName(content));

            blob.Properties.ContentType = content.MimeType;

            blob.Metadata[nameof(IBlobContent.FileId)] = content.FileId.ToString();

            return blob.OpenWrite();
        }

        /// <summary>
        /// Gets the download stream.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>The download stream</returns>
        public override Stream GetDownloadStream(IBlobContent content)
        {
            CloudBlob blob = this.GetBlob(content);
            return blob.OpenRead();
        }

        /// <summary>
        /// Downloads a content to the stream
        /// </summary>
        /// <param name="content">The content</param>
        /// <param name="target">The target stream</param>
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
                if (blob.Exists())
                {
                    blob.FetchAttributes();
                    bool hasFileId = blob.Metadata.ContainsKey(nameof(IBlobContentLocation.FileId));
                    if (!hasFileId || (hasFileId && blob.Metadata[nameof(IBlobContentLocation.FileId)].Equals(content.FileId.ToString())))
                    {
                        var requestOptions = new Storage.Blob.BlobRequestOptions()
                        {
                            RetryPolicy = new Storage.RetryPolicies.NoRetry() // RetryPolicies.Retry(1, TimeSpan.FromSeconds(1))
                        };
                        blob.DeleteIfExists(DeleteSnapshotsOption.None, null, requestOptions, null);
                    }
                }
            }
            catch (Exception e)
            {
                throw new BlobStorageException(string.Format("Cannot delete BLOB '{0}' from library storage '{1}'. {2}: {3}.", this.GetBlobName(content), this.Name, e.Source, e.Message), e);
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
            if (isRelativeUrlEnabled)
            {
                return string.Concat("/", this.GetBlobPath(content));
            }

            return string.Concat(this.rootUrl, this.GetBlobPath(content));
        }

        /// <inheritdoc />
        public override void Copy(IBlobContentLocation source, IBlobContentLocation destination)
        {
            var sourceBlob = this.GetBlob(source);
            var destBlob = this.GetBlob(destination);

            this.ServerSideCopy(sourceBlob, destBlob);

            destBlob.Metadata[nameof(IBlobContentLocation.FileId)] = source.FileId.ToString();
            destBlob.SetMetadata();
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

        #region Initialization

        /// <summary>
        /// Initializes the storage.
        /// </summary>
        /// <param name="config">The config.</param>
        protected override void InitializeStorage(NameValueCollection config)
        {
            this.client = this.CreateBlobClient(config);

            var enableBlobStorageChunkUploadString = ConfigurationManager.AppSettings["sf:enableBlobStorageChunkUpload"];
            if (bool.TryParse(enableBlobStorageChunkUploadString, out bool enableBlobStorageChunkUpload) && enableBlobStorageChunkUpload)
            {
                this.enableBlobStorageChunkUpload = true;
            }

            this.containerName = config[ContainerNameKey];
            if (string.IsNullOrEmpty(this.containerName))
                this.containerName = this.Name.ToLower();

            //// TODO: Azure - container name should be validated 

            config.Remove(ContainerNameKey);

            this.rootUrl = UrlPath.GetAbsoluteHost(config[PublicHostKey])
                ?? this.client.BaseUri.ToString();
            if (this.rootUrl[this.rootUrl.Length - 1] != '/')
                this.rootUrl += "/";

            this.isRelativeUrlEnabled = bool.TryParse(config[ЕnableRelativeUrls], out _);
        }

        /// <summary>
        /// Creates a blob client
        /// </summary>
        /// <param name="config">The config to use</param>
        /// <returns>The blob client</returns>
        protected virtual CloudBlobClient CreateBlobClient(NameValueCollection config)
        {
            this.connectionString = config[ConnectionStringKey];
            if (string.IsNullOrEmpty(this.connectionString))
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
            if (!string.IsNullOrEmpty(val))
                blobClient.DefaultRequestOptions.ParallelOperationThreadCount = int.Parse(val);
            config.Remove(ParallelOperationThreadCountKey);

            return blobClient;
        }

        public override ChunkUploadResult UploadChunk(Guid chunkedFileUploadSessionId, int chunkOrdinal, int chunkSize, Stream data)
        {
            var container = this.GetOrCreateContainer(this.containerName);

            var blobReference = container.GetBlockBlobReference(GetTempChunksUploadPath(chunkedFileUploadSessionId));

            string blockId = Convert.ToBase64String(BitConverter.GetBytes(chunkOrdinal));

            try
            {
                blobReference.PutBlock(blockId, data, null);
            }
            catch (Exception ex)
            {
                return new FailedChunkUploadResult()
                {
                    ChunkedFileUploadSessionId = chunkedFileUploadSessionId,
                    Exception = ex,
                    FailedChunkOrdinal = chunkOrdinal,
                    SuccessfullyUploaded = false
                };
            }

            return new ChunkUploadResult()
            {
                SuccessfullyUploaded = true,
                ChunkedFileUploadSessionId = chunkedFileUploadSessionId,
            };
        }

        public override MediaContent CommitChunks(Guid chunkedFileUploadSessionId, MediaContent mediaContent, int numberOfChunks, long fileTotalSize, string fileName, string fileExtension)
        {
            var container = this.GetOrCreateContainer(this.containerName);

            var blobReference = container.GetBlockBlobReference(GetTempChunksUploadPath(chunkedFileUploadSessionId));

            var blockIds = new string[numberOfChunks];
            for (int i = 0; i < numberOfChunks; i++)
            {
                blockIds[i] = Convert.ToBase64String(BitConverter.GetBytes(i));
            }

            blobReference.PutBlockList(blockIds);

            base.CommitChunks(chunkedFileUploadSessionId, mediaContent, numberOfChunks, fileTotalSize, fileName, fileExtension);

            var blobName = this.GetBlobName(mediaContent);
            var finalBlobDestination = container.GetBlockBlobReference(blobName);

            this.ServerSideCopy(blobReference, finalBlobDestination, true); 

            blobReference.DeleteIfExists();

            return mediaContent;
        }

        public override void CleanUpChunks(Guid chunkedFileUploadSessionId)
        {
            var container = this.GetOrCreateContainer(this.containerName);

            var fileId = chunkedFileUploadSessionId.ToString();
            var blobReference = container.GetBlockBlobReference(fileId);

            if (blobReference.Exists())
            {
                var requestOptions = new Storage.Blob.BlobRequestOptions()
                {
                    RetryPolicy = new Storage.RetryPolicies.NoRetry() // RetryPolicies.Retry(1, TimeSpan.FromSeconds(1))
                };

                blobReference.DeleteIfExists(DeleteSnapshotsOption.None, null, requestOptions, null);
            }
        }

        public override bool SupportsChunkUpload()
        {
            return this.enableBlobStorageChunkUpload && Config.Get<LibrariesConfig>().ChunkedUpload.EnableChunkedUploads;
        }

        #endregion

        #region Private

        private void ServerSideCopy(CloudBlockBlob sourceBlob, CloudBlockBlob destBlob, bool deleteSource = false)
        {
            var start = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(1);
            var copyId = destBlob.StartCopy(sourceBlob);

            while (true)
            {
                Thread.Sleep(100);
                destBlob.FetchAttributes();

                if (DateTime.UtcNow - start > timeout)
                {
                    if (destBlob.CopyState.Status == CopyStatus.Pending)
                        destBlob.AbortCopy(copyId);

                    if (deleteSource)
                        sourceBlob.DeleteIfExists();

                    destBlob.DeleteIfExists();
                    throw new TimeoutException("Timeout for committing the blob (default 1 minute) ellapsed.");
                }

                if (destBlob.CopyState.Status == CopyStatus.Pending)
                    continue;

                if (destBlob.CopyState.Status == CopyStatus.Success)
                    break;

                if (deleteSource)
                    sourceBlob.DeleteIfExists();

                throw new ApplicationException("Failed to copy blob.");
            }
        }

        private string GetTempChunksUploadPath(Guid chunkSessionReference)
        {
            return $"temp-chunk-upload/{chunkSessionReference}";
        }

        private string GetBlobPath(IBlobContentLocation content)
        {
            return string.Concat(this.containerName, "/", this.GetBlobName(content));
        }

        private CloudBlockBlob GetBlob(IBlobContentLocation blobLocation)
        {
            try
            {
                var container = this.client.GetContainerReference(this.containerName);
                var blob = container.GetBlockBlobReference(this.GetBlobName(blobLocation));
                return blob;
            }
            catch (StorageException e)
            {
                throw this.WrapStorageException(e);
            }
        }

        private CloudBlobContainer GetOrCreateContainer(string name)
        {
            try
            {
                CloudBlobContainer container = this.client.GetContainerReference(name);

                // Note: For some reason this returns false even the container did not already exists (even in the 5.0.2 this behavior is same)
                container.CreateIfNotExists(BlobContainerPublicAccessType.Blob);
                return container;
            }
            catch (StorageException e)
            {
                throw this.WrapStorageException(e);
            }
        }

        private Exception WrapStorageException(StorageException ex)
        {
            var requestInfo = ex.RequestInformation != null && !string.IsNullOrEmpty(ex.RequestInformation.ErrorCode) ?
                string.Concat("Request information error code: ", ex.RequestInformation.ErrorCode) : string.Empty;
            return requestInfo.IsNullOrEmpty() ? ex : new Exception(string.Join(" ", ex.Message, requestInfo), ex);
        }

        #endregion

        #region Fields

        private string connectionString;
        private CloudBlobClient client;
        private string rootUrl;
        private string containerName;
        private bool isRelativeUrlEnabled;
        private bool enableBlobStorageChunkUpload;

        #endregion
    }
}
