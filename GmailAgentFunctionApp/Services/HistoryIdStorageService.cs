using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GmailAgentFunctionApp.Services
{
    /// <summary>
    /// Service for storing and retrieving the last known Gmail historyId
    /// </summary>
    public class HistoryIdStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<HistoryIdStorageService> _logger;
        private const string ContainerName = "gmail-history";
        private const string HistoryIdBlobName = "last-history-id.json";

        public HistoryIdStorageService(BlobServiceClient blobServiceClient, ILogger<HistoryIdStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the storage container if it doesn't exist
        /// </summary>
        private async Task EnsureContainerExistsAsync()
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                await containerClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ensuring container exists: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stores the last known historyId for a specific email address
        /// </summary>
        public async Task StoreLastHistoryIdAsync(string emailAddress, ulong historyId)
        {
            try
            {
                await EnsureContainerExistsAsync();

                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient($"{emailAddress}/{HistoryIdBlobName}");

                var historyData = new HistoryIdData
                {
                    EmailAddress = emailAddress,
                    HistoryId = historyId,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(historyData);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogInformation($"Stored historyId {historyId} for {emailAddress}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing historyId: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the last known historyId for a specific email address
        /// </summary>
        public async Task<ulong?> GetLastHistoryIdAsync(string emailAddress)
        {
            try
            {
                await EnsureContainerExistsAsync();

                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient($"{emailAddress}/{HistoryIdBlobName}");

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogInformation($"No stored historyId found for {emailAddress}");
                    return null;
                }

                var response = await blobClient.DownloadAsync();
                using var streamReader = new StreamReader(response.Value.Content);
                var json = await streamReader.ReadToEndAsync();
                
                var historyData = JsonSerializer.Deserialize<HistoryIdData>(json);
                
                if (historyData != null)
                {
                    _logger.LogInformation($"Retrieved historyId {historyData.HistoryId} for {emailAddress} (last updated: {historyData.LastUpdated})");
                    return historyData.HistoryId;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving historyId: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current historyId from Gmail for an email address and stores it
        /// </summary>
        public async Task InitializeHistoryIdAsync(string emailAddress, GoogleAuthService authService)
        {
            try
            {
                var gmailService = await authService.CreateGmailService();
                var profile = await gmailService.Users.GetProfile("me").ExecuteAsync();
                
                if (profile.HistoryId.HasValue)
                {
                    await StoreLastHistoryIdAsync(emailAddress, profile.HistoryId.Value);
                    _logger.LogInformation($"Initialized historyId {profile.HistoryId.Value} for {emailAddress}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing historyId: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Data model for storing historyId information
    /// </summary>
    public class HistoryIdData
    {
        public string EmailAddress { get; set; } = string.Empty;
        public ulong HistoryId { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 