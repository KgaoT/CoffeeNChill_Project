using System.Net;
using System.Text.Json; 


// catches unexpected errors and returns consistent JSON error message.
namespace CoffeeNChill.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected error occurred while processing the request.");

                context.Response.StatusCode =
                    (int)HttpStatusCode.InternalServerError;

                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    error = "An unexpected server error occurred.",
                    statusCode = context.Response.StatusCode
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(errorResponse));
            }
        }
    }
}
