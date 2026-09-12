using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<IMenuItemService, MenuItemService>();
builder.Services.AddSingleton<IFileStorageService>(services =>
{
    var configuration = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var mode = configuration["DocumentStorageMode"] ?? "Blob";

    return mode.Equals("FileShare", StringComparison.OrdinalIgnoreCase)
        ? new FileStorageService(configuration)
        : new AzuriteBlobStorageService(configuration);
});

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
