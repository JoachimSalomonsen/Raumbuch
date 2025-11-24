# Azure Deployment Checklist

Use this checklist when deploying the Raumbuch Service to Azure.

## Pre-Deployment

- [ ] Code committed to repository
- [ ] All tests passing locally
- [ ] Documentation updated
- [ ] Azure resources verified (App Service, Storage Account)

## Obtain Azure Storage Credentials

- [ ] Navigate to Azure Portal: https://portal.azure.com
- [ ] Go to Resource Group: `Connect_Extensions`
- [ ] Open Storage Account: `raumbuchstorage`
- [ ] Click **Access keys** (under Security + networking)
- [ ] Copy **Connection string** from Key1
- [ ] Store securely (do not commit to code!)

## Deploy Application

Choose one deployment method:

### Method A: Visual Studio Publish
- [ ] Open `RaumbuchService.sln` in Visual Studio
- [ ] Right-click `RaumbuchService` project → **Publish**
- [ ] Select existing profile for `Raumbuch` App Service
- [ ] Click **Publish** button
- [ ] Wait for deployment to complete
- [ ] Check output window for success message

### Method B: Azure Portal ZIP Deploy
- [ ] Build project in Release mode
- [ ] Navigate to `bin\Release` folder
- [ ] Create ZIP of contents (not the folder itself)
- [ ] Go to App Service in Azure Portal
- [ ] Click **Advanced Tools** → **Go** (opens Kudu)
- [ ] Navigate to **Tools** → **Zip Push Deploy**
- [ ] Drag and drop ZIP file
- [ ] Wait for deployment to complete

## Configure App Service

- [ ] Navigate to `Raumbuch` App Service in Azure Portal
- [ ] Click **Configuration** (under Settings)
- [ ] Click **+ New application setting**
- [ ] Add: `AZURE_STORAGE_CONNECTION_STRING`
  - Name: `AZURE_STORAGE_CONNECTION_STRING`
  - Value: (paste connection string from earlier)
- [ ] Click **OK**

### Verify Existing Settings
- [ ] `TRIMBLE_CLIENT_ID` is present
- [ ] `TRIMBLE_CLIENT_SECRET` is present
- [ ] `TRIMBLE_BASE_URL` = `https://app21.connect.trimble.com/tc/api/2.0`
- [ ] `TRIMBLE_AUTH_URL` = `https://id.trimble.com/oauth/authorize`
- [ ] `TRIMBLE_TOKEN_URL` = `https://id.trimble.com/oauth/token`
- [ ] `TRIMBLE_SCOPE` = `openid CST-PowerBI`
- [ ] `TRIMBLE_REDIRECT_URI` is correct

- [ ] Click **Save** at the top
- [ ] Click **Continue** when prompted to restart

## Post-Deployment Testing

### Basic Health Check
- [ ] Navigate to: https://raumbuch.azurewebsites.net/
- [ ] Page loads without errors
- [ ] No browser console errors
- [ ] UI elements visible and functional

### Configuration Management Test
- [ ] Enter a valid Trimble Connect Access Token
- [ ] Enter Project ID (e.g., `y7-N0uBcXNI`)
- [ ] Enter Configuration Name (e.g., `Test_Deployment`)
- [ ] Click **📁 Projektdaten laden**
- [ ] Verify folders dropdown populates
- [ ] Verify saved configurations dropdown shows (may be empty initially)
- [ ] Click **💾 Konfiguration speichern**
- [ ] Verify success message: `✅ Konfiguration 'Test_Deployment' wurde in Azure gespeichert.`

### Load Configuration Test
- [ ] Click **📁 Projektdaten laden** again
- [ ] Verify `Test_Deployment` appears in dropdown
- [ ] Select it from dropdown
- [ ] Click **📥 Laden**
- [ ] Verify configuration loads successfully

### Azure Storage Verification
- [ ] Go to Azure Portal
- [ ] Navigate to `raumbuchstorage` Storage Account
- [ ] Click **Storage browser** (or **Containers**)
- [ ] Find container: `raumbuch-configs`
- [ ] Look for folder with your Project ID
- [ ] Verify `Test_Deployment.json` file exists
- [ ] Download and inspect content (should be valid JSON)

## Monitoring Setup

### Enable Application Insights (Optional)
- [ ] Go to `Raumbuch` App Service
- [ ] Click **Application Insights** (under Settings)
- [ ] Enable if not already enabled
- [ ] Configure basic telemetry

### Check Logs
- [ ] Go to `Raumbuch` App Service
- [ ] Click **Log stream** (under Monitoring)
- [ ] Keep open while testing
- [ ] Watch for any errors or warnings

## Troubleshooting

If issues occur, check:

### Application Not Starting
- [ ] Check **Log stream** for startup errors
- [ ] Verify App Service is running (not stopped)
- [ ] Check all required App Settings are present
- [ ] Restart App Service

### Configuration Not Saving to Azure
- [ ] Verify `AZURE_STORAGE_CONNECTION_STRING` is set correctly
- [ ] Check connection string has correct format
- [ ] Test connection string in Azure Storage Explorer
- [ ] Check Log stream for detailed error messages

### Cannot Load Configurations
- [ ] Verify blob container `raumbuch-configs` exists
- [ ] Check files exist in correct folder structure
- [ ] Verify Project ID matches folder name in storage
- [ ] Check network access to storage account

### 500 Errors
- [ ] Check Log stream for stack traces
- [ ] Verify all NuGet packages deployed
- [ ] Check .NET Framework version (should be 4.8)
- [ ] Restart App Service

## Rollback Procedure

If deployment fails and rollback is needed:

- [ ] Go to `Raumbuch` App Service in Azure Portal
- [ ] Click **Deployment slots** or **Deployment Center**
- [ ] Redeploy previous version
- [ ] Alternatively: Remove `AZURE_STORAGE_CONNECTION_STRING` to use local mode
- [ ] Restart App Service

## Backup Configurations Before Rollback

- [ ] Open Azure Storage Explorer (download if needed)
- [ ] Connect to `raumbuchstorage`
- [ ] Navigate to `raumbuch-configs` container
- [ ] Download all files to local backup location
- [ ] Store securely for redistribution if needed

## Success Criteria

Deployment is successful when:

- [x] Application loads at https://raumbuch.azurewebsites.net/
- [x] No errors in browser console
- [x] Configuration can be saved to Azure
- [x] Configuration appears in dropdown
- [x] Configuration can be loaded from Azure
- [x] Files visible in Azure Storage blob container

## Post-Deployment Tasks

- [ ] Notify team of successful deployment
- [ ] Share application URL
- [ ] Provide brief usage instructions
- [ ] Schedule follow-up review in 1 week
- [ ] Document any issues encountered
- [ ] Update runbook with lessons learned

## Communication

Send deployment notification:

**Subject**: Raumbuch Service Deployed to Azure

**To**: Team / Stakeholders

**Message Template**:
```
The Raumbuch Service has been successfully deployed to Azure.

Access URL: https://raumbuch.azurewebsites.net/

Key Features:
- Configuration management with Azure Blob Storage
- Configurations automatically organized by project
- Dropdown selection of saved configurations
- Backward compatible with local file upload/download

Documentation:
- Deployment Guide: AZURE_DEPLOYMENT_GUIDE.md
- Migration Summary: AZURE_MIGRATION_SUMMARY.md
- README: README.md

Please test and report any issues.
```

---

**Last Updated**: 2025-11-23
**Deployment Guide**: [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md)
