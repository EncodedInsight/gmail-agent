using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using GmailAgentFunctionApp.Models;
using Microsoft.Extensions.Logging;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2.Requests;
using System.Threading.Tasks;
using System.Text.Json;
using Azure.Core;

namespace GmailAgentFunctionApp.Services
{
    public class GoogleAuthService
    {
        private readonly GoogleAuthConfig _config;
        private readonly TokenStorageService _tokenStorage;
        private readonly ILogger<GoogleAuthService> _logger;
        private UserCredential? _credentials;

        public GoogleAuthService(GoogleAuthConfig config, TokenStorageService tokenStorage, ILogger<GoogleAuthService> logger)
        {
            _config = config;
            _tokenStorage = tokenStorage;
            _logger = logger;
        }

        public string GetAuthorizationUrl()
        {
            var flow = CreateFlow();
            var request = (GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(_config.RedirectUri);
            
            request.AccessType = "offline";
            request.Prompt = "consent";
            
            return request.Build().ToString();
        }

        private async Task RefreshTokenIfNeededAsync(UserCredential credentials, TokenResponse? originalToken = null)
        {
            if (!credentials.Token.IsStale)
            {
                _logger.LogInformation("Token is still valid, no refresh needed");
                return;
            }

            _logger.LogInformation("Token is stale, attempting refresh");

            // Log token state before refresh
            _logger.LogInformation("Token before refresh - Access Token: {AccessToken}, Refresh Token: {RefreshToken}, Expiry: {Expiry}",
                credentials.Token.AccessToken?.Substring(0, 10) + "...",
                !string.IsNullOrEmpty(credentials.Token.RefreshToken) ? "Present" : "Missing",
                credentials.Token.ExpiresInSeconds);

            if (string.IsNullOrEmpty(credentials.Token.RefreshToken))
            {
                _logger.LogError("No refresh token found in credentials");
                throw new Exception("No refresh token found in credentials");
            }

            var success = await credentials.RefreshTokenAsync(CancellationToken.None);
            if (!success)
            {
                _logger.LogError("Failed to refresh token");
                throw new Exception("Failed to refresh token");
            }

            // Log token details after refresh
            _logger.LogInformation("Refreshed token details - Access Token: {AccessToken}, Refresh Token: {RefreshToken}, Expiry: {Expiry}",
                credentials.Token.AccessToken?.Substring(0, 10) + "...",
                !string.IsNullOrEmpty(credentials.Token.RefreshToken) ? "Present" : "Missing",
                credentials.Token.ExpiresInSeconds);

            // If refresh token is missing after refresh, restore it from the original token
            if (string.IsNullOrEmpty(credentials.Token.RefreshToken) && originalToken != null)
            {
                _logger.LogWarning("Refresh token was dropped during refresh, restoring from original token");
                credentials.Token.RefreshToken = originalToken.RefreshToken;
            }

            await _tokenStorage.SaveTokenAsync(credentials.Token);
        }

        public async Task<UserCredential> GetOrCreateCredentialsAsync()
        {
            try 
            {
                // Check if we have credentials in memory
                if (_credentials != null)
                {
                    _logger.LogInformation("In-memory credentials found");
                    await RefreshTokenIfNeededAsync(_credentials);
                    return _credentials;
                }

                // Try to load token from storage
                _logger.LogInformation("Getting token from storage");

                var token = await _tokenStorage.GetTokenAsync();
                if (token == null)
                {
                    _logger.LogWarning("No stored credentials found");
                    throw new InvalidOperationException("No stored credentials found. User must authenticate first.");
                }

                _logger.LogInformation("Found stored token, creating credentials");
                var flow = CreateFlow();
                _credentials = new UserCredential(flow, "user", token);
                
                // TEMP OVERRIDE removed since refresh check is now in RefreshTokenIfNeededAsync
                await RefreshTokenIfNeededAsync(_credentials, token);

                _logger.LogInformation("Returning credentials");
                return _credentials;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrCreateCredentialsAsync: {ex.Message}");
                throw;
            }
        }

        private GoogleAuthorizationCodeFlow CreateFlow() {
            var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _config.ClientId,
                    ClientSecret = _config.ClientSecret
                },
                Scopes = GoogleAuthConfig.Scopes,
                DataStore = new TokenStorageDataStore(_tokenStorage),
            };

            return new GoogleAuthorizationCodeFlow(initializer);
        }

        public async Task<UserCredential> ExchangeCodeForCredentialsAsync(string code)
        {
            var flow = CreateFlow();

            var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                userId: "user",
                code: code,
                redirectUri: _config.RedirectUri,
                CancellationToken.None);

            // Log the token response with all properties
            _logger.LogInformation("Token response: {tokenResponse}", JsonSerializer.Serialize(tokenResponse));

            await _tokenStorage.SaveTokenAsync(tokenResponse);
            _credentials = new UserCredential(flow, "user", tokenResponse);
        
            // // Modified refresh handler
            // await _credentials.Flow.DataStore.StoreAsync("user", tokenResponse);

            return _credentials;
        }

        public async Task<GmailService> CreateGmailService()
        {
            try
            {
                var credentials = await GetOrCreateCredentialsAsync();

                _logger.LogInformation("Credentials: {credentials}", credentials);

                _logger.LogInformation("Creating Gmail service");

                var applicationName = Environment.GetEnvironmentVariable("APPLICATION_NAME") ?? "Gmail Agent";

                return new GmailService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credentials,
                    ApplicationName = applicationName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating Gmail service: {ex.Message}");
                throw;
            }
        }

        public async Task RevokeTokenAsync()
        {
            if (_credentials != null)
            {
                await _credentials.RevokeTokenAsync(CancellationToken.None);
                _credentials = null;
            }
            await _tokenStorage.ClearTokenAsync();
        }
    }

    internal class TokenStorageDataStore : IDataStore
    {
        private readonly TokenStorageService _tokenStorage;
        private readonly ILogger<TokenStorageDataStore> _logger;
        
        public TokenStorageDataStore(TokenStorageService tokenStorage)
        {
            _tokenStorage = tokenStorage;
        }

        public async Task StoreAsync<T>(string key, T value)
        {
            if (value is TokenResponse token)
            {
                await _tokenStorage.SaveTokenAsync(token);
            }
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var token = await _tokenStorage.GetTokenAsync();
            return (T)(object)token;
        }

        public async Task DeleteAsync<T>(string key)
        {
            await _tokenStorage.ClearTokenAsync();
        }

        public async Task ClearAsync()
        {
            await _tokenStorage.ClearTokenAsync();
        }
    }
} 