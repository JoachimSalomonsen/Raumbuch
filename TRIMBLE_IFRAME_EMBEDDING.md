# Trimble Connect iframe Embedding Configuration

## Overview

This guide explains how the Raumbuch application is configured to be embedded as a private extension within Trimble Connect Web File Explorer using an iframe.

## Web.config Configuration

The application's `Web.config` file has been configured to allow embedding in Trimble Connect iframes. The following configuration is already in place in `RaumbuchService/Web.config`:

### Configuration Details

```xml
<system.webServer>
  <handlers>
    <!-- Existing handlers configuration -->
  </handlers>
  
  <!-- Configuration for embedding in Trimble Connect iframe -->
  <httpProtocol>
    <customHeaders>
      <!-- Remove X-Frame-Options to allow iframe embedding -->
      <remove name="X-Frame-Options" />
      
      <!-- Add Content-Security-Policy with frame-ancestors for Trimble Connect -->
      <add name="Content-Security-Policy" 
           value="default-src 'self'; frame-ancestors 'self' https://web.connect.trimble.com https://*.connect.trimble.com;" />
      
      <!-- Add CORS headers for Trimble Connect API access -->
      <add name="Access-Control-Allow-Origin" value="https://web.connect.trimble.com" />
      <add name="Access-Control-Allow-Methods" value="GET, POST, PUT, DELETE, OPTIONS" />
      <add name="Access-Control-Allow-Headers" value="Content-Type, Authorization, X-Requested-With" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

## What Each Configuration Does

### 1. Remove X-Frame-Options Header

```xml
<remove name="X-Frame-Options" />
```

**Purpose:** By default, Azure App Service and IIS may add `X-Frame-Options: DENY` or `X-Frame-Options: SAMEORIGIN` headers, which prevent the page from being loaded in an iframe. Removing this header allows iframe embedding.

**Why it's needed:** Without removing this header, Trimble Connect cannot load the application in an iframe.

### 2. Content-Security-Policy with frame-ancestors

```xml
<add name="Content-Security-Policy" 
     value="default-src 'self'; frame-ancestors 'self' https://web.connect.trimble.com https://*.connect.trimble.com;" />
```

**Purpose:** Content Security Policy (CSP) is the modern, recommended way to control iframe embedding. The `frame-ancestors` directive specifies which domains are allowed to embed this application in an iframe.

**Allowed domains:**
- `'self'` - The application can embed itself
- `https://web.connect.trimble.com` - Main Trimble Connect Web domain
- `https://*.connect.trimble.com` - Any subdomain of connect.trimble.com

**Why it's needed:** This provides fine-grained control over iframe embedding and is more secure than X-Frame-Options.

### 3. CORS Headers

```xml
<add name="Access-Control-Allow-Origin" value="https://web.connect.trimble.com" />
<add name="Access-Control-Allow-Methods" value="GET, POST, PUT, DELETE, OPTIONS" />
<add name="Access-Control-Allow-Headers" value="Content-Type, Authorization, X-Requested-With" />
```

**Purpose:** Cross-Origin Resource Sharing (CORS) headers allow Trimble Connect to make API requests to the application.

**Why it's needed:** Trimble Connect needs to:
- Fetch the extension manifest JSON file
- Make API calls to the application's endpoints
- Handle authentication and authorization

## Verification After Deployment

After deploying to Azure, verify the configuration by:

1. **Open the application in a browser:**
   ```
   https://raumbuch.azurewebsites.net/
   ```

2. **Check response headers in DevTools:**
   - Open Browser DevTools (F12)
   - Go to Network tab
   - Reload the page
   - Select the main document request (e.g., `index.html` or the root `/`)
   - Check the Response Headers

3. **Expected headers:**
   - ✅ **NO** `X-Frame-Options` header should be present
   - ✅ `Content-Security-Policy` header should contain: `frame-ancestors 'self' https://web.connect.trimble.com https://*.connect.trimble.com`
   - ✅ `Access-Control-Allow-Origin` header should be: `https://web.connect.trimble.com`

4. **If X-Frame-Options still appears:**
   - It may come from Azure "Easy Auth" settings
   - Check for other `<customHeaders>` blocks in Web.config
   - Check if there's a reverse proxy or security module overriding headers

## Trimble Connect Extension Setup

To use this application as a private extension in Trimble Connect:

### 1. Create Extension Manifest JSON

Create a JSON manifest file that describes your extension. This should be hosted at a publicly accessible URL (e.g., in Azure Blob Storage or on the same Azure App Service).

**Example manifest structure:**
```json
{
  "name": "Raumbuch Manager",
  "version": "1.0.0",
  "description": "IFC Raumbuch management tool",
  "icon": "https://raumbuch.azurewebsites.net/favicon.ico",
  "author": "Your Name/Organization",
  "extensions": [
    {
      "type": "fileViewer",
      "name": "Raumbuch Manager",
      "url": "https://raumbuch.azurewebsites.net/index.html",
      "fileTypes": [".ifc"],
      "description": "View and manage IFC Raumbuch data"
    }
  ]
}
```

### 2. Register Extension in Trimble Connect

1. Log in to Trimble Connect Web
2. Navigate to your project
3. Go to Settings or Extensions (depending on Trimble Connect version)
4. Add private extension using the manifest URL
5. The application will now be available in the File Explorer

## Security Considerations

### Content Security Policy

The current CSP configuration allows:
- **Resources from same origin:** `default-src 'self'`
- **Iframe embedding from:** Trimble Connect domains only

If you need to load resources from additional domains (e.g., CDNs, APIs), you may need to adjust the CSP policy:

```xml
<!-- Example with additional sources -->
<add name="Content-Security-Policy" 
     value="default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; frame-ancestors 'self' https://web.connect.trimble.com https://*.connect.trimble.com;" />
```

**Note:** `'unsafe-inline'` and `'unsafe-eval'` reduce security but may be necessary for some frameworks. Use with caution.

### CORS Configuration

The current CORS configuration allows requests only from `https://web.connect.trimble.com`. If Trimble Connect uses different subdomains or if you need to support multiple environments, you may need to adjust this.

**Option 1: Allow all Trimble Connect subdomains (not recommended due to wildcard limitations in CORS)**
```xml
<!-- Note: This won't work as expected - CORS doesn't support wildcards in Access-Control-Allow-Origin -->
<add name="Access-Control-Allow-Origin" value="https://*.connect.trimble.com" />
```

**Option 2: Handle CORS programmatically in code**
For more flexible CORS configuration, consider implementing CORS in the application code (e.g., in `Global.asax.cs` or using OWIN middleware) to dynamically set the `Access-Control-Allow-Origin` header based on the request's origin.

## Troubleshooting

### Issue: Application doesn't load in iframe

**Possible causes:**
1. X-Frame-Options header is still present
2. CSP frame-ancestors doesn't include the embedding domain
3. Browser console shows CSP violation errors

**Solution:**
- Check response headers in DevTools
- Verify Web.config has been deployed correctly
- Check Azure App Service configuration for conflicting settings

### Issue: API calls fail with CORS errors

**Possible causes:**
1. CORS headers not configured correctly
2. Preflight OPTIONS requests not handled
3. Origin doesn't match exactly

**Solution:**
- Verify CORS headers in response
- Ensure OPTIONS requests are not blocked
- Check that the origin matches exactly (including protocol and subdomain)

### Issue: Changes not taking effect

**Possible causes:**
1. Web.config not deployed to Azure
2. Azure App Service cached old configuration
3. Browser cached old headers

**Solution:**
1. Verify Web.config is present in Azure (use Kudu console at https://raumbuch.scm.azurewebsites.net/)
2. Restart Azure App Service
3. Clear browser cache and test in incognito mode

## Additional Resources

- [MDN: Content Security Policy (CSP)](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)
- [MDN: X-Frame-Options](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Frame-Options)
- [MDN: CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [Trimble Connect Developer Documentation](https://developer.connect.trimble.com/)
- [Azure App Service Configuration](https://docs.microsoft.com/en-us/azure/app-service/configure-common)

## Summary

The application is now configured to:
- ✅ Allow embedding in Trimble Connect iframes
- ✅ Accept API requests from Trimble Connect
- ✅ Support CORS for cross-origin requests
- ✅ Use modern CSP policies for security

No additional Azure portal configuration is required - the Web.config changes are sufficient for iframe embedding.
