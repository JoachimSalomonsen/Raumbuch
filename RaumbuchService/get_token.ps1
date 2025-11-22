# Trimble Connect Token Generator
# Run this script to get a valid access token

Write-Host "=== Trimble Connect Token Generator ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get Authorization Code
Write-Host "Step 1: Get Authorization Code" -ForegroundColor Yellow
Write-Host "Open this URL in your browser and log in:" -ForegroundColor White
Write-Host "https://id.trimble.com/oauth/authorize?client_id=073a84b7-323b-43bf-b5a9-96bf17638dcc&response_type=code&redirect_uri=http://localhost:5005/callback/&scope=openid%20CST-PowerBI" -ForegroundColor Green
Write-Host ""
Write-Host "NOTE: Using scope 'openid CST-PowerBI' for Trimble Connect Files API" -ForegroundColor Yellow
Write-Host ""

# Ask user for authorization code
$authCode = Read-Host "Paste the authorization code here (from URL after ?code=)"

if ([string]::IsNullOrWhiteSpace($authCode)) {
    Write-Host "ERROR: No authorization code provided!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Exchange code for access token..." -ForegroundColor Yellow

try {
    $body = @{
        grant_type = "authorization_code"
        client_id = "073a84b7-323b-43bf-b5a9-96bf17638dcc"
        client_secret = "7f4e6d7c6c0c45f387866e86e6eda65c"
        redirect_uri = "http://localhost:5005/callback/"
        code = $authCode
    }

    $response = Invoke-RestMethod -Uri "https://id.trimble.com/oauth/token" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"

    Write-Host ""
    Write-Host "SUCCESS! Access token received." -ForegroundColor Green
    Write-Host ""
    Write-Host "Token length: $($response.access_token.Length) characters" -ForegroundColor Cyan
    Write-Host "Expires in: $($response.expires_in) seconds (~$([math]::Round($response.expires_in/60)) minutes)" -ForegroundColor Cyan
    
    # Decode JWT to show scope and region
    try {
        $tokenParts = $response.access_token.Split('.')
        $payload = $tokenParts[1]
        # Add padding if needed
        while ($payload.Length % 4 -ne 0) { $payload += "=" }
        $payloadJson = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload))
        $tokenData = $payloadJson | ConvertFrom-Json
        Write-Host "Scope: $($tokenData.scope)" -ForegroundColor Cyan
        Write-Host "Data Region: $($tokenData.data_region)" -ForegroundColor Cyan
    } catch {
        # Ignore decoding errors
    }
    
    Write-Host ""
    Write-Host "=== COPY THIS ACCESS TOKEN ===" -ForegroundColor Yellow
    Write-Host $response.access_token -ForegroundColor White
    Write-Host "=== END OF TOKEN ===" -ForegroundColor Yellow
    Write-Host ""
    
    # Copy to clipboard
    $response.access_token | Set-Clipboard
    Write-Host "Token has been copied to clipboard!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Now paste it into the 'Access Token' field in your app." -ForegroundColor Cyan
}
catch {
    Write-Host ""
    Write-Host "ERROR: Failed to get access token" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor White
    }
}
