using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Functions.Worker;

namespace CoffeeNChill.Functions.Functions.StaffDocuments;

public class DownloadStaffDocumentsFunction(IFileStorageService storage)
{
    [Function("DownloadStaffDocument")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequest req,
        string fileName)
    {
        var stream = await storage.DownloadDocumentAsync(fileName);
        if (stream is null) return new NotFoundObjectResult(new { message = "Document not found." });

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType)) contentType = "application/octet-stream";
        return new FileStreamResult(stream, contentType) { FileDownloadName = Path.GetFileName(fileName) };
    }
}
