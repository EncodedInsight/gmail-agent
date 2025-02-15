# Gmail Agent API Documentation

This document provides details about the HTTP endpoints available in the Gmail Agent application.

## Authentication Endpoints

### Home Page

Returns the main web interface for the Gmail Agent.

- **URL**: `/api/GmailAgentHome`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page with links to other functions

### Authorize with Gmail

Initiates the OAuth flow with Google.

- **URL**: `/api/GmailAgentAuth`
- **Method**: `GET`
- **Authentication Required**: No
- **Query Parameters**:
  - `redirect_uri` (optional): Custom redirect URI after authentication
- **Response**: Redirects to Google OAuth consent screen

### OAuth Redirect Handler

Handles the OAuth callback from Google.

- **URL**: `/api/GmailAgentRedirectHandler`
- **Method**: `GET`
- **Authentication Required**: No
- **Query Parameters**:
  - `code`: Authorization code from Google
  - `state`: State parameter for security validation
- **Response**: HTML page confirming successful authentication

### Logout

Logs out the current user by clearing stored tokens.

- **URL**: `/api/GmailAgentLogout`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page confirming logout

## Email Processing Endpoints

### Process Urgent Messages (HTTP Trigger)

Manually triggers the processing of urgent messages.

- **URL**: `/api/ProcessUrgentMessagesHttp`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page with processing results

### Process High Risk Messages (HTTP Trigger)

Manually triggers the processing of high-risk messages.

- **URL**: `/api/ProcessHighRiskMessagesHttp`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page with processing results

## Push Notification Endpoints

### Setup Push Notifications

Sets up Gmail push notifications for real-time email processing.

- **URL**: `/api/SetupPushNotifications`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page confirming setup with expiration details

### Stop Push Notifications

Stops Gmail push notifications.

- **URL**: `/api/StopPushNotifications`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page confirming notifications have been stopped

### Renew Push Notifications (HTTP Trigger)

Manually renews Gmail push notifications before they expire.

- **URL**: `/api/RenewPushNotificationsHttp`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page confirming renewal with new expiration details

### Push Notification Handler

Webhook endpoint that receives push notifications from Gmail.

- **URL**: `/api/GmailPushNotificationHandler`
- **Method**: `POST`
- **Authentication Required**: No
- **Request Body**: JSON payload from Gmail push notification
- **Response**: 200 OK if processed successfully

## History Management

### Initialize History ID

Initializes the history ID for tracking email changes.

- **URL**: `/api/InitializeHistoryId`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page confirming history ID initialization

## Testing Endpoints

### Test Gmail Authentication

Tests if the current authentication is working.

- **URL**: `/api/GmailAgentTest`
- **Method**: `GET`
- **Authentication Required**: No
- **Response**: HTML page with authentication status and user information

## Timer-Triggered Functions

These functions are not accessible via HTTP but run on a schedule:

1. **GmailAgentFunction**: Runs daily at 10 PM
2. **ProcessUrgentMessages**: Runs hourly
3. **ProcessHighRiskMessages**: Runs hourly
4. **RenewPushNotifications**: Runs daily at noon

## Response Formats

Most endpoints return HTML responses for browser interaction. The typical structure is:

```html
<html>
<body>
    <h1>Title</h1>
    <p>Status message</p>
    <a href="/api/GmailAgentHome">Return to Home</a>
</body>
</html>
```

## Error Handling

All endpoints return appropriate HTTP status codes:

- **200 OK**: Operation completed successfully
- **400 Bad Request**: Invalid parameters
- **401 Unauthorized**: Authentication required
- **500 Internal Server Error**: Server-side error

Error responses include a message explaining the issue.

## Security Considerations

- The application uses OAuth 2.0 for authentication with Gmail
- Tokens are stored securely in Azure Blob Storage
- No sensitive information is exposed in URLs
- All HTTP endpoints use HTTPS in production 