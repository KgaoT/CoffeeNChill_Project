using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace CoffeeNChill.Functions.Functions.StaffDocuments;

public class ListStaffDocumentsFunction(IFileStorageService storage)
{
    [Function("ListStaffDocuments")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequest req) =>
        new OkObjectResult(await storage.GetAllDocumentsAsync());
}
