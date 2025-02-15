using System.Text.Json.Serialization;

namespace GmailAgentFunctionApp.Models
{
    /// <summary>
    /// Represents a push notification from Gmail via Google Cloud Pub/Sub
    /// </summary>
    public class GmailPushNotification
    {
        [JsonPropertyName("message")]
        public PubSubMessage? Message { get; set; }
        
        [JsonPropertyName("subscription")]
        public string? Subscription { get; set; }
    }

    /// <summary>
    /// Represents a Google Cloud Pub/Sub message
    /// </summary>
    public class PubSubMessage
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }
        
        [JsonPropertyName("messageId")]
        public string? MessageId { get; set; }
        
        [JsonPropertyName("publishTime")]
        public string? PublishTime { get; set; }
        
        [JsonPropertyName("attributes")]
        public Dictionary<string, string>? Attributes { get; set; }
    }

    /// <summary>
    /// Represents the decoded data from a Gmail notification
    /// </summary>
    public class GmailEmailNotificationData
    {
        [JsonPropertyName("emailAddress")]
        public string EmailAddress { get; set; } = string.Empty;
        
        [JsonPropertyName("email")]
        public string EmailAlt { get; set; } = string.Empty;
        
        [JsonPropertyName("emailId")]
        public string EmailId { get; set; } = string.Empty;
        
        [JsonPropertyName("messageId")]
        public string MessageId { get; set; } = string.Empty;
        
        [JsonPropertyName("historyId")]
        public ulong HistoryId { get; set; }
        
        // Helper method to get historyId as string
        public string GetHistoryIdString() => HistoryId.ToString();
        
        // Helper method to get the best available email address
        public string GetEmailAddress() => !string.IsNullOrEmpty(EmailAddress) ? EmailAddress : EmailAlt;
        
        // Helper method to get the best available message ID
        public string GetMessageId() => !string.IsNullOrEmpty(EmailId) ? EmailId : MessageId;
    }
} 