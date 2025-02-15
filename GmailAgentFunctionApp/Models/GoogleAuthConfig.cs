namespace GmailAgentFunctionApp.Models
{
    public class GoogleAuthConfig
    {
        public string ClientId { get; set; } = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
        public string ClientSecret { get; set; } = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";
        public string RedirectUri { get; set; } = Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI") 
            ?? throw new ArgumentNullException("GOOGLE_REDIRECT_URI is not set");
        public string UserEmail { get; set; } = Environment.GetEnvironmentVariable("USER_EMAIL") 
            ?? throw new ArgumentNullException("USER_EMAIL is not set");
        
        public static readonly string[] Scopes = new[]
        {
            "https://www.googleapis.com/auth/gmail.readonly",
            "https://www.googleapis.com/auth/gmail.labels",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/userinfo.profile",
            "https://www.googleapis.com/auth/gmail.modify",
            "https://www.googleapis.com/auth/gmail.settings.basic"
        };
    }
} 