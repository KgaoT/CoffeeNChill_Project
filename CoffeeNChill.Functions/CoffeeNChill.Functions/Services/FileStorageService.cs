using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

// Azure Files implementation used with a real Azure Storage account.
public class FileStorageService : IFileStorageService
{
    private const string ShareName = "staff-docs";
    private readonly ShareClient _shareClient;

    public FileStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["DocumentsConnectionString"] ?? configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("A document storage connection string is missing.");
        _shareClient = new ShareClient(connectionString, ShareName);
        _shareClient.CreateIfNotExists();
    }

    public async Task<StaffDocument> UploadDocumentAsync(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        var client = _shareClient.GetRootDirectoryClient().GetFileClient(name);
        await client.CreateAsync(file.Length, new ShareFileCreateOptions
        {
            HttpHeaders = new ShareFileHttpHeaders { ContentType = file.ContentType }
        });
        await using var stream = file.OpenReadStream();
        await client.UploadRangeAsync(new HttpRange(0, file.Length), stream);
        return ToDocument(name, file.ContentType, file.Length, DateTime.UtcNow);
    }

    public async Task<Stream?> DownloadDocumentAsync(string fileName)
    {
        var client = _shareClient.GetRootDirectoryClient().GetFileClient(Path.GetFileName(fileName));
        if (!await client.ExistsAsync()) return null;
        return (await client.DownloadAsync()).Value.Content;
    }

    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        var client = _shareClient.GetRootDirectoryClient().GetFileClient(Path.GetFileName(fileName));
        return (await client.DeleteIfExistsAsync()).Value;
    }

    public async Task<List<StaffDocument>> GetAllDocumentsAsync()
    {
        var documents = new List<StaffDocument>();
        var directory = _shareClient.GetRootDirectoryClient();
        await foreach (var item in directory.GetFilesAndDirectoriesAsync())
        {
            if (item.IsDirectory) continue;
            var properties = (await directory.GetFileClient(item.Name).GetPropertiesAsync()).Value;
            documents.Add(ToDocument(item.Name, properties.ContentType ?? "application/octet-stream",
                properties.ContentLength, properties.LastModified.UtcDateTime));
        }
        return documents;
    }

    private static StaffDocument ToDocument(string name, string contentType, long size, DateTime uploadedOn) => new()
    {
        FileName = name, FileExtension = Path.GetExtension(name), ContentType = contentType,
        FileSize = size, UploadedOn = uploadedOn, ContainerName = ShareName
    };
}
