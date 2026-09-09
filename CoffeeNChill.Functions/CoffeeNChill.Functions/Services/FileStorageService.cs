using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly BlobContainerClient _containerClient;

        private const string ContainerName = "staff-docs";

        public FileStorageService(IConfiguration configuration)
        {
            string connectionString =
                configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "AzureWebJobsStorage connection string is missing.");

            BlobServiceClient blobServiceClient =
                new BlobServiceClient(connectionString);

            _containerClient =
                blobServiceClient.GetBlobContainerClient(ContainerName);

            _containerClient.CreateIfNotExists();
        }

        public async Task<StaffDocument> UploadDocumentAsync(
            IFormFile file)
        {
            BlobClient blobClient =
                _containerClient.GetBlobClient(file.FileName);

            using Stream stream = file.OpenReadStream();

            BlobHttpHeaders headers =
                new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                };

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = headers
                });

            return new StaffDocument
            {
                FileName = file.FileName,
                FileExtension = Path.GetExtension(file.FileName),
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedOn = DateTime.UtcNow,
                ContainerName = ContainerName
            };
        }

        public async Task<Stream?> DownloadDocumentAsync(
            string fileName)
        {
            BlobClient blobClient =
                _containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return null;
            }

            BlobDownloadInfo download =
                await blobClient.DownloadAsync();

            return download.Content;
        }

        public async Task<bool> DeleteDocumentAsync(
            string fileName)
        {
            BlobClient blobClient =
                _containerClient.GetBlobClient(fileName);

            var response =
                await blobClient.DeleteIfExistsAsync();

            return response.Value;
        }

        public async Task<List<StaffDocument>> GetAllDocumentsAsync()
        {
            List<StaffDocument> documents =
                new List<StaffDocument>();

            await foreach (
                BlobItem item
                in _containerClient.GetBlobsAsync())
            {
                BlobClient blobClient =
                    _containerClient.GetBlobClient(item.Name);

                BlobProperties properties =
                    await blobClient.GetPropertiesAsync();

                documents.Add(new StaffDocument
                {
                    FileName = item.Name,
                    FileExtension = Path.GetExtension(item.Name),
                    ContentType =
                        properties.ContentType ?? string.Empty,
                    FileSize =
                        properties.ContentLength,
                    UploadedOn =
                        properties.LastModified.DateTime,
                    ContainerName = ContainerName
                });
            }

            return documents;
        }
    }
}

