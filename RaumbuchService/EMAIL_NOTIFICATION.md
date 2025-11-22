# Email Notification Implementation

## Overview

Implemented email notification system to replace TODO-based notifications. The new system:

1. **Fetches project users** from Trimble Connect API
2. **Shows user selection UI** with checkboxes
3. **Sends email notifications** with download link to selected users
4. **Uses file download URLs** from Trimble Connect API

## Architecture

### Backend Components

#### 1. TrimbleConnectService - New Methods

**`GetProjectUsersAsync(string projectId)`**
- Endpoint: `GET /projects/{projectId}/users`
- Returns list of users with `id`, `email`, `givenName`, `familyName`, `name`

**`GetFileDownloadUrlAsync(string fileId)`**
- Endpoint: `GET /files/fs/{fileId}/downloadurl`
- Returns download URL for sharing files via email

#### 2. EmailService (NEW)

Located: `RaumbuchService\Services\EmailService.cs`

**Features:**
- Development mode: Logs emails to console instead of sending
- Production mode: Sends via SMTP
- HTML email templates with styled download buttons
- Separate templates for Raumprogramm and Raumbuch notifications

**Methods:**
- `SendRaumprogrammNotificationAsync()`
- `SendRaumbuchNotificationAsync()`

**Configuration:**
```csharp
new EmailService(
    smtpHost: "smtp.example.com",     // Required for production
    smtpPort: 587,
    smtpUsername: "user@example.com",  // Optional
    smtpPassword: "password",          // Optional
    fromEmail: "noreply@raumbuch.local",
    fromName: "Raumbuch Manager",
    enableSsl: true,
    isDevelopment: false               // Set to false for production
)
```

#### 3. New Controller Endpoints

**`POST /api/raumbuch/get-project-users`**
- Request: `{ accessToken, projectId }`
- Response: `{ success, users: [{ id, email, displayName }] }`

**`POST /api/raumbuch/send-notification`**
- Request:
  ```json
  {
    "accessToken": "...",
    "fileId": "...",
    "fileName": "Raumprogramm.xlsx",
    "projectName": "...",
    "recipientEmails": ["user1@example.com", "user2@example.com"],
    "notificationType": "raumprogramm" // or "raumbuch"
  }
  ```
- Response: `{ success, message, recipientCount }`

### Frontend Changes

#### Updated UI (index.html - Step 2)

**Old Flow (TODO):**
1. Enter title
2. Enter assignees (comma-separated emails)
3. Enter due date
4. Click "Create TODO"

**New Flow (Email):**
1. Click "Benutzer laden" button
2. Select users from checkbox list
3. Enter/confirm File ID (auto-filled from Step 1)
4. Click "E-Mail senden"

#### New JavaScript Functions

**`loadProjectUsers()`**
- Calls `/get-project-users` endpoint
- Displays user list with checkboxes

**`displayUserList(users)`**
- Renders user checkboxes dynamically
- Format: `Name (email@example.com)`

**`sendEmailNotification()`**
- Gets selected users from checkboxes
- Calls `/send-notification` endpoint
- Shows success/error message

## Email Template

The emails are styled HTML with:
- Gradient header (matches app branding)
- Professional layout
- Big "Download" button
- File information (name, project)
- Auto-generated disclaimer

**Subject (Raumprogramm):** "Raumprogramm wurde auf Trimble Connect erstellt"

**Subject (Raumbuch):** "Raumbuch wurde auf Trimble Connect erstellt"

## File Download URL

**Problem:** Trimble Connect doesn't provide direct "Open in Excel Online" URLs via API.

**Current Solution:** Email contains download link from `/files/fs/{fileId}/downloadurl`

**User Flow:**
1. User clicks link in email
2. File downloads to local machine
3. User opens in Excel (desktop or online manually)

**Future Enhancement:** Research Trimble Connect web viewer URLs or Office 365 integration.

## Development vs Production

### Development Mode (Current)
```csharp
var emailService = new EmailService(isDevelopment: true);
```
- Emails are logged to console/debug output
- No SMTP configuration needed
- Perfect for testing workflow

**Console Output:**
```
=== EMAIL (Development Mode - Not Sent) ===
From: Raumbuch Manager <noreply@raumbuch.local>
To: user1@example.com, user2@example.com
Subject: Raumprogramm wurde auf Trimble Connect erstellt
Body:
<!DOCTYPE html>...
==========================================
```

### Production Mode

**Requirements:**
1. SMTP server credentials
2. Update Web.config with SMTP settings:
```xml
<appSettings>
  <add key="SMTP_HOST" value="smtp.sendgrid.net" />
  <add key="SMTP_PORT" value="587" />
  <add key="SMTP_USERNAME" value="apikey" />
  <add key="SMTP_PASSWORD" value="SG.xxx..." />
  <add key="EMAIL_FROM" value="noreply@yourcompany.com" />
  <add key="EMAIL_FROM_NAME" value="Raumbuch Manager" />
</appSettings>
```

3. Update controller to read config:
```csharp
var emailService = new EmailService(
    smtpHost: ConfigurationManager.AppSettings["SMTP_HOST"],
    smtpPort: int.Parse(ConfigurationManager.AppSettings["SMTP_PORT"]),
    smtpUsername: ConfigurationManager.AppSettings["SMTP_USERNAME"],
    smtpPassword: ConfigurationManager.AppSettings["SMTP_PASSWORD"],
    fromEmail: ConfigurationManager.AppSettings["EMAIL_FROM"],
    fromName: ConfigurationManager.AppSettings["EMAIL_FROM_NAME"],
    isDevelopment: false
);
```

## Testing

### Test Email Notification Flow

1. **Start application** (F5 in Visual Studio)

2. **Configure token and project:**
   - Paste access token
   - Project ID: `y7-N0uBcXNI`
   - Target Folder ID: `y2YBScEdiLI`

3. **Step 1 - Import Template:**
   - Enter template file ID
   - Click "Vorlage importieren"
   - Note the returned file ID (auto-filled in Step 2)

4. **Step 2 - Send Email:**
   - Click "Benutzer laden"
   - Select one or more users
   - Verify file ID is filled
   - Click "E-Mail senden"

5. **Check console output** for email details (development mode)

### Expected Results

? **User List Loading:**
- Shows all project users with names and emails
- Checkboxes for selection

? **Email Sending:**
- Success message: "Benachrichtigung wurde an X Empfänger gesendet."
- Console logs show email content
- Download URL is valid Trimble Connect URL

## API Comparison

### Old (TODO) vs New (Email)

| Feature | TODO API | Email API |
|---------|----------|-----------|
| User selection | Manual email input | Visual checkbox list |
| User discovery | Manual | Automatic from project |
| Notification | Trimble Connect UI | Direct email |
| File access | TODO link | Download link |
| Configuration | None | SMTP (production) |

## Security Notes

1. **Access tokens** are passed in request bodies (not secure for production - consider using session/cookies)
2. **SMTP passwords** should be stored securely (Azure Key Vault, encrypted config)
3. **Email validation** is minimal - enhance for production
4. **Rate limiting** should be added to prevent email spam

## Future Enhancements

1. **Excel Online Integration:**
   - Research Trimble Connect + Office 365 integration
   - Generate "Open in Excel Online" links

2. **Email Templates:**
   - Add company logo
   - Customize text per project
   - Multilingual support (DE/EN)

3. **Notification History:**
   - Log sent emails to database
   - Show notification status in UI

4. **Advanced User Selection:**
   - Filter users by role
   - Select all/deselect all buttons
   - Remember last selected users

5. **Attachment Support:**
   - Attach Excel file directly to email (if file size allows)
   - Reduces dependency on download URLs

## Files Modified/Created

### New Files:
- `RaumbuchService\Services\EmailService.cs` - Email sending service
- `RaumbuchService\Models\NotificationModels.cs` - Request/response models
- `RaumbuchService\EMAIL_NOTIFICATION.md` - This documentation

### Modified Files:
- `RaumbuchService\Services\TrimbleConnectService.cs` - Added user and download URL methods
- `RaumbuchService\Controllers\RaumbuchController.cs` - Added new endpoints
- `RaumbuchService\index.html` - Updated Step 2 UI and JavaScript

## Backward Compatibility

The old TODO endpoint (`/api/raumbuch/create-todo`) is still available for backward compatibility, but new implementations should use the email notification system.
