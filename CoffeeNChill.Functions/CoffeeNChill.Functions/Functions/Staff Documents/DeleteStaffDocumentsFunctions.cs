using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace CoffeeNChill.Functions.Functions.StaffDocuments;

public class DeleteStaffDocumentsFunction(IFileStorageService storage)
{
    [Function("DeleteStaffDocument")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "documents/{fileName}")] HttpRequest req,
        string fileName) => await storage.DeleteDocumentAsync(fileName)
            ? new NoContentResult()
            : new NotFoundObjectResult(new { message = "Document not found." });
}
