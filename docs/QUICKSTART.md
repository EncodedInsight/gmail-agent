# Gmail Agent Quick Start Guide

This guide will help you get the Gmail Agent up and running quickly. For more detailed information, refer to the full README and documentation.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azurite](https://github.com/Azure/Azurite) for local storage emulation (optional)
- Google account with access to Gmail
- GitHub account (for deployment)

## 5-Minute Setup

### 1. Clone and Configure

```bash
# Clone the repository
git clone https://github.com/yourusername/gmail-agent.git
cd gmail-agent

# Copy the example settings file
cp GmailAgentFunctionApp/local.settings.example.json GmailAgentFunctionApp/local.settings.json
```

### 2. Set Up Google API Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable the Gmail API
4. Create OAuth credentials (Web application type)
5. Add redirect URI: `http://localhost:7071/api/GmailAgentRedirectHandler`
6. Note your Client ID and Client Secret

### 3. Update Configuration

Edit `GmailAgentFunctionApp/local.settings.json` and add your Google credentials:

```json
{
  "Values": {
    "GOOGLE_CLIENT_ID": "your-client-id-here",
    "GOOGLE_CLIENT_SECRET": "your-client-secret-here",
    "OPENAI_MODEL_NAME": "gpt-4o-mini",
    "OPENAI_API_TIMEOUT_SECONDS": "10",
    "APPLICATION_NAME": "Gmail Agent",
    "GOOGLE_REDIRECT_URI": "http://localhost:7071/api/GmailAgentRedirectHandler"
  }
}
```

### 4. Run the Application

```bash
# Start Azurite (in a separate terminal)
azurite --silent --location ./__blobstorage__ --debug ./__debug.log

# Run the function app
cd GmailAgentFunctionApp
func start
```

### 5. Authenticate and Set Up

1. Open your browser and go to: http://localhost:7071/api/GmailAgentHome
2. Click "Authorize with Gmail" and complete the OAuth flow
3. Click "Setup Push Notifications" to enable real-time email processing (this also initializes the history ID for tracking email changes)

## What's Next?

- The application will now process your emails automatically
- Urgent emails will be labeled with "URGENT"
- High-risk emails will be labeled with "HIGH_RISK"
- Check the logs for processing information

## Deploying to Production

For production deployment, we use GitHub Actions:

1. Fork this repository to your GitHub account
2. Create Azure resources as described in the [Deployment Guide](../DEPLOYMENT.md)
3. Set up GitHub secrets:
   - `AZURE_FUNCTIONAPP_NAME`: Your Azure Function App name
   - `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: Your publish profile from Azure Portal
4. Push changes to your repository to trigger the deployment

## Troubleshooting

### Common Issues

1. **OAuth Error**: Ensure your redirect URI exactly matches what's in Google Cloud Console
2. **Storage Error**: Make sure Azurite is running or you have a valid Azure Storage connection
3. **Function Not Found**: Ensure you're running the app from the GmailAgentFunctionApp directory

### Getting Help

If you encounter issues:
1. Check the terminal output for error messages
2. Review the full documentation in the README
3. Open an issue on GitHub with details about your problem

## Next Steps

- Review the [API Documentation](./API.md) to understand available endpoints
- Check the [Deployment Guide](../DEPLOYMENT.md) for production deployment
- Explore the [Architecture Documentation](./ARCHITECTURE.md) to understand how it works 