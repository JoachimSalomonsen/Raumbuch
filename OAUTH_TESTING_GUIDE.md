# OAuth Authentication Testing Guide

## Prerequisites

Before testing the OAuth implementation, ensure:

1. **Trimble Connect App Registration**
   - Your application is registered with Trimble Connect
   - Client ID and Client Secret are available
   - Redirect URI is configured correctly

2. **Configuration Settings**
   - All `TRIMBLE_*` environment variables are set (see Web.config for defaults)
   - Session state is enabled in Web.config

## Local Testing

### Step 1: Update Trimble App Registration

Add your local redirect URI to Trimble Connect app registration:
- Format: `http://localhost:[YOUR_PORT]/api/auth/callback`
- Example: `http://localhost:5000/api/auth/callback`

### Step 2: Update Web.config (Local Development)

Ensure the following settings in `Web.config`:

```xml
<add key="TRIMBLE_CLIENT_ID" value="YOUR_CLIENT_ID" />
<add key="TRIMBLE_CLIENT_SECRET" value="YOUR_CLIENT_SECRET" />
<add key="TRIMBLE_REDIRECT_URI" value="http://localhost:5000/api/auth/callback" />
```

**Note**: The redirect URI can also be dynamically constructed by the backend based on your server URL.

### Step 3: Start the Application

1. Open the solution in Visual Studio
2. Press F5 to start debugging
3. Note the port number (e.g., `localhost:5000`)
4. Navigate to `http://localhost:[PORT]/index.html`

### Step 4: Test Login Flow

1. **Click "Mit Trimble anmelden" button**
   - A popup window should open
   - You should be redirected to Trimble's login page

2. **Log in with Trimble credentials**
   - Enter your Trimble Connect username and password
   - Authorize the application if prompted

3. **Verify successful login**
   - Popup should close automatically
   - Main window should show:
     - "✓ Angemeldet" status message (green)
     - "Abmelden" button visible
     - "Mit Trimble anmelden" button hidden
   - An alert should confirm: "✓ Erfolgreich angemeldet!"

4. **Verify token is stored**
   - Open browser Developer Tools (F12)
   - Go to Application → Storage → Session Storage
   - Check for `access_token` key with a value

### Step 5: Test API Calls

1. Enter a Project ID
2. Click "📁 Projektdaten laden"
3. Verify that folders are loaded successfully
4. This confirms the token is being used correctly in API calls

### Step 6: Test Logout

1. Click "🚪 Abmelden" button
2. Verify:
   - Alert confirms logout
   - "Mit Trimble anmelden" button is visible again
   - "Abmelden" button is hidden
   - Status message cleared
3. Check Session Storage (F12 → Application):
   - `access_token` should be removed

## Azure Deployment Testing

### Step 1: Configure Azure App Service

Set the following environment variables in Azure App Service Configuration:

```
TRIMBLE_CLIENT_ID = YOUR_CLIENT_ID
TRIMBLE_CLIENT_SECRET = YOUR_CLIENT_SECRET
TRIMBLE_AUTH_URL = https://id.trimble.com/oauth/authorize
TRIMBLE_TOKEN_URL = https://id.trimble.com/oauth/token
TRIMBLE_SCOPE = openid CST-PowerBI
TRIMBLE_REDIRECT_URI = https://yourdomain.azurewebsites.net/api/auth/callback
```

### Step 2: Update Trimble App Registration

Add your Azure redirect URI:
- Format: `https://yourdomain.azurewebsites.net/api/auth/callback`

### Step 3: Deploy and Test

1. Deploy the application to Azure
2. Navigate to `https://yourdomain.azurewebsites.net/index.html`
3. Follow the same testing steps as local testing

## Troubleshooting

### Popup Blocked

**Symptom**: Popup window doesn't open when clicking login button

**Solution**: 
- Allow popups for the application domain
- Check browser settings: Settings → Site Settings → Pop-ups and redirects

### State Validation Failed

**Symptom**: After login, redirected to app with "validation_failed" error

**Possible Causes**:
- Cookies disabled in browser
- Session expired
- Browser privacy settings blocking session

**Solutions**:
1. Enable cookies in browser
2. Check that session state is configured in Web.config
3. Try in incognito/private mode to rule out extensions

### Token Exchange Failed

**Symptom**: After login, redirected to app with "auth_failed" error

**Possible Causes**:
- Incorrect client ID or client secret
- Redirect URI mismatch
- Network connectivity issues

**Solutions**:
1. Verify client ID and secret in configuration
2. Check redirect URI matches exactly (including trailing slash)
3. Check browser console for detailed error messages
4. Check server logs for detailed error information

### Token Not Persisting

**Symptom**: After successful login, API calls fail with authentication errors

**Possible Causes**:
- SessionStorage disabled
- JavaScript errors preventing token storage
- Callback not completing successfully

**Solutions**:
1. Check browser console for JavaScript errors
2. Verify sessionStorage is enabled in browser
3. Check that `/api/auth/token` endpoint returns valid token
4. Clear browser cache and try again

### Server-Side Errors

Check the following server-side logs:

1. **Visual Studio Output Window** (local development)
   - Look for Debug.WriteLine messages
   - Check for exceptions during token exchange

2. **Azure App Service Logs** (production)
   - Enable Application Logging
   - Download log files from Azure portal
   - Check for OAuth-related errors

## API Testing with Postman

To test the OAuth flow programmatically:

### Test Login URL Generation

```
GET http://localhost:5000/api/auth/login
```

Expected response:
```json
{
  "success": true,
  "authUrl": "https://id.trimble.com/oauth/authorize?client_id=...",
  "state": "..."
}
```

### Test Token Retrieval

After completing OAuth flow manually:

```
GET http://localhost:5000/api/auth/token
```

Expected response (if logged in):
```json
{
  "success": true,
  "access_token": "...",
  "expires_at": "2024-..."
}
```

### Test Logout

```
POST http://localhost:5000/api/auth/logout
Content-Type: application/json
```

Expected response:
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

## Security Testing

### Verify Token Not in URL

1. Log in successfully
2. Check browser address bar - should only see `?auth_success=true`
3. Token should NOT be visible in URL
4. Check browser history - no tokens should be stored there

### Verify Generic Error Messages

1. Force an error (e.g., invalid client secret)
2. Check error message in URL - should be generic like `?auth_error=auth_failed`
3. Check server logs for detailed error information

### Verify Session Isolation

1. Log in in one browser tab
2. Open new tab to same application
3. Both tabs should share the same session
4. Logging out in one tab should affect the other

## Success Criteria

The OAuth implementation is working correctly when:

- ✅ Login button opens popup with Trimble login page
- ✅ After successful login, user sees confirmation message
- ✅ Token is stored in sessionStorage
- ✅ API calls work using the OAuth token
- ✅ Logout clears token and updates UI
- ✅ Token is never visible in URL or browser history
- ✅ Error messages are generic (no sensitive information leaked)
- ✅ Session persists across page refreshes
- ✅ Session expires after 60 minutes of inactivity

## Next Steps After Testing

Once testing is complete:

1. Document any issues found
2. Test all existing functionality with OAuth tokens
3. Update user documentation
4. Train users on new login process
5. Monitor logs for authentication issues in production
