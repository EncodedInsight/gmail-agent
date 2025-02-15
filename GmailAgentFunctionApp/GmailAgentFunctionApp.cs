using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using GmailAgentFunctionApp.Services;
using GmailAgentFunctionApp.Models;
using System.Web;
using System.Text.Json;

namespace GmailAgentFunctionApp
{
    public class GmailAgentFunction
    {
        private readonly ILogger<GmailAgentFunction> _logger;
        private readonly GoogleAuthService _authService;
        private readonly OpenAiService _openAiService;
        private readonly HistoryIdStorageService _historyIdStorage;
        private readonly GoogleAuthConfig _authConfig;

        public GmailAgentFunction(
            ILogger<GmailAgentFunction> logger,
            GoogleAuthService authService,
            OpenAiService openAiService,
            HistoryIdStorageService historyIdStorage,
            GoogleAuthConfig authConfig)
        {
            _logger = logger;
            _authService = authService;
            _openAiService = openAiService;
            _historyIdStorage = historyIdStorage;
            _authConfig = authConfig;
        }

        [Function("GmailAgentAuth")]
        public async Task<HttpResponseData> HandleAuth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Starting OAuth flow");

            var authUrl = _authService.GetAuthorizationUrl();

            _logger.LogInformation($"Authorization URL: {authUrl}");

            var response = req.CreateResponse(HttpStatusCode.Found);
            response.Headers.Add("Location", authUrl);
            
            return response;
        }

        [Function("GmailAgentHome")]
        public HttpResponseData GetHomePage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            var html = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Gmail Agent</title>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 40px; }
                        .button { 
                            display: inline-block;
                            padding: 10px 20px;
                            background-color: #4285f4;
                            color: white;
                            text-decoration: none;
                            border-radius: 4px;
                            margin-right: 10px;
                            margin-bottom: 10px;
                        }
                        .logout {
                            background-color: #dc3545;
                        }
                        .urgent {
                            background-color: #ffc107;
                            color: black;
                        }
                        .high-risk {
                            background-color: #dc3545;
                        }
                        .push-notifications {
                            background-color: #28a745;
                        }
                        .stop-notifications {
                            background-color: #6c757d;
                        }
                        .initialize {
                            background-color: #17a2b8;
                        }
                        .button-group {
                            margin-top: 20px;
                        }
                    </style>
                </head>
                <body>
                    <h1>Gmail Agent</h1>
                    <p>Click below to authorize the application to access your Gmail account:</p>
                    <div class='button-group'>
                        <a href='/api/GmailAgentAuth' class='button'>Authorize Gmail Access</a>
                        <a href='/api/GmailAgentTest' class='button'>Test Authentication</a>
                        <a href='/api/GmailAgentLogout' class='button logout'>Logout</a>
                    </div>
                    <h2>Email Processing</h2>
                    <div class='button-group'>
                        <a href='/api/ProcessUrgentMessagesHttp' class='button urgent'>Process Urgent Messages</a>
                        <a href='/api/ProcessHighRiskMessagesHttp' class='button high-risk'>Process High Risk Messages</a>
                    </div>
                    <h2>Push Notifications</h2>
                    <div class='button-group'>
                        <a href='/api/SetupPushNotifications' class='button push-notifications'>Setup Push Notifications</a>
                        <a href='/api/StopPushNotifications' class='button stop-notifications'>Stop Push Notifications</a>
                        <a href='/api/InitializeHistoryId' class='button initialize'>Initialize History ID</a>
                        <a href='/api/RenewPushNotificationsHttp' class='button push-notifications'>Renew Push Notifications</a>
                    </div>
                </body>
                </html>";

            response.WriteString(html);
            return response;
        }

        [Function("GmailAgentRedirectHandler")]
        public async Task<HttpResponseData> HandleRedirect(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Processing OAuth callback");

            var queryDictionary = HttpUtility.ParseQueryString(req.Url.Query);
            var code = queryDictionary["code"];

            if (string.IsNullOrEmpty(code))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync(@"
                    <html><body>
                        <h1>Error</h1>
                        <p>No authorization code received</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }

            try
            {
                var credentials = await _authService.ExchangeCodeForCredentialsAsync(code);
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync(@"
                    <html><body>
                        <h1>Success!</h1>
                        <p>Authentication successful! You can now close this window.</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during OAuth callback: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync(@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Authentication failed</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("GmailAgentTest")]
        public async Task<HttpResponseData> TestGmailAuth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Testing Gmail authentication");

            try
            {
                var gmailService = await _authService.CreateGmailService();

                // Get inbox messages count
                var inboxListRequest = gmailService.Users.Messages.List("me");
                inboxListRequest.LabelIds = new[] { "INBOX" };
                var inbox = await inboxListRequest.ExecuteAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync($@"
                    <html><body>
                        <h1>Gmail Authentication Test</h1>
                        <p>Successfully authenticated!</p>
                        <p>Number of messages in inbox: {inbox.Messages?.Count ?? 0}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication test failed: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync($@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Not authenticated. Please authorize first.</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("ProcessUrgentMessages")]
        public async Task ProcessUrgentMessages([TimerTrigger("0 0 * * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"ProcessUrgentMessages started at: {DateTime.UtcNow}");

            try
            {
                await LabelUrgentMessagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in ProcessUrgentMessages: {ex.Message}");
                throw;
            }
        }

        [Function("ProcessUrgentMessagesHttp")]
        public async Task<HttpResponseData> ProcessUrgentMessagesHttp(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("ProcessUrgentMessagesHttp started");

            try
            {
                await LabelUrgentMessagesAsync();
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in ProcessUrgentMessagesHttp: {ex.Message}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                return response;
            }
        }

        private async Task LabelUrgentMessagesAsync()
        {
            var gmailService = await _authService.CreateGmailService();

            // Ensure URGENT label exists
            var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();
            var urgentLabel = labels.Labels.FirstOrDefault(l => l.Name == "URGENT");

            if (urgentLabel == null)
            {
                _logger.LogInformation("Creating URGENT label");
                var newLabel = new Google.Apis.Gmail.v1.Data.Label
                {
                    Name = "URGENT",
                    LabelListVisibility = "labelShow",
                    MessageListVisibility = "show"
                };
                urgentLabel = await gmailService.Users.Labels.Create(newLabel, "me").ExecuteAsync();
            }

            // Get all messages in inbox
            var inboxRequest = gmailService.Users.Messages.List("me");
            inboxRequest.LabelIds = new[] { "INBOX" };
            var inbox = await inboxRequest.ExecuteAsync();

            if (inbox.Messages != null)
            {
                foreach (var message in inbox.Messages)
                {
                    var fullMessage = await gmailService.Users.Messages.Get("me", message.Id).ExecuteAsync();
                    
                    // Check if this is an email from me to me
                    string fromAddress = fullMessage.Payload?.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                    string toAddress = fullMessage.Payload?.Headers?.FirstOrDefault(h => h.Name == "To")?.Value ?? "";
                    
                    if (fromAddress.Contains(_authConfig.UserEmail) && toAddress.Contains(_authConfig.UserEmail))
                    {
                        _logger.LogInformation($"Skipping urgency check for email from me to me: {message.Id}");
                        continue;
                    }
                    
                    // Check if message already has URGENT label
                    if (fullMessage.LabelIds != null && fullMessage.LabelIds.Contains(urgentLabel.Id))
                    {
                        continue;
                    }

                    // Get message content
                    string subject = "";
                    string sender = "";
                    string body = "";

                    if (fullMessage.Payload?.Headers != null)
                    {
                        subject = fullMessage.Payload.Headers
                            .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";
                        sender = fullMessage.Payload.Headers
                            .FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                    }

                    if (fullMessage.Payload?.Parts != null)
                    {
                        foreach (var part in fullMessage.Payload.Parts)
                        {
                            if (part.MimeType == "text/plain" && part.Body?.Data != null)
                            {
                                body = DecodeBase64(part.Body.Data);
                                break;
                            }
                        }
                    }
                    else if (fullMessage.Payload?.Body?.Data != null)
                    {
                        body = DecodeBase64(fullMessage.Payload.Body.Data);
                    }

                    // Use OpenAI to analyze the email
                    if (await _openAiService.IsEmailUrgentAsync(subject, body, sender))
                    {
                        _logger.LogInformation($"Adding URGENT label to message with subject: {subject}");
                        
                        var modifyRequest = new Google.Apis.Gmail.v1.Data.ModifyMessageRequest
                        {
                            AddLabelIds = new[] { urgentLabel.Id }
                        };
                        
                        await gmailService.Users.Messages.Modify(modifyRequest, "me", message.Id).ExecuteAsync();
                    }
                }
            }
        }

        private string DecodeBase64(string base64)
        {
            var bytes = System.Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        [Function("GmailAgentLogout")]
        public async Task<HttpResponseData> HandleLogout(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Processing logout request");

            try
            {
                await _authService.RevokeTokenAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync(@"
                    <html><body>
                        <h1>Logged Out</h1>
                        <p>You have been successfully logged out.</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during logout: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync(@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Logout failed</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("ProcessHighRiskMessages")]
        public async Task ProcessHighRiskMessages([TimerTrigger("0 0 * * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"ProcessHighRiskMessages started at: {DateTime.UtcNow}");

            try
            {
                await LabelHighRiskMessagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in ProcessHighRiskMessages: {ex.Message}");
                throw;
            }
        }

        [Function("ProcessHighRiskMessagesHttp")]
        public async Task<HttpResponseData> ProcessHighRiskMessagesHttp(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("ProcessHighRiskMessagesHttp started");

            try
            {
                await LabelHighRiskMessagesAsync();
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in ProcessHighRiskMessagesHttp: {ex.Message}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                return response;
            }
        }

        private async Task LabelHighRiskMessagesAsync()
        {
            var gmailService = await _authService.CreateGmailService();

            try
            {
                // Ensure labels exist
                var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();
                var highRiskLabel = await EnsureLabelExists(gmailService, "HIGH_RISK");
                var moderateRiskLabel = await EnsureLabelExists(gmailService, "MODERATE_RISK");

                // Get all unread messages in inbox
                var inboxRequest = gmailService.Users.Messages.List("me");
                inboxRequest.LabelIds = new[] { "INBOX", "UNREAD" };
                var inbox = await inboxRequest.ExecuteAsync();

                if (inbox.Messages != null)
                {
                    foreach (var message in inbox.Messages)
                    {
                        _logger.LogInformation($"Processing message: {message.Id}");

                        var fullMessage = await gmailService.Users.Messages.Get("me", message.Id).ExecuteAsync();
                        
                        // Check if this is an email from me to me
                        string fromAddress = fullMessage.Payload?.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                        string toAddress = fullMessage.Payload?.Headers?.FirstOrDefault(h => h.Name == "To")?.Value ?? "";
                        
                        if (fromAddress.Contains(_authConfig.UserEmail) && toAddress.Contains(_authConfig.UserEmail))
                        {
                            _logger.LogInformation($"Skipping risk check for email from me to me: {message.Id}");
                            continue;
                        }
                        
                        // Skip if message already has risk labels
                        if (fullMessage.LabelIds != null && 
                            (fullMessage.LabelIds.Contains(highRiskLabel.Id) || 
                             fullMessage.LabelIds.Contains(moderateRiskLabel.Id)))
                        {
                            continue;
                        }

                        // Extract message content
                        var (subject, sender, body, attachments) = ExtractMessageContent(fullMessage);

                        // Analyze the email for risks
                        var riskAnalysis = await _openAiService.AnalyzeEmailRiskAsync(subject, body, sender, attachments);

                        if (riskAnalysis.RiskLevel != EmailRiskLevel.None)
                        {
                            var labelId = riskAnalysis.RiskLevel == EmailRiskLevel.High 
                                ? highRiskLabel.Id 
                                : moderateRiskLabel.Id;

                            _logger.LogInformation($"Adding {riskAnalysis.RiskLevel} label to message with subject: {subject}");
                            
                            var modifyRequest = new Google.Apis.Gmail.v1.Data.ModifyMessageRequest
                            {
                                AddLabelIds = new[] { labelId }
                            };
                            
                            await gmailService.Users.Messages.Modify(modifyRequest, "me", message.Id).ExecuteAsync();

                            // Only send notification email for HIGH_RISK messages
                            if (riskAnalysis.RiskLevel == EmailRiskLevel.High)
                            {
                                await SendRiskNotificationEmail(gmailService, fullMessage, sender, subject, riskAnalysis.Explanation);
                            }
                        }

                        _logger.LogInformation($"Processed message: {message.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in LabelHighRiskMessagesAsync: {ex.Message}");
            }

            _logger.LogInformation("LabelHighRiskMessagesAsync completed");
        }

        private async Task<Google.Apis.Gmail.v1.Data.Label> EnsureLabelExists(
            Google.Apis.Gmail.v1.GmailService gmailService, 
            string labelName)
        {
            var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();
            var label = labels.Labels.FirstOrDefault(l => l.Name == labelName);

            if (label == null)
            {
                _logger.LogInformation($"Creating {labelName} label");
                var newLabel = new Google.Apis.Gmail.v1.Data.Label
                {
                    Name = labelName,
                    LabelListVisibility = "labelShow",
                    MessageListVisibility = "show"
                };
                label = await gmailService.Users.Labels.Create(newLabel, "me").ExecuteAsync();
            }

            return label;
        }

        private (string subject, string sender, string body, string attachments) ExtractMessageContent(
            Google.Apis.Gmail.v1.Data.Message message)
        {
            string subject = "";
            string sender = "";
            string body = "";
            string attachments = "";

            if (message.Payload?.Headers != null)
            {
                subject = message.Payload.Headers
                    .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";
                sender = message.Payload.Headers
                    .FirstOrDefault(h => h.Name == "From")?.Value ?? "";
            }

            if (message.Payload?.Parts != null)
            {
                var attachmentsList = message.Payload.Parts
                    .Where(p => !string.IsNullOrEmpty(p.Filename))
                    .Select(p => p.Filename)
                    .ToList();

                attachments = string.Join(", ", attachmentsList);

                foreach (var part in message.Payload.Parts)
                {
                    if (part.MimeType == "text/plain" && part.Body?.Data != null)
                    {
                        body = DecodeBase64(part.Body.Data);
                        break;
                    }
                }
            }
            else if (message.Payload?.Body?.Data != null)
            {
                body = DecodeBase64(message.Payload.Body.Data);
            }

            return (subject, sender, body, attachments);
        }

        private async Task SendRiskNotificationEmail(
            Google.Apis.Gmail.v1.GmailService gmailService,
            Google.Apis.Gmail.v1.Data.Message message,
            string sender,
            string subject,
            string riskExplanation)
        {
            // Get the original message's thread ID and message ID
            var threadId = message.ThreadId;
            var messageId = message.Id;
            var originalSubject = message.Payload?.Headers?
                .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";

            // Create reply message
            var replyMessage = new Google.Apis.Gmail.v1.Data.Message
            {
                ThreadId = threadId,
                Raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    $"From: me\r\n" +
                    $"To: {_authConfig.UserEmail}\r\n" +
                    $"Subject: {originalSubject}\r\n" +  // Must match exactly
                    $"Message-ID: <{Guid.NewGuid()}@gmail.com>\r\n" +
                    $"In-Reply-To: {messageId}\r\n" +
                    $"References: {messageId}\r\n" +
                    "\r\n" +
                    $"⚠️ High risk email detected from: {sender}\r\n\r\n" +
                    $"Original Subject: {subject}\r\n\r\n" +
                    "Risk Analysis Report:\r\n" +
                    $"{riskExplanation}\r\n\r\n" +
                    "It is recommended to not engage with this email."
                )).Replace("+", "-").Replace("/", "_")
            };

            // Send the reply
            await gmailService.Users.Messages.Send(replyMessage, "me").ExecuteAsync();
            _logger.LogInformation($"Sent risk analysis reply for message: {message.Id}");
        }

        [Function("SetupPushNotifications")]
        public async Task<HttpResponseData> SetupPushNotifications(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Setting up Gmail push notifications");

            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get user profile to get email address and current historyId
                var profile = await gmailService.Users.GetProfile("me").ExecuteAsync();
                string emailAddress = profile.EmailAddress;
                
                // Initialize the history ID
                if (profile.HistoryId.HasValue)
                {
                    await _historyIdStorage.StoreLastHistoryIdAsync(emailAddress, profile.HistoryId.Value);
                    _logger.LogInformation($"Initialized historyId {profile.HistoryId.Value} for {emailAddress}");
                }
                
                // Get the topic name from environment variables
                var topicName = Environment.GetEnvironmentVariable("GMAIL_PUBSUB_TOPIC");
                if (string.IsNullOrEmpty(topicName))
                {
                    _logger.LogError("GMAIL_PUBSUB_TOPIC environment variable is not set");
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                    await errorResponse.WriteStringAsync($@"
                        <html><body>
                            <h1>Error</h1>
                            <p>GMAIL_PUBSUB_TOPIC environment variable is not set</p>
                            <a href='/api/GmailAgentHome'>Return to Home</a>
                        </body></html>");
                    return errorResponse;
                }
                
                // Create a watch request for the user's inbox
                var watchRequest = new Google.Apis.Gmail.v1.Data.WatchRequest
                {
                    TopicName = topicName,
                    LabelIds = new List<string> { "INBOX" }
                };
                
                var watchResponse = await gmailService.Users.Watch(watchRequest, "me").ExecuteAsync();
                
                _logger.LogInformation($"Watch request successful. Expiration: {watchResponse.Expiration}");
                
                // Store the historyId from the watch response if available
                if (watchResponse.HistoryId.HasValue)
                {
                    await _historyIdStorage.StoreLastHistoryIdAsync(emailAddress, watchResponse.HistoryId.Value);
                    _logger.LogInformation($"Updated historyId to {watchResponse.HistoryId.Value} from watch response");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync($@"
                    <html><body>
                        <h1>Push Notifications Setup</h1>
                        <p>Successfully set up Gmail push notifications!</p>
                        <p>Email: {emailAddress}</p>
                        <p>Topic: {topicName}</p>
                        <p>History ID: {(watchResponse.HistoryId.HasValue ? watchResponse.HistoryId.Value.ToString() : "Not available")}</p>
                        <p>Expiration: {DateTime.UnixEpoch.AddMilliseconds(long.Parse(watchResponse.Expiration.ToString()))}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting up push notifications: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync($@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Failed to set up push notifications: {ex.Message}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("GmailPushNotificationHandler")]
        public async Task<HttpResponseData> HandlePushNotification(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Received Gmail push notification");

            try
            {
                // Read the notification payload
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Notification payload: {requestBody}");

                // Parse the notification data
                var notification = JsonSerializer.Deserialize<GmailPushNotification>(requestBody);
                _logger.LogInformation($"Parsed notification: Message present: {notification?.Message != null}");
                
                if (notification?.Message?.Data != null)
                {
                    // Decode the base64 data
                    var decodedData = DecodeBase64(notification.Message.Data);
                    _logger.LogInformation($"Decoded notification data: {decodedData}");
                    
                    try
                    {
                        // Parse the decoded data
                        var emailData = JsonSerializer.Deserialize<GmailEmailNotificationData>(decodedData);
                        _logger.LogInformation($"Parsed email data: " +
                            $"EmailId: '{emailData?.EmailId}', " +
                            $"MessageId: '{emailData?.MessageId}', " +
                            $"HistoryId: {emailData?.HistoryId}, " +
                            $"EmailAddress: '{emailData?.GetEmailAddress()}'");
                        
                        if (emailData != null)
                        {
                            string emailAddress = emailData.GetEmailAddress();
                            
                            // If we have a message ID, process it directly
                            string messageId = emailData.GetMessageId();
                            if (!string.IsNullOrEmpty(messageId))
                            {
                                _logger.LogInformation($"Processing email with ID: {messageId}");
                                await ProcessNewEmailAsync(messageId);
                            }
                            // Otherwise use history to process changes
                            else if (emailData.HistoryId > 0 && !string.IsNullOrEmpty(emailAddress))
                            {
                                // Get the stored history ID
                                ulong? storedHistoryId = await _historyIdStorage.GetLastHistoryIdAsync(emailAddress);
                                
                                // Store the new history ID for future use
                                await _historyIdStorage.StoreLastHistoryIdAsync(emailAddress, emailData.HistoryId);
                                _logger.LogInformation($"Updated stored historyId to {emailData.HistoryId} for {emailAddress}");
                                
                                if (storedHistoryId.HasValue)
                                {
                                    // Process changes from stored history ID to new history ID
                                    _logger.LogInformation($"Processing history changes from stored ID: {storedHistoryId.Value} to new ID: {emailData.HistoryId}");
                                    await ProcessHistoryChangesAsync(storedHistoryId.Value, emailData.HistoryId, emailAddress);
                                }
                                else
                                {
                                    // If no stored history ID, initialize it and process with a small window
                                    _logger.LogInformation($"No stored historyId found. Initializing with current ID: {emailData.HistoryId}");
                                    
                                    // Use a slightly lower history ID to catch recent changes
                                    ulong startHistoryId = emailData.HistoryId > 10 ? emailData.HistoryId - 10 : 1;
                                    await ProcessHistoryChangesAsync(startHistoryId, emailData.HistoryId, emailAddress);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No message ID or valid history ID found in notification.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not deserialize email notification data.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error deserializing or processing email data: {ex.Message}");
                        _logger.LogError($"Exception details: {ex}");
                    }
                }
                else
                {
                    _logger.LogWarning("No message data found in notification.");
                }
                
                // Return a success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing push notification: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                return response;
            }
        }

        private async Task ProcessHistoryChangesAsync(ulong startHistoryId, ulong endHistoryId, string emailAddress)
        {
            _logger.LogInformation($"Processing history changes from ID: {startHistoryId} to ID: {endHistoryId} for {emailAddress}");
            
            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get history list
                var historyRequest = gmailService.Users.History.List("me");
                historyRequest.StartHistoryId = startHistoryId;
                
                _logger.LogInformation($"Requesting history with StartHistoryId: {startHistoryId}");
                var historyList = await historyRequest.ExecuteAsync();
                _logger.LogInformation($"Retrieved history list with {historyList.History?.Count ?? 0} history items");
                
                if (historyList.History != null && historyList.History.Count > 0)
                {
                    foreach (var history in historyList.History)
                    {
                        _logger.LogInformation($"Processing history item with ID: {history.Id}");
                        
                        // Log all available properties to see what we're getting
                        _logger.LogInformation($"History item details: " +
                            $"ID: {history.Id}, " +
                            $"MessagesAdded: {history.MessagesAdded?.Count ?? 0}, " +
                            $"MessagesDeleted: {history.MessagesDeleted?.Count ?? 0}, " +
                            $"LabelsAdded: {history.LabelsAdded?.Count ?? 0}, " +
                            $"LabelsRemoved: {history.LabelsRemoved?.Count ?? 0}");
                        
                        // Process messages added
                        if (history.MessagesAdded != null && history.MessagesAdded.Count > 0)
                        {
                            _logger.LogInformation($"Found {history.MessagesAdded.Count} messages added");
                            
                            foreach (var messageAdded in history.MessagesAdded)
                            {
                                if (messageAdded.Message != null)
                                {
                                    _logger.LogInformation($"Processing added message with ID: {messageAdded.Message.Id}");
                                    // Process each new message
                                    await ProcessNewEmailAsync(messageAdded.Message.Id);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No messages added in this history item");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No history changes found between the stored and new history IDs.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing history changes: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        private async Task ProcessNewEmailAsync(string messageId)
        {
            _logger.LogInformation($"Processing new email with ID: {messageId}");
            
            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get the full message details
                var message = await gmailService.Users.Messages.Get("me", messageId).ExecuteAsync();
                
                // Check if this is an email from me to me
                string fromAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                string toAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "To")?.Value ?? "";
                
                if (fromAddress.Contains(_authConfig.UserEmail) && toAddress.Contains(_authConfig.UserEmail))
                {
                    _logger.LogInformation($"Skipping email from me to me: {messageId}");
                    return;
                }
                
                _logger.LogInformation($"Retrieved message with subject: {message.Payload?.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value}");
                
                // Process for urgent messages
                await ProcessMessageForUrgency(gmailService, message);
                
                // Process for high risk messages
                await ProcessMessageForRisk(gmailService, message);
                
                _logger.LogInformation($"Completed processing for message ID: {messageId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing new email: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        private async Task ProcessMessageForUrgency(Google.Apis.Gmail.v1.GmailService gmailService, Google.Apis.Gmail.v1.Data.Message message)
        {
            try
            {
                // Check if this is an email from me to me
                string fromAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                string toAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "To")?.Value ?? "";
                
                if (fromAddress.Contains(_authConfig.UserEmail) && toAddress.Contains(_authConfig.UserEmail))
                {
                    _logger.LogInformation($"Skipping urgency check for email from me to me: {message.Id}");
                    return;
                }
                
                // Ensure URGENT label exists
                var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();
                var urgentLabel = labels.Labels.FirstOrDefault(l => l.Name == "URGENT");

                if (urgentLabel == null)
                {
                    _logger.LogInformation("Creating URGENT label");
                    var newLabel = new Google.Apis.Gmail.v1.Data.Label
                    {
                        Name = "URGENT",
                        LabelListVisibility = "labelShow",
                        MessageListVisibility = "show"
                    };
                    urgentLabel = await gmailService.Users.Labels.Create(newLabel, "me").ExecuteAsync();
                }

                // Check if message already has URGENT label
                if (message.LabelIds != null && message.LabelIds.Contains(urgentLabel.Id))
                {
                    return;
                }

                // Get message content
                string subject = "";
                string sender = "";
                string body = "";

                if (message.Payload?.Headers != null)
                {
                    subject = message.Payload.Headers
                        .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";
                    sender = message.Payload.Headers
                        .FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                }

                if (message.Payload?.Parts != null)
                {
                    foreach (var part in message.Payload.Parts)
                    {
                        if (part.MimeType == "text/plain" && part.Body?.Data != null)
                        {
                            body = DecodeBase64(part.Body.Data);
                            break;
                        }
                    }
                }
                else if (message.Payload?.Body?.Data != null)
                {
                    body = DecodeBase64(message.Payload.Body.Data);
                }

                // Use OpenAI to analyze the email
                if (await _openAiService.IsEmailUrgentAsync(subject, body, sender))
                {
                    _logger.LogInformation($"Adding URGENT label to message with subject: {subject}");
                    
                    var modifyRequest = new Google.Apis.Gmail.v1.Data.ModifyMessageRequest
                    {
                        AddLabelIds = new[] { urgentLabel.Id }
                    };
                    
                    await gmailService.Users.Messages.Modify(modifyRequest, "me", message.Id).ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message for urgency: {ex.Message}");
            }
        }

        private async Task ProcessMessageForRisk(Google.Apis.Gmail.v1.GmailService gmailService, Google.Apis.Gmail.v1.Data.Message message)
        {
            try
            {
                // Check if this is an email from me to me
                string fromAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
                string toAddress = message.Payload?.Headers?.FirstOrDefault(h => h.Name == "To")?.Value ?? "";
                
                if (fromAddress.Contains(_authConfig.UserEmail) && toAddress.Contains(_authConfig.UserEmail))
                {
                    _logger.LogInformation($"Skipping risk check for email from me to me: {message.Id}");
                    return;
                }
                
                // Ensure labels exist
                var highRiskLabel = await EnsureLabelExists(gmailService, "HIGH_RISK");
                var moderateRiskLabel = await EnsureLabelExists(gmailService, "MODERATE_RISK");
                
                // Skip if message already has risk labels
                if (message.LabelIds != null && 
                    (message.LabelIds.Contains(highRiskLabel.Id) || 
                     message.LabelIds.Contains(moderateRiskLabel.Id)))
                {
                    return;
                }

                // Extract message content
                var (subject, sender, body, attachments) = ExtractMessageContent(message);

                // Analyze the email for risks
                var riskAnalysis = await _openAiService.AnalyzeEmailRiskAsync(subject, body, sender, attachments);

                if (riskAnalysis.RiskLevel != EmailRiskLevel.None)
                {
                    var labelId = riskAnalysis.RiskLevel == EmailRiskLevel.High 
                        ? highRiskLabel.Id 
                        : moderateRiskLabel.Id;

                    _logger.LogInformation($"Adding {riskAnalysis.RiskLevel} label to message with subject: {subject}");
                    
                    var modifyRequest = new Google.Apis.Gmail.v1.Data.ModifyMessageRequest
                    {
                        AddLabelIds = new[] { labelId }
                    };
                    
                    await gmailService.Users.Messages.Modify(modifyRequest, "me", message.Id).ExecuteAsync();

                    // Only send notification email for HIGH_RISK messages
                    if (riskAnalysis.RiskLevel == EmailRiskLevel.High)
                    {
                        await SendRiskNotificationEmail(gmailService, message, sender, subject, riskAnalysis.Explanation);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message for risk: {ex.Message}");
            }
        }

        [Function("StopPushNotifications")]
        public async Task<HttpResponseData> StopPushNotifications(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Stopping Gmail push notifications");

            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Stop watching for notifications
                await gmailService.Users.Stop("me").ExecuteAsync();
                
                _logger.LogInformation("Successfully stopped watching for notifications");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync($@"
                    <html><body>
                        <h1>Push Notifications Stopped</h1>
                        <p>Successfully stopped Gmail push notifications!</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping push notifications: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync($@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Failed to stop push notifications: {ex.Message}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("InitializeHistoryId")]
        public async Task<HttpResponseData> InitializeHistoryId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Initializing Gmail history ID");

            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get user profile to get email address
                var profile = await gmailService.Users.GetProfile("me").ExecuteAsync();
                string emailAddress = profile.EmailAddress;
                
                // Initialize the history ID
                await _historyIdStorage.InitializeHistoryIdAsync(emailAddress, _authService);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync($@"
                    <html><body>
                        <h1>History ID Initialized</h1>
                        <p>Successfully initialized history ID for {emailAddress}.</p>
                        <p>Current history ID: {profile.HistoryId}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing history ID: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync($@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Failed to initialize history ID: {ex.Message}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }

        [Function("RenewPushNotifications")]
        public async Task RenewPushNotifications([TimerTrigger("0 0 12 * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"RenewPushNotifications function executed at: {DateTime.Now}");
            
            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get user profile to get email address
                var profile = await gmailService.Users.GetProfile("me").ExecuteAsync();
                string emailAddress = profile.EmailAddress;
                
                _logger.LogInformation($"Renewing push notifications for {emailAddress}");
                
                // Get the topic name from environment variables
                var topicName = Environment.GetEnvironmentVariable("GMAIL_PUBSUB_TOPIC");
                if (string.IsNullOrEmpty(topicName))
                {
                    _logger.LogError("GMAIL_PUBSUB_TOPIC environment variable is not set");
                    return;
                }
                
                // Create a watch request for the user's inbox
                var watchRequest = new Google.Apis.Gmail.v1.Data.WatchRequest
                {
                    TopicName = topicName,
                    LabelIds = new List<string> { "INBOX" }
                };
                
                var watchResponse = await gmailService.Users.Watch(watchRequest, "me").ExecuteAsync();
                
                // Calculate expiration time
                var expirationTime = watchResponse.Expiration.HasValue 
                    ? DateTime.UnixEpoch.AddMilliseconds(watchResponse.Expiration.Value) 
                    : DateTime.UtcNow.AddDays(7);
                
                // Store the historyId from the watch response if available
                if (watchResponse.HistoryId.HasValue)
                {
                    await _historyIdStorage.StoreLastHistoryIdAsync(emailAddress, watchResponse.HistoryId.Value);
                    _logger.LogInformation($"Updated historyId to {watchResponse.HistoryId.Value} from watch response");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renewing push notifications: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        [Function("RenewPushNotificationsHttp")]
        public async Task<HttpResponseData> RenewPushNotificationsHttp(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Manual renewal of Gmail push notifications requested");

            try
            {
                var gmailService = await _authService.CreateGmailService();
                
                // Get user profile to get email address
                var profile = await gmailService.Users.GetProfile("me").ExecuteAsync();
                string emailAddress = profile.EmailAddress;
                
                // Get the topic name from environment variables
                var topicName = Environment.GetEnvironmentVariable("GMAIL_PUBSUB_TOPIC");
                if (string.IsNullOrEmpty(topicName))
                {
                    _logger.LogError("GMAIL_PUBSUB_TOPIC environment variable is not set");
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                    await errorResponse.WriteStringAsync($@"
                        <html><body>
                            <h1>Error</h1>
                            <p>GMAIL_PUBSUB_TOPIC environment variable is not set</p>
                            <a href='/api/GmailAgentHome'>Return to Home</a>
                        </body></html>");
                    return errorResponse;
                }
                
                // Create a watch request for the user's inbox
                var watchRequest = new Google.Apis.Gmail.v1.Data.WatchRequest
                {
                    TopicName = topicName,
                    LabelIds = new List<string> { "INBOX" }
                };
                
                var watchResponse = await gmailService.Users.Watch(watchRequest, "me").ExecuteAsync();
                
                // Calculate expiration time
                var expirationTime = watchResponse.Expiration.HasValue 
                    ? DateTime.UnixEpoch.AddMilliseconds(watchResponse.Expiration.Value) 
                    : DateTime.UtcNow.AddDays(7);
                
                // Store the historyId from the watch response if available
                if (watchResponse.HistoryId.HasValue)
                {
                    await _historyIdStorage.StoreLastHistoryIdAsync(emailAddress, watchResponse.HistoryId.Value);
                    _logger.LogInformation($"Updated historyId to {watchResponse.HistoryId.Value} from watch response");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await response.WriteStringAsync($@"
                    <html><body>
                        <h1>Push Notifications Renewed</h1>
                        <p>Successfully renewed Gmail push notifications!</p>
                        <p>Email: {emailAddress}</p>
                        <p>Topic: {topicName}</p>
                        <p>History ID: {(watchResponse.HistoryId.HasValue ? watchResponse.HistoryId.Value.ToString() : "Not available")}</p>
                        <p>Expiration: {expirationTime}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renewing push notifications: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/html; charset=utf-8");
                await errorResponse.WriteStringAsync($@"
                    <html><body>
                        <h1>Error</h1>
                        <p>Failed to renew push notifications: {ex.Message}</p>
                        <a href='/api/GmailAgentHome'>Return to Home</a>
                    </body></html>");
                return errorResponse;
            }
        }
    }

    // Timer trigger info class
    public class MyInfo
    {
        public bool IsPastDue { get; set; }
        public ScheduleStatus ScheduleStatus { get; set; }
    }

    public class ScheduleStatus
    {
        public DateTime Last { get; set; }
        public DateTime Next { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
