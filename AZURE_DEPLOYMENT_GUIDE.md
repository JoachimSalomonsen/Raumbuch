# Azure Deployment Guide for Raumbuch Service

This guide explains how to deploy the Raumbuch Service to Azure App Service and configure Azure Blob Storage for configuration management.

## Prerequisites

- Azure subscription with the following resources already created:
  - Resource Group: `Connect_Extensions`
  - App Service: `Raumbuch` (in Sweden Central)
  - Storage Account: `raumbuchstorage` (in Norway East)
  - App Service Plan: `ASP-ConnectExtensions-9c15`

## Azure Resources Overview

Based on your CSV export:

| Resource | Type | Location | Purpose |
|----------|------|----------|---------|
| `Raumbuch` | App Service | Sweden Central | Hosts the web application |
| `raumbuchstorage` | Storage Account | Norway East | Stores JSON configuration files |
| `Connect_Extensions` | Resource Group | Norway East | Contains all resources |
| `ASP-ConnectExtensions-9c15` | App Service Plan | Sweden Central | Compute resources for App Service |

## Step 1: Get Azure Storage Connection String

1. Go to Azure Portal: https://portal.azure.com
2. Navigate to Resource Groups → `Connect_Extensions`
3. Click on `raumbuchstorage` Storage Account
4. In the left menu, click **Access keys** under Security + networking
5. Copy the **Connection string** from Key1 or Key2
   - It should look like: `DefaultEndpointsProtocol=https;AccountName=raumbuchstorage;AccountKey=...;EndpointSuffix=core.windows.net`

## Step 2: Configure Azure App Service Settings

1. In Azure Portal, navigate to the `Raumbuch` App Service
2. In the left menu, click **Configuration** under Settings
3. Under **Application settings**, click **+ New application setting**
4. Add the following settings:

### Required Settings for Azure Storage:

| Name | Value | Notes |
|------|-------|-------|
| `AZURE_STORAGE_CONNECTION_STRING` | (Your connection string from Step 1) | For configuration file storage |

### Existing Trimble Connect Settings (verify these are present):

| Name | Value | Notes |
|------|-------|-------|
| `TRIMBLE_CLIENT_ID` | `073a84b7-323b-43bf-b5a9-96bf17638dcc` | Trimble Connect OAuth Client ID |
| `TRIMBLE_CLIENT_SECRET` | `7f4e6d7c6c0c45f387866e86e6eda65c` | Trimble Connect OAuth Client Secret |
| `TRIMBLE_BASE_URL` | `https://app21.connect.trimble.com/tc/api/2.0` | EU region API endpoint |
| `TRIMBLE_AUTH_URL` | `https://id.trimble.com/oauth/authorize` | OAuth authorization endpoint |
| `TRIMBLE_TOKEN_URL` | `https://id.trimble.com/oauth/token` | OAuth token endpoint |
| `TRIMBLE_SCOPE` | `openid CST-PowerBI` | OAuth scopes |
| `TRIMBLE_REDIRECT_URI` | `https://raumbuch.azurewebsites.net/callback/` | OAuth redirect URI (adjust if needed) |

**Important Note**: Do NOT edit Web.config directly for production settings. The Web.config file contains default values for local development only. Azure App Settings (Environment Variables) override Web.config values when deployed. This keeps credentials secure and allows different values for different environments without code changes.

5. Click **Save** at the top
6. Click **Continue** when prompted to restart the app

## Step 3: Deploy Application to Azure

### Option A: Deploy from Visual Studio

1. Open `RaumbuchService.sln` in Visual Studio
2. Right-click on `RaumbuchService` project → **Publish**
3. Choose **Azure** → **Next**
4. Choose **Azure App Service (Windows)** → **Next**
5. Sign in to your Azure account if needed
6. Select:
   - Subscription: `Azure subscription 1`
   - Resource Group: `Connect_Extensions`
   - App Service: `Raumbuch`
7. Click **Finish**
8. Click **Publish** to deploy

### Option B: Deploy from Azure Portal (ZIP Deploy)

1. Build the project in Visual Studio (Release configuration)
2. Navigate to the `bin\Release` folder
3. Create a ZIP file of all contents (not the folder itself)
4. In Azure Portal, go to `Raumbuch` App Service
5. In the left menu, click **Advanced Tools** → **Go**
6. In Kudu console, go to **Tools** → **Zip Push Deploy**
7. Drag and drop your ZIP file to deploy

### Option C: Deploy using Git

1. In Azure Portal, go to `Raumbuch` App Service
2. In the left menu, click **Deployment Center**
3. Configure deployment from GitHub (or other repository)
4. Follow the prompts to connect your repository

## Step 4: Verify Deployment

1. Navigate to: `https://raumbuch.azurewebsites.net/`
2. You should see the Raumbuch Manager interface
3. Test the configuration:
   - Enter an access token and project ID
   - Click **📁 Projektdaten laden**
   - The saved configurations dropdown should populate (or show "Keine gespeicherten Konfigurationen" if none exist yet)
   - Enter a configuration name and click **💾 Konfiguration speichern**
   - Verify it appears in the dropdown after saving

## Step 5: Test Azure Storage Integration

### Test Saving a Configuration:

1. Fill in the required fields:
   - Access Token (from Trimble Connect)
   - Project ID (e.g., `y7-N0uBcXNI`)
   - Configuration Name (e.g., `Test_Config`)
   - Select folders and files as needed
2. Click **💾 Konfiguration speichern**
3. You should see: `✅ Konfiguration 'Test_Config' wurde in Azure gespeichert.`

### Test Loading a Configuration:

1. Click **📁 Projektdaten laden** to refresh the configurations list
2. The **Gespeicherte Konfigurationen (Azure)** dropdown should show your saved configuration
3. Select it and click **📥 Laden**
4. The form should populate with the saved values

### Verify in Azure Storage:

1. In Azure Portal, navigate to `raumbuchstorage` Storage Account
2. Click **Storage browser** in the left menu
3. Navigate to **Blob containers** → `raumbuch-configs`
4. You should see folders named by project IDs
5. Inside each project folder, you'll find the JSON configuration files

## Data Organization in Azure Storage

The application organizes data as follows:

```
raumbuchstorage (Storage Account)
└── raumbuch-configs (Blob Container)
    └── {projectId}/
        ├── Config1.json
        ├── Config2.json
        └── ...
```

- Each project gets its own folder (named by Project ID from Trimble Connect)
- Configuration files are stored as JSON blobs
- File naming: `{ConfigurationName}.json`

## Local Development

For local development, you can run without Azure Storage:

1. Leave `AZURE_STORAGE_CONNECTION_STRING` empty in `Web.config`
2. The application will automatically fall back to downloading configurations as JSON files
3. You can upload previously downloaded JSON files using the "lokale Datei hochladen" option

## Troubleshooting

### Configuration not saving to Azure:

- Check that `AZURE_STORAGE_CONNECTION_STRING` is set in App Service Configuration
- Verify the connection string is correct (test in Azure Storage Explorer)
- Check the App Service logs for detailed error messages

### Cannot see saved configurations:

- Ensure Project ID is entered correctly
- Click **📁 Projektdaten laden** to refresh the list
- Check Azure Storage browser to verify files exist

### Application not starting:

- Check App Service logs in Azure Portal: **Monitoring** → **Log stream**
- Verify all required NuGet packages are deployed
- Ensure .NET Framework 4.8 is available on the App Service

### Access Denied errors:

- Verify the storage account connection string has read/write permissions
- Check that the storage account firewall settings allow the App Service IP

## Security Considerations

### Current Implementation (Phase 1):
- No authentication required
- All users with the application URL can access all configurations
- Suitable for internal use within a trusted organization

### Future Enhancements (Phase 2 - Optional):
- Add Azure AD authentication
- Implement role-based access control (RBAC)
- Add per-project access restrictions
- Enable storage account firewall rules

## Monitoring and Maintenance

### View Application Logs:

1. In Azure Portal, navigate to `Raumbuch` App Service
2. Click **Log stream** under Monitoring
3. View real-time logs of the application

### Monitor Storage Usage:

1. Navigate to `raumbuchstorage` Storage Account
2. Click **Metrics** under Monitoring
3. View storage capacity, transaction counts, etc.

### Backup Configurations:

- Azure Storage automatically provides redundancy (LRS, GRS, etc.)
- For additional backup, use Azure Storage Explorer to download all configurations periodically
- Consider enabling soft delete on the storage account for accidental deletion protection

## Cost Optimization

- Storage costs are minimal for JSON files (typically < $1/month for thousands of configs)
- App Service Plan costs depend on the tier selected
- Consider using deployment slots for testing before production deployment

## Support

For issues or questions:
- Check the Azure Service Health dashboard
- Review App Service diagnostic logs
- Contact: Joachim Salomonsen

## Related Documentation

- [Local Testing Guide](LOCAL_TESTING_GUIDE.md)
- [Trimble Config Guide](TRIMBLE_CONFIG_GUIDE.md)
- [Quick Start Testing](QUICK_START_TESTING.md)
- [README](README.md)
