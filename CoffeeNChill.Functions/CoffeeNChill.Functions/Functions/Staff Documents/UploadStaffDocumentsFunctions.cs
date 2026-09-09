/*namespace CoffeeNChill.Functions.Functions
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

{
    public class DeleteStaffDocumentFunction
    {
        private readonly IFileStorageService _fileStorageService;

        public DeleteStaffDocumentFunction(
            IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        [Function("DeleteStaffDocument")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "delete",
                Route = "documents/{fileName}")]
            HttpRequestData req,
            string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var badRequest =
                        req.CreateResponse(
                            HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        message = "A file name is required."
                    });

                    return badRequest;
                }

                bool deleted =
                    await _fileStorageService
                        .DeleteDocumentAsync(fileName);

                if (!deleted)
                {
                    var notFound =
                        req.CreateResponse(
                            HttpStatusCode.NotFound);

                    await notFound.WriteAsJsonAsync(new
                    {
                        message =
                            $"Document '{fileName}' was not found."
                    });

                    return notFound;
                }

                var response =
                    req.CreateResponse(
                        HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    message =
                        $"Document '{fileName}' was deleted successfully."
                });

                return response;
            }
            catch (Exception ex)
            {
                var errorResponse =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await errorResponse.WriteAsJsonAsync(new
                {
                    message =
                        "An error occurred while deleting the document.",
                    error = ex.Message
                });

                return errorResponse;
            }
        }
    }
}*/
