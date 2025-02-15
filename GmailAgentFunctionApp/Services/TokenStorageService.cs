using Azure.Storage.Blobs;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GmailAgentFunctionApp.Services
{
    public class TokenStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<TokenStorageService> _logger;
        private const string TokenFileName = "gmail_token.json";

        public TokenStorageService(BlobServiceClient blobServiceClient, ILogger<TokenStorageService> logger)
        {
            _containerClient = blobServiceClient.GetBlobContainerClient("tokens");
            _logger = logger;
            _containerClient.CreateIfNotExists();
        }

        public async Task SaveTokenAsync(Google.Apis.Auth.OAuth2.Responses.TokenResponse token)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(TokenFileName);
                var json = JsonSerializer.Serialize(token);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await blobClient.UploadAsync(stream, overwrite: true);
                _logger.LogInformation("Token saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving token: {ex.Message}");
                throw;
            }
        }

        public async Task<Google.Apis.Auth.OAuth2.Responses.TokenResponse?> GetTokenAsync()
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(TokenFileName);
                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogWarning("No token file found");
                    return null;
                }

                using var stream = await blobClient.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                var token = JsonSerializer.Deserialize<Google.Apis.Auth.OAuth2.Responses.TokenResponse>(json);
                _logger.LogInformation("Token retrieved successfully");
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving token: {ex.Message}");
                throw;
            }
        }

        public async Task ClearTokenAsync()
        {
            var blobClient = _containerClient.GetBlobClient(TokenFileName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
} 