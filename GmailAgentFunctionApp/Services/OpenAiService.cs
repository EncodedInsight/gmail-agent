using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace GmailAgentFunctionApp.Services
{
    public enum EmailRiskLevel
    {
        None,
        Moderate,
        High
    }

    public class RiskAnalysisResult
    {
        public EmailRiskLevel RiskLevel { get; set; }
        public string Explanation { get; set; }
    }

    public class OpenAiService
    {
        private readonly AzureOpenAIClient _client;
        private readonly ChatClient _chatClient;
        private readonly ILogger<OpenAiService> _logger;
        private readonly int _apiTimeoutSeconds;

        public OpenAiService(ILogger<OpenAiService> logger)
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
                ?? throw new ArgumentNullException("AZURE_OPENAI_ENDPOINT is not set");
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") 
                ?? throw new ArgumentNullException("AZURE_OPENAI_KEY is not set");
            var modelName = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME") 
                ?? "gpt-4o-mini";
            
            // Parse timeout from environment variable or use default of 10 seconds
            if (!int.TryParse(Environment.GetEnvironmentVariable("OPENAI_API_TIMEOUT_SECONDS"), out _apiTimeoutSeconds))
            {
                _apiTimeoutSeconds = 10;
            }

            _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
            _chatClient = _client.GetChatClient(modelName);
            _logger = logger;
        }

        public async Task<bool> IsEmailUrgentAsync(string subject, string body, string sender)
        {
            _logger.LogInformation($"IsEmailUrgentAsync Subject: {subject}");

            try
            {
                var systemMessage = @"You are an email urgency analyzer. 
                    Your task is to determine if an email requires immediate attention or urgent response.
                    Do not give this designation out unless the email is clearly urgent and requires immediate action.

                    Consider factors such as:
                    - The sender's identity and importance
                    - Time-sensitive language or deadlines
                    - Critical business impact
                    - Financial or security implications
                    Respond with only 'true' if urgent or 'false' if not urgent.";

                var userMessage = $"Please analyze this email:\nFrom: {sender}\nSubject: {subject}\n\nBody:\n{body}";

                // Log the length of the user message
                _logger.LogInformation($"User Message Length: {userMessage.Length}");

                // Use configurable timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(_apiTimeoutSeconds));

                var completion = await _chatClient.CompleteChatAsync([
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(userMessage)
                ], cancellationToken: cancellationToken.Token);

                _logger.LogInformation($"Completion Status: {completion.GetRawResponse().Status}");

                if (completion.GetRawResponse().Status != 200)
                {
                    _logger.LogError($"Error analyzing email urgency: {completion.GetRawResponse().ReasonPhrase}");
                    return false;
                }

                _logger.LogInformation($"Urgency Analysis Completion: {completion.Value.Content[0].Text}");

                return completion.Value.Content[0].Text.Trim().ToLower() == "true";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing email urgency: {ex.Message}");
                return false;
            }
        }

        public async Task<RiskAnalysisResult> AnalyzeEmailRiskAsync(string subject, string body, string sender, string attachments)
        {
            _logger.LogInformation($"AnalyzeEmailRiskAsync Subject: {subject}");

            try
            {
                var systemMessage = @"You are an email security analyzer. 
                    Your task is to determine if an email poses potential security risks.
                    Consider factors such as:
                    - Potential phishing attempts
                    - Suspicious sender domains or addresses
                    - Unusual or suspicious attachments
                    - Links to suspicious domains
                    - Social engineering tactics
                    - Urgency or pressure tactics
                    - Poor grammar or typical scam language patterns
                    - Requests for sensitive information
                    
                    Categorize the risk level as follows:
                    HIGH_RISK: Clear and immediate security threats such as:
                    - Obvious phishing attempts
                    - Malicious attachments
                    - Requests for credentials or sensitive data
                    - Known scam patterns
                    
                    MODERATE_RISK: Potential concerns that require attention such as:
                    - Unusual but not clearly malicious requests
                    - Slightly suspicious sender addresses
                    - Requests for information that seem questionable
                    - Moderate pressure tactics
                    
                    NO_RISK: Normal business communications with no suspicious elements.
                    
                    Respond with exactly one of: 'HIGH_RISK', 'MODERATE_RISK', or 'NO_RISK', followed by a newline and 
                    a bullet-point explanation of specific risks identified (if any risks exist).";

                var userMessage = $"Please analyze this email:\nFrom: {sender}\nSubject: {subject}\nAttachments: {attachments}\n\nBody:\n{body}";

                // Use configurable timeout
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(_apiTimeoutSeconds));

                var completion = await _chatClient.CompleteChatAsync([
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(userMessage)
                ], cancellationToken: cancellationToken.Token);

                if (completion.GetRawResponse().Status != 200)
                {
                    _logger.LogError($"Error analyzing email risk: {completion.GetRawResponse().ReasonPhrase}");
                    return new RiskAnalysisResult { RiskLevel = EmailRiskLevel.None, Explanation = string.Empty };
                }

                var response = completion.Value.Content[0].Text.Trim();
                var parts = response.Split('\n', 2);
                var riskLevel = parts[0].Trim();
                var explanation = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                return new RiskAnalysisResult 
                { 
                    RiskLevel = riskLevel switch
                    {
                        "HIGH_RISK" => EmailRiskLevel.High,
                        "MODERATE_RISK" => EmailRiskLevel.Moderate,
                        _ => EmailRiskLevel.None
                    },
                    Explanation = explanation
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing email risk: {ex.Message}");
                return new RiskAnalysisResult { RiskLevel = EmailRiskLevel.None, Explanation = string.Empty };
            }
        }
    }
} 