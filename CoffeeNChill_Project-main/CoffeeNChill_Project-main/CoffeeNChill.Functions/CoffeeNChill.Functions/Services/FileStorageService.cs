
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

public class FileStorageService : IFileStorageService
{
    private const string StorageName = "staff-docs";

    private readonly BlobContainerClient? _blobContainerClient;
    private readonly ShareClient? _shareClient;

    public FileStorageService(IConfiguration configuration)
    {
        var mode = configuration["DocumentStorageMode"] ?? "Files";

        var connectionString =
            configuration["DocumentsConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException(
                "A document storage connection string is missing.");

        if (mode.Equals("Blob", StringComparison.OrdinalIgnoreCase))
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _blobContainerClient =
                blobServiceClient.GetBlobContainerClient(StorageName);

            _blobContainerClient.CreateIfNotExists();
        }
        else
        {
            _shareClient = new ShareClient(connectionString, StorageName);
            _shareClient.CreateIfNotExists();
        }
    }

    public async Task<StaffDocument> UploadDocumentAsync(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);

        if (_blobContainerClient != null)
        {
            var blob = _blobContainerClient.GetBlobClient(name);

            await using var stream = file.OpenReadStream();

            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            });

            return ToDocument(name, file.ContentType, file.Length,
                DateTime.UtcNow);
        }

        var client = _shareClient!.GetRootDirectoryClient().GetFileClient(name);

        await client.CreateAsync(file.Length, new ShareFileCreateOptions
        {
            HttpHeaders = new ShareFileHttpHeaders
            {
                ContentType = file.ContentType
            }
        });

        await using var fileStream = file.OpenReadStream();

        await client.UploadRangeAsync(
            new HttpRange(0, file.Length), fileStream);

        return ToDocument(name, file.ContentType, file.Length,
            DateTime.UtcNow);
    }

    public async Task<Stream?> DownloadDocumentAsync(string fileName)
    {
        var name = Path.GetFileName(fileName);

        if (_blobContainerClient != null)
        {
            var blob = _blobContainerClient.GetBlobClient(name);

            if (!await blob.ExistsAsync())
                return null;

            return (await blob.DownloadStreamingAsync()).Value.Content;
        }

        var client = _shareClient!.GetRootDirectoryClient().GetFileClient(name);

        if (!await client.ExistsAsync())
            return null;

        return (await client.DownloadAsync()).Value.Content;
    }

    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        var name = Path.GetFileName(fileName);

        if (_blobContainerClient != null)
        {
            var blob = _blobContainerClient.GetBlobClient(name);
            return (await blob.DeleteIfExistsAsync()).Value;
        }

        var client = _shareClient!.GetRootDirectoryClient().GetFileClient(name);
        return (await client.DeleteIfExistsAsync()).Value;
    }

    public async Task<List<StaffDocument>> GetAllDocumentsAsync()
    {
        var documents = new List<StaffDocument>();

        if (_blobContainerClient != null)
        {
            await foreach (var blob in _blobContainerClient.GetBlobsAsync())
            {
                var properties = blob.Properties;

                documents.Add(ToDocument(
                    blob.Name,
                    properties.ContentType ?? "application/octet-stream",
                    properties.ContentLength ?? 0,
                    properties.LastModified?.UtcDateTime ?? DateTime.UtcNow));
            }

            return documents;
        }

        var directory = _shareClient!.GetRootDirectoryClient();

        await foreach (var item in directory.GetFilesAndDirectoriesAsync())
        {
            if (item.IsDirectory)
                continue;

            var properties = (await directory
                .GetFileClient(item.Name)
                .GetPropertiesAsync()).Value;

            documents.Add(ToDocument(
                item.Name,
                properties.ContentType ?? "application/octet-stream",
                properties.ContentLength,
                properties.LastModified.UtcDateTime));
        }

        return documents;
    }

    private static StaffDocument ToDocument(
        string name,
        string contentType,
        long size,
        DateTime uploadedOn) => new()
        {
            FileName = name,
            FileExtension = Path.GetExtension(name),
            ContentType = contentType,
            FileSize = size,
            UploadedOn = uploadedOn,
            ContainerName = StorageName
        };
}

