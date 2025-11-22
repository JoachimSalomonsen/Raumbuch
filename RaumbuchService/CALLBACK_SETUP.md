# Callback URL Setup for Local Testing

## Problem
The registered OAuth callback URL is `http://localhost:5005/callback/`, but the main application runs on `https://localhost:44305`.

## Solution Options

### Option 1: Simple HTTP Server for Callback (Recommended for Testing)

1. Copy `callback.html` to a separate folder (e.g., `C:\temp\callback`)

2. Run a simple HTTP server on port 5005:

**Using Python:**
```bash
cd C:\temp\callback
python -m http.server 5005
```

**Using Node.js (http-server):**
```bash
npm install -g http-server
cd C:\temp\callback
http-server -p 5005
```

**Using .NET CLI:**
```bash
cd C:\temp\callback
dotnet tool install -g dotnet-serve
dotnet serve -p 5005
```

3. The callback URL will now work at: `http://localhost:5005/callback.html`

### Option 2: Update Trimble Connect Registration

1. Go to Trimble Developer Portal
2. Update your application's callback URL to: `https://localhost:44305/callback.html`
3. Update `Web.config` with the new callback URL
4. Update the OAuth link in `index.html`

**Note:** You'll need to wait for the change to propagate (usually instant, but can take a few minutes).

### Option 3: Use IIS Express Configuration

Add a binding for port 5005 in your project's `.vs\config\applicationhost.config` file.

## Current Status

Currently configured for **Option 1** with callback URL: `http://localhost:5005/callback/`

## Testing the Flow

1. Click "Hier klicken" link in the Token section
2. Login with your Trimble credentials
3. You'll be redirected to `http://localhost:5005/callback.html?code=...`
4. Copy the authorization code
5. Use Postman to exchange it for an access token:

**Token Exchange Request:**
- Method: POST
- URL: `https://id.trimble.com/oauth/token`
- Headers:
  - `Content-Type: application/x-www-form-urlencoded`
- Body (x-www-form-urlencoded):
  - `grant_type`: `authorization_code`
  - `code`: `<your_authorization_code>`
  - `client_id`: `073a84b7-323b-43bf-b5a9-96bf17638dcc`
  - `client_secret`: `7f4e6d7c6c0c45f387866e86e6eda65c`
  - `redirect_uri`: `http://localhost:5005/callback/`

6. Copy the `access_token` from the response
7. Paste it into the "Access Token" field in the GUI
