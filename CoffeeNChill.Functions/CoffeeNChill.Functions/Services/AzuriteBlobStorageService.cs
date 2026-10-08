using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

// Azurite does not emulate Azure Files, so FileStorageService's ShareClient
// (CreateIfNotExists in its constructor) throws against it, which would take
// down every document endpoint for local/Docker runs. This is the Blob-backed
// equivalent used instead when DocumentStorageMode=Blob, selected in
// Program.cs. Same container name ("staff-docs") and the exact same
// IFileStorageService contract/StaffDocument shape as FileStorageService, so
// callers never see a difference between the two modes.
public class AzuriteBlobStorageService : IFileStorageService
{
    private const string ContainerName = "staff-docs";
    private readonly BlobContainerClient _containerClient;

    public AzuriteBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is missing.");
        _containerClient = new BlobContainerClient(connectionString, ContainerName);
        _containerClient.CreateIfNotExists();
    }

    public async Task<StaffDocument> UploadDocumentAsync(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        var blobClient = _containerClient.GetBlobClient(name);
        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
            Conditions = null // allow overwrite, matching FileStorageService's CreateAsync-then-upload replace behavior
        });
        return ToDocument(name, file.ContentType, file.Length, DateTime.UtcNow);
    }

    public async Task<Stream?> DownloadDocumentAsync(string fileName)
    {
        var blobClient = _containerClient.GetBlobClient(Path.GetFileName(fileName));
        if (!await blobClient.ExistsAsync()) return null;
        return (await blobClient.DownloadStreamingAsync()).Value.Content;
    }

    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        var blobClient = _containerClient.GetBlobClient(Path.GetFileName(fileName));
        return await blobClient.DeleteIfExistsAsync();
    }

    public async Task<List<StaffDocument>> GetAllDocumentsAsync()
    {
        var documents = new List<StaffDocument>();
        await foreach (var item in _containerClient.GetBlobsAsync())
        {
            documents.Add(ToDocument(
                item.Name,
                item.Properties.ContentType ?? "application/octet-stream",
                item.Properties.ContentLength ?? 0,
                item.Properties.LastModified?.UtcDateTime ?? DateTime.UtcNow));
        }
        return documents;
    }

    private static StaffDocument ToDocument(string name, string contentType, long size, DateTime uploadedOn) => new()
    {
        FileName = name, FileExtension = Path.GetExtension(name), ContentType = contentType,
        FileSize = size, UploadedOn = uploadedOn, ContainerName = ContainerName
    };
}
