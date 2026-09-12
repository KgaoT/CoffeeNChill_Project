using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace CoffeeNChill.Functions.Functions.StaffDocuments;

public class UploadStaffDocumentsFunction(IFileStorageService storage)
{
    [Function("UploadStaffDocument")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequest req)
    {
        if (!req.HasFormContentType)
            return new BadRequestObjectResult(new { message = "Content-Type must be multipart/form-data." });

        var form = await req.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return new BadRequestObjectResult(new { message = "A non-empty file is required in the 'file' field." });

        var document = await storage.UploadDocumentAsync(file);
        return new CreatedResult($"/api/documents/download/{Uri.EscapeDataString(document.FileName)}", document);
    }
}
