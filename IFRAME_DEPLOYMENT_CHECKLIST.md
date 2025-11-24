# iframe Embedding Deployment Checklist

## Quick Start: Deploy iframe Configuration to Azure

This checklist helps you deploy the iframe embedding configuration to Azure App Service.

### ✅ Pre-Deployment Checklist

- [x] Web.config updated with iframe configuration
- [x] Documentation created (TRIMBLE_IFRAME_EMBEDDING.md)
- [x] Test page created (test-iframe.html)
- [ ] Changes deployed to Azure App Service
- [ ] Headers verified in production

---

## Step 1: Deploy to Azure

Choose one of these deployment methods:

### Option A: Visual Studio (Recommended)
1. Open `RaumbuchService.sln` in Visual Studio
2. Right-click `RaumbuchService` project → **Publish**
3. Select existing Azure publish profile
4. Click **Publish** button
5. Wait for deployment to complete

### Option B: Azure Portal (ZIP Deploy)
1. Build project in Visual Studio (Release configuration)
2. Navigate to `bin\Release` folder
3. Create ZIP of all contents
4. Open Azure Portal → App Service "Raumbuch"
5. Go to **Advanced Tools** → **Go** (opens Kudu)
6. Select **Tools** → **Zip Push Deploy**
7. Drag and drop ZIP file

### Option C: Git Deployment
1. In Azure Portal → App Service "Raumbuch"
2. Go to **Deployment Center**
3. Sync with GitHub repository
4. Deployment happens automatically

---

## Step 2: Verify Deployment

### 2.1 Restart App Service (if needed)
1. In Azure Portal → App Service "Raumbuch"
2. Click **Restart** button
3. Wait for restart to complete (~30 seconds)

### 2.2 Check Response Headers
1. Open browser and navigate to:
   ```
   https://raumbuch.azurewebsites.net/
   ```

2. Open DevTools (F12) → **Network** tab

3. Reload page (Ctrl+R or Cmd+R)

4. Click on the main document request (first item, usually `/` or `index.html`)

5. Check **Response Headers** section:

   **Expected Headers:**
   ```
   Content-Security-Policy: default-src 'self'; frame-ancestors 'self' https://web.connect.trimble.com https://*.connect.trimble.com;
   Access-Control-Allow-Origin: https://web.connect.trimble.com
   Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
   Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With
   ```

   **Should NOT be present:**
   ```
   X-Frame-Options: DENY
   X-Frame-Options: SAMEORIGIN
   ```

### 2.3 Test iframe Embedding

**Option 1: Use Test Page**
1. Deploy `test-iframe.html` to any web server
2. Open it in a browser
3. The iframe should load the Raumbuch app
4. Check for success message: "✅ iframe loaded successfully!"

**Option 2: Quick Browser Test**
1. Open browser console (F12)
2. Paste this code and run:
   ```javascript
   const iframe = document.createElement('iframe');
   iframe.src = 'https://raumbuch.azurewebsites.net/';
   iframe.style.width = '100%';
   iframe.style.height = '600px';
   iframe.style.border = '2px solid blue';
   document.body.appendChild(iframe);
   iframe.onload = () => console.log('✅ iframe loaded successfully!');
   iframe.onerror = () => console.error('❌ iframe failed to load');
   ```
3. The iframe should appear and load the app
4. Check console for success/error message

---

## Step 3: Configure Trimble Connect Extension

### 3.1 Create Extension Manifest

Create a JSON file with your extension configuration:

```json
{
  "name": "Raumbuch Manager",
  "version": "1.0.0",
  "description": "IFC Raumbuch management tool for Trimble Connect",
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

### 3.2 Host Manifest File

**Option A: Azure Blob Storage (Recommended)**
1. Open Azure Portal → Storage Account "raumbuchstorage"
2. Create new container: `extension-manifest`
3. Set container to **Public access level**: Blob
4. Upload manifest file as `raumbuch-extension.json`
5. Copy blob URL (e.g., `https://raumbuchstorage.blob.core.windows.net/extension-manifest/raumbuch-extension.json`)

**Option B: Same App Service**
1. Place manifest file in `RaumbuchService` folder
2. Deploy to Azure
3. Access at: `https://raumbuch.azurewebsites.net/raumbuch-extension.json`

### 3.3 Register Extension in Trimble Connect

1. Log in to Trimble Connect Web: https://web.connect.trimble.com
2. Open your project
3. Navigate to **Settings** or **Extensions** (location varies by version)
4. Select **Private Extensions** or **Add Extension**
5. Enter manifest URL
6. Save and activate extension
7. Test by opening an IFC file in File Explorer

---

## Troubleshooting

### ❌ Issue: X-Frame-Options still appears

**Cause:** Azure configuration or module overriding Web.config

**Solution:**
1. Check for Azure "Easy Auth" settings that might add headers
2. Verify Web.config was deployed correctly:
   - Open Kudu: https://raumbuch.scm.azurewebsites.net
   - Navigate to **Debug console** → **CMD**
   - Go to `site/wwwroot`
   - Click to view `Web.config`
   - Verify `<httpProtocol>` section is present
3. Restart App Service
4. Clear browser cache

### ❌ Issue: CSP violation error

**Error in console:** `Refused to frame 'https://raumbuch.azurewebsites.net/' because an ancestor violates the following Content Security Policy directive: "frame-ancestors..."`

**Cause:** Embedding domain not in CSP frame-ancestors list

**Solution:**
1. Identify the embedding domain from error message
2. Add domain to CSP frame-ancestors in Web.config
3. Redeploy

### ❌ Issue: CORS error when calling API

**Error in console:** `Access to XMLHttpRequest at 'https://raumbuch.azurewebsites.net/api/...' from origin 'https://web.connect.trimble.com' has been blocked by CORS policy`

**Cause:** CORS headers not configured or origin mismatch

**Solution:**
1. Verify CORS headers in response
2. Check that origin matches exactly (including protocol)
3. For OPTIONS requests, ensure they're not blocked by handlers
4. May need to handle CORS programmatically in code for more complex scenarios

### ❌ Issue: iframe loads but API calls fail

**Cause:** Authentication or CORS issue

**Solution:**
1. Check browser console for specific error
2. Verify OAuth flow works in iframe context
3. Check that cookies are allowed in iframe (SameSite settings)
4. May need to adjust session state configuration

---

## Success Criteria

Your deployment is successful when:

- ✅ No X-Frame-Options header in response
- ✅ Content-Security-Policy header with correct frame-ancestors
- ✅ CORS headers present for Trimble Connect
- ✅ Application loads in iframe without errors
- ✅ Application functions correctly within iframe
- ✅ Extension appears in Trimble Connect File Explorer
- ✅ Can open IFC files with the extension

---

## Additional Resources

- **TRIMBLE_IFRAME_EMBEDDING.md** - Detailed configuration guide
- **AZURE_DEPLOYMENT_GUIDE.md** - General Azure deployment instructions
- **test-iframe.html** - Interactive test page
- **Trimble Developer Portal** - https://developer.connect.trimble.com/

---

## Support

If you encounter issues not covered in this guide:

1. Check browser console for error messages
2. Review response headers in Network tab
3. Consult TRIMBLE_IFRAME_EMBEDDING.md troubleshooting section
4. Check Azure App Service logs in Kudu console
5. Verify OAuth configuration is correct

---

**Last Updated:** 2025-11-24  
**Configuration Version:** 1.0  
**Azure App Service:** raumbuch.azurewebsites.net
