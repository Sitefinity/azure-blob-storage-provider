# Sitefinity's Azure Blob Storage Provider

####Since version 8.2 Sitefinity suuports AzureBlobStorageProvider out of the box.

Sitefinity's Azure Blob Storage Provider is an implementation of a cloud blob storage provider, which stores the binary blob data of Sitefinity's library items on Azure storage. This document describes how to use the provider.

Note that only the binary data of the items in a library are stored on the remote blob storage. Sitefinity still manages its logical items by library with their regular meta-data properties (title, description etc) in its own database.

## Dependencies

### OData

This version depends on three libraries (collectively referred to as ODataLib), which are resolved through the ODataLib (version 5.6.4) packages available through NuGet and not the WCF Data Services installer which currently contains 5.0.0 versions.

The ODataLib libraries can be downloaded directly or referenced by your code project through NuGet.  

The specific ODataLib packages are:

- [Microsoft.Data.OData](http://nuget.org/packages/Microsoft.Data.OData/)
- [Microsoft.Data.Edm](http://nuget.org/packages/Microsoft.Data.Edm/)
- [System.Spatial](http://nuget.org/packages/System.Spatial)

For full documentation, please refer to the [Wiki](https://github.com/Sitefinity/azure-blog-storage-provider/wiki).