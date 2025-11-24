# OAuth Authentication Implementation

## Overview

This implementation adds OAuth 2.0 authentication to the Raumbuch application, replacing the manual access token entry field with an automated login flow through Trimble Connect.

## Features

- **Automated Login**: Users click a button to log in via Trimble Connect
- **Popup Window**: OAuth flow happens in a popup window for better UX
- **Session Management**: Access tokens are stored in browser sessionStorage
- **Token Refresh**: Support for refreshing expired tokens
- **Logout**: Clean logout that clears both session and local storage

## Components

### Backend

1. **TrimbleAuthService.cs** (`/Services/TrimbleAuthService.cs`)
   - Handles OAuth authorization URL generation
   - Exchanges authorization codes for access tokens
   - Refreshes access tokens using refresh tokens

2. **AuthController.cs** (`/Controllers/AuthController.cs`)
   - `/api/auth/login` - Returns OAuth authorization URL
   - `/api/auth/callback` - Handles OAuth callback and token exchange
   - `/api/auth/token` - Returns current access token from session
   - `/api/auth/refresh` - Refreshes expired access tokens
   - `/api/auth/logout` - Clears session data

### Frontend

1. **UI Changes** (index.html)
   - Replaced text input field with "Mit Trimble anmelden" button
   - Added logout button and authentication status display
   - Shows current authentication state (logged in/out)

2. **JavaScript Functions**
   - `loginWithTrimble()` - Opens OAuth popup window
   - `logoutFromTrimble()` - Clears tokens and logs out
   - `getToken()` - Retrieves token from sessionStorage (instead of input field)
   - `checkOAuthCallback()` - Handles callback parameters on page load
   - `updateAuthStatus()` - Updates UI based on authentication state

## Configuration

### Required Settings (Web.config or Azure App Settings)

```xml
<add key="TRIMBLE_CLIENT_ID" value="073a84b7-323b-43bf-b5a9-96bf17638dcc" />
<add key="TRIMBLE_CLIENT_SECRET" value="YOUR_CLIENT_SECRET" />
<add key="TRIMBLE_AUTH_URL" value="https://id.trimble.com/oauth/authorize" />
<add key="TRIMBLE_TOKEN_URL" value="https://id.trimble.com/oauth/token" />
<add key="TRIMBLE_SCOPE" value="openid CST-PowerBI" />
<add key="TRIMBLE_REDIRECT_URI" value="http://localhost:5005/callback/" />
```

**Note**: The `TRIMBLE_REDIRECT_URI` in the config is a fallback value. The actual redirect URI used is dynamically constructed based on the server's URL (e.g., `https://yourdomain.com/api/auth/callback`).

### Session State

Session state is enabled in Web.config:

```xml
<sessionState mode="InProc" timeout="60" />
```

This is required to store OAuth state parameters and tokens on the server side.

## OAuth Flow

1. **User clicks "Mit Trimble anmelden"**
   - Frontend calls `/api/auth/login`
   - Backend generates OAuth URL with state parameter
   - Frontend opens OAuth URL in popup window

2. **User logs in to Trimble Connect**
   - User authenticates in popup window
   - Trimble redirects to `/api/auth/callback` with authorization code

3. **Backend exchanges code for tokens**
   - Backend validates state parameter (CSRF protection)
   - Backend exchanges authorization code for access token
   - Backend stores tokens in server session
   - Backend redirects to main page with success flag only

4. **Frontend retrieves token**
   - Frontend detects success callback parameter
   - Frontend calls `/api/auth/token` to retrieve token securely
   - Frontend stores token in sessionStorage
   - Frontend updates UI to show logged-in state
   - User can now use all API features

## Security Features

- **CSRF Protection**: Random state parameter validated on callback
- **Session Storage**: Tokens stored in sessionStorage (cleared on browser close)
- **Server-Side Session**: Tokens also stored in server session
- **HTTPS Required**: OAuth flow requires HTTPS in production
- **No Tokens in URLs**: Access tokens never passed as URL parameters (prevents logging/sharing)
- **Generic Error Codes**: Error messages use generic codes to avoid information leakage
- **Connection Pooling**: Static HttpClient instance prevents socket exhaustion

## Deployment Notes

### Azure App Service

When deploying to Azure App Service:

1. Set all `TRIMBLE_*` environment variables in App Service Configuration
2. Ensure the registered redirect URI matches your Azure domain:
   - Format: `https://yourdomain.azurewebsites.net/api/auth/callback`
3. Update Trimble Connect app registration with the correct redirect URI

### Trimble Connect App Registration

In your Trimble Connect app registration:

1. Add redirect URI: `https://yourdomain.com/api/auth/callback`
2. For local testing: `http://localhost:[port]/api/auth/callback`
3. Ensure scopes include: `openid` and your app scope (e.g., `CST-PowerBI`)

## Testing

### Local Testing

1. Start the application locally (IIS Express or Kestrel)
2. Navigate to `http://localhost:[port]/index.html`
3. Click "Mit Trimble anmelden"
4. Complete login in popup window
5. Verify token is stored and UI shows logged-in state
6. Test API calls to ensure token is sent correctly

### Troubleshooting

**Popup blocked**: 
- Allow popups for the application domain
- Check browser popup settings

**State validation failed**:
- Ensure cookies are enabled
- Check that session state is properly configured

**Token exchange failed**:
- Verify client ID and client secret are correct
- Check that redirect URI matches exactly (including trailing slash)
- Verify network connectivity to Trimble OAuth server

**Token not persisting**:
- Check browser console for JavaScript errors
- Verify sessionStorage is enabled in browser
- Check that OAuth callback parameters are being received

## Migration Notes

### For Existing Users

Users who previously entered access tokens manually will need to:

1. Log out (clear any manually entered tokens)
2. Use the new "Mit Trimble anmelden" button
3. Complete OAuth login flow

### Backward Compatibility

The implementation maintains compatibility with existing API calls:
- `getToken()` function returns token from sessionStorage
- All API calls continue to use `accessToken` in request body
- No changes needed to existing API endpoints

## Future Enhancements

Possible improvements for future versions:

1. **Automatic Token Refresh**: Automatically refresh tokens before expiration
2. **Remember Me**: Option to persist tokens beyond session
3. **Multi-Project Support**: Store tokens per project
4. **Token Validation**: Validate token on page load and prompt re-login if expired
5. **Progress Indicators**: Better visual feedback during OAuth flow
