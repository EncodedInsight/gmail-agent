using GmailAgentFunctionApp.Services;
using GmailAgentFunctionApp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Storage.Blobs;
using System.Net.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Validate storage connection string
        var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(storageConnection))
        {
            throw new InvalidOperationException("AzureWebJobsStorage connection string is not configured");
        }

        services.AddHttpClient();

        services.AddSingleton<BlobServiceClient>(sp => 
            new BlobServiceClient(storageConnection));
        services.AddSingleton<GoogleAuthConfig>();
        services.AddSingleton<TokenStorageService>();
        services.AddSingleton<GoogleAuthService>();
        services.AddSingleton<OpenAiService>();
        services.AddSingleton<HistoryIdStorageService>();
    })
    .Build();

await host.RunAsync(); 