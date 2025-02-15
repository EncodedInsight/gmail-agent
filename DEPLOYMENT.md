# Deployment Guide for Gmail Agent

This guide provides detailed instructions for deploying the Gmail Agent to Azure Functions.

## Prerequisites

- Azure subscription
- Azure CLI installed and configured
- .NET 8.0 SDK
- Google Cloud Platform account with Gmail API enabled
- GitHub account

## Step 1: Prepare Your Google Cloud Project

1. Create a project in the [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the Gmail API for your project
3. Configure the OAuth consent screen:
   - Set the user type (External or Internal)
   - Add required scopes:
     - `https://www.googleapis.com/auth/gmail.readonly`
     - `https://www.googleapis.com/auth/gmail.modify`
     - `https://www.googleapis.com/auth/gmail.labels`
4. Create OAuth 2.0 credentials (Web application type)
5. Add authorized redirect URIs:
   - For local development: `http://localhost:7071/api/GmailAgentRedirectHandler`
   - For production: `https://your-function-app-name.azurewebsites.net/api/GmailAgentRedirectHandler`

## Step 2: Set Up Push Notifications (Optional)

If you want to use Gmail push notifications:

1. Set up a Google Cloud Pub/Sub topic:
   ```bash
   gcloud pubsub topics create gmail-notifications
   ```
2. Create a subscription for the topic:
   ```bash
   gcloud pubsub subscriptions create gmail-notifications-sub --topic=gmail-notifications
   ```
3. Note your topic name: `projects/your-project-id/topics/gmail-notifications`

## Step 3: Create Azure Resources

1. Create a Resource Group:
   ```bash
   az group create --name YourResourceGroup --location eastus
   ```

2. Create a Storage Account:
   ```bash
   az storage account create --name yourstorageaccount --location eastus --resource-group YourResourceGroup --sku Standard_LRS
   ```

3. Get the Storage Account connection string:
   ```bash
   az storage account show-connection-string --name yourstorageaccount --resource-group YourResourceGroup
   ```

4. Create a Function App:
   ```bash
   az functionapp create --resource-group YourResourceGroup --consumption-plan-location eastus --runtime dotnet-isolated --functions-version 4 --name your-function-app-name --storage-account yourstorageaccount --os-type Windows
   ```

## Step 4: Configure Application Settings

Set the required application settings for your Function App:

```bash
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "GOOGLE_CLIENT_ID=your-google-client-id"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "GOOGLE_CLIENT_SECRET=your-google-client-secret"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "OPENAI_API_KEY=your-openai-api-key"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "AZURE_OPENAI_ENDPOINT=your-azure-openai-endpoint"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "AZURE_OPENAI_KEY=your-azure-openai-key"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "OPENAI_MODEL_NAME=gpt-4o-mini"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "OPENAI_API_TIMEOUT_SECONDS=10"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "APPLICATION_NAME=Gmail Agent"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "GMAIL_PUBSUB_TOPIC=projects/your-project-id/topics/your-topic-name"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "BLOB_CONTAINER_NAME=your-blob-container-name"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "GOOGLE_REDIRECT_URI=https://your-function-app-name.azurewebsites.net/api/GmailAgentRedirectHandler"
az functionapp config appsettings set --name your-function-app-name --resource-group YourResourceGroup --settings "USER_EMAIL=your-email@example.com"
```

## Step 5: Deploy the Function App with GitHub Actions (CI/CD)

1. Fork or clone this repository to your GitHub account
2. In your GitHub repository, go to Settings > Secrets and variables > Actions
3. Add the following repository secrets:
   - `AZURE_FUNCTIONAPP_NAME`: Your Azure Function App name
   - `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: Your publish profile from Azure Portal
     - To get this, go to your Function App in Azure Portal
     - Click "Get publish profile" and copy the contents

4. The repository already includes a GitHub Actions workflow file at `.github/workflows/main_gmailagent.yml`
5. Push any changes to your repository to trigger the deployment workflow

The GitHub Actions workflow will automatically:
- Build the project
- Run tests (if any)
- Deploy to your Azure Function App

## Step 6: Configure CORS (if needed)

If you're accessing the Function App from a web application:

```bash
az functionapp cors add --name your-function-app-name --resource-group YourResourceGroup --allowed-origins "https://your-web-app-domain.com"
```

## Step 7: Test the Deployment

1. Navigate to `https://your-function-app-name.azurewebsites.net/api/GmailAgentHome`
2. Follow the authentication flow to connect your Gmail account
3. Set up push notifications if desired

## Troubleshooting

- Check Application Insights logs for errors
- Verify that all environment variables are correctly set
- Ensure that the Google OAuth credentials are properly configured
- Check that the redirect URIs match your deployed Function App URL

## Maintenance

- Push notifications are automatically renewed before expiration (every 7 days)
- Monitor Azure Function execution and adjust the consumption plan if needed