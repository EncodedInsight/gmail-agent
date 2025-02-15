# Gmail Agent - Intelligent Email Processing with Azure Functions

This project provides an Azure Functions-based solution for intelligent Gmail message processing using AI. It can automatically categorize urgent messages, identify high-risk emails, and set up real-time notifications for new emails.

## Features

- **OAuth Authentication**: Secure Gmail API access using OAuth 2.0
- **Urgent Message Detection**: AI-powered identification of time-sensitive emails
- **High-Risk Email Detection**: Identification of potentially dangerous or suspicious emails
- **Real-time Notifications**: Gmail push notifications for immediate processing of new emails
- **Scheduled Processing**: Regular background processing of emails
- **Web Interface**: Simple web UI for authentication and configuration

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/) (or Azurite for local development)
- [Google Cloud Platform Account](https://cloud.google.com) with Gmail API enabled
- [OpenAI API Key](https://platform.openai.com/) or Azure OpenAI Service
- [GitHub Account](https://github.com) for deployment

## Setup Instructions

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/gmail-agent.git
cd gmail-agent
```

### 2. Google API Setup

1. Create a project in the [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the Gmail API for your project
3. Configure the OAuth consent screen
4. Create OAuth 2.0 credentials (Web application type)
5. Add authorized redirect URIs:
   - For local development: `http://localhost:7071/api/GmailAgentRedirectHandler`
   - For production: `https://your-function-app-name.azurewebsites.net/api/GmailAgentRedirectHandler`

### 3. Configure Environment Variables

Create a `local.settings.json` file in the GmailAgentFunctionApp directory with the following content:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "GOOGLE_CLIENT_ID": "your-google-client-id",
    "GOOGLE_CLIENT_SECRET": "your-google-client-secret",
    "OPENAI_API_KEY": "your-openai-api-key",
    "AZURE_OPENAI_ENDPOINT": "your-azure-openai-endpoint",
    "AZURE_OPENAI_KEY": "your-azure-openai-key",
    "OPENAI_MODEL_NAME": "gpt-4o-mini",
    "OPENAI_API_TIMEOUT_SECONDS": "10",
    "APPLICATION_NAME": "Gmail Agent",
    "GMAIL_PUBSUB_TOPIC": "projects/your-project-id/topics/gmail-notifications",
    "GOOGLE_REDIRECT_URI": "http://localhost:7071/api/GmailAgentRedirectHandler"
  },
  "Host": {
    "LocalHttpPort": 7071,
    "CORS": "*"
  }
}
```

### 4. Run Locally

1. Start Azurite for local storage emulation (if not using a real Azure Storage account):
   ```bash
   azurite --silent --location ./__blobstorage__ --debug ./__debug.log
   ```

2. Run the function app:
   ```bash
   cd GmailAgentFunctionApp
   func start
   ```

3. Navigate to `http://localhost:7071/api/GmailAgentHome` to access the web interface

### 5. Deploy to Azure

1. Create an Azure Function App in the Azure Portal
2. Configure application settings with the same environment variables as in local.settings.json
3. Deploy using GitHub Actions:
   - Fork this repository to your GitHub account
   - Set up the required GitHub secrets:
     - `AZURE_FUNCTIONAPP_NAME`: Your Azure Function App name
     - `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: Your publish profile from Azure Portal
   - Push changes to your repository to trigger the deployment

For detailed deployment instructions, see the [Deployment Guide](DEPLOYMENT.md).

## Usage

1. Access the web interface at `/api/GmailAgentHome`
2. Click "Authorize with Gmail" to authenticate with your Google account
3. Set up push notifications by clicking "Setup Push Notifications"
4. The system will now process your emails automatically

## Architecture

- **GmailAgentFunction**: Main Azure Function class containing all HTTP and timer-triggered functions
- **Services**:
  - **GoogleAuthService**: Handles OAuth authentication with Google
  - **TokenStorageService**: Securely stores OAuth tokens
  - **OpenAiService**: Provides AI capabilities for email analysis
  - **HistoryIdStorageService**: Manages Gmail history IDs for efficient change tracking

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 