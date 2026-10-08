using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

//Configure Azure Functions
//Configure Azure Functions with ASP.NET Core HTTP intergration
builder.ConfigureFunctionsWebApplication();

// HttpResponseData.WriteAsJsonAsync used the worker's default JSON options,
// which preserve C# property names as-is (PascalCase: "RowKey", "IsAvailable")
// instead of the camelCase REST clients (and this project's own Postman
// collection) expect. Found by actually running the collection: every test
// asserting on a lowerCamelCase field (rowKey, price, isAvailable,
// partitionKey) failed against the real response. Configuring the worker's
// serializer fixes every endpoint at once, rather than patching each
// response object individually.
builder.Services.Configure<Microsoft.Azure.Functions.Worker.WorkerOptions>(options =>
{
    options.Serializer = new Azure.Core.Serialization.JsonObjectSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
});

builder.Services.AddSingleton<ITableStorageService, TableStorageService>();

// Document storage mode switch: Azurite does not emulate Azure Files, so
// FileStorageService's ShareClient.CreateIfNotExists() throws against it and
// would take down every document endpoint for local/Docker runs. "FileShare"
// (the default, rubric-compliant mode) targets a real Azure Storage account;
// "Blob" targets Azurite's blob service for local/container demonstrations.
// Both implementations satisfy the exact same IFileStorageService contract,
// so no calling code needs to know which mode is active.
var documentStorageMode = builder.Configuration["DocumentStorageMode"] ?? "FileShare";
builder.Services.AddSingleton<IFileStorageService>(sp => documentStorageMode switch
{
    "Blob" => new AzuriteBlobStorageService(sp.GetRequiredService<IConfiguration>()),
    "FileShare" => new FileStorageService(sp.GetRequiredService<IConfiguration>()),
    _ => throw new InvalidOperationException(
        $"Unknown DocumentStorageMode '{documentStorageMode}'. Expected 'FileShare' or 'Blob'.")
});

builder.Build().Run();
