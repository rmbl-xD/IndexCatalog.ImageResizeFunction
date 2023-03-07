using System;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(IndexCatalog.ImageResize.Startup))]
namespace IndexCatalog.ImageResize;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var blobString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        builder.Services.AddScoped(_ => new BlobServiceClient(blobString));
    }
}