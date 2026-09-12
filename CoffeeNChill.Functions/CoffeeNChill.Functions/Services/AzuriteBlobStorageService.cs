using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

// Azurite does not emulate Azure Files. This adapter preserves the document API for the local Docker demo.
public class AzuriteBlobStorageService : IFileStorageService
{
    private const string ContainerName = "staff-docs";
    private readonly BlobContainerClient _container;

    public AzuriteBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is missing.");
        _container = new BlobContainerClient(connectionString, ContainerName);
        _container.CreateIfNotExists();
    }

    public async Task<StaffDocument> UploadDocumentAsync(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        var client = _container.GetBlobClient(name);
        await using var stream = file.OpenReadStream();
        await client.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        });
        return ToDocument(name, file.ContentType, file.Length, DateTime.UtcNow);
    }

    public async Task<Stream?> DownloadDocumentAsync(string fileName)
    {
        var client = _container.GetBlobClient(Path.GetFileName(fileName));
        if (!await client.ExistsAsync()) return null;
        return (await client.DownloadStreamingAsync()).Value.Content;
    }

    public async Task<bool> DeleteDocumentAsync(string fileName) =>
        (await _container.GetBlobClient(Path.GetFileName(fileName)).DeleteIfExistsAsync()).Value;

    public async Task<List<StaffDocument>> GetAllDocumentsAsync()
    {
        var documents = new List<StaffDocument>();
        await foreach (var item in _container.GetBlobsAsync())
            documents.Add(ToDocument(item.Name, item.Properties.ContentType ?? "application/octet-stream",
                item.Properties.ContentLength ?? 0, item.Properties.LastModified?.UtcDateTime ?? DateTime.UtcNow));
        return documents;
    }

    private static StaffDocument ToDocument(string name, string contentType, long size, DateTime uploadedOn) => new()
    {
        FileName = name, FileExtension = Path.GetExtension(name), ContentType = contentType,
        FileSize = size, UploadedOn = uploadedOn, ContainerName = ContainerName
    };
}
