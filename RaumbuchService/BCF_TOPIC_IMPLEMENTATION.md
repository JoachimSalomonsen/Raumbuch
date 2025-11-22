# BCF Topic Implementation for Trimble Connect Notifications

## Overview

Implemented BCF (BIM Collaboration Format) Topic creation as the notification system for Raumprogramm/Raumbuch creation. This is a **native Trimble Connect integration** that replaces the previous TODO and email notification approaches.

## Why BCF Topics?

? **Native Integration** - BCF is built into Trimble Connect  
? **No SMTP Required** - No external email server configuration needed  
? **Direct Assignment** - Users are assigned via their Trimble Connect email  
? **Folder Links** - Direct links to folders in Trimble Connect web interface  
? **Proven API** - Working endpoint confirmed with test requests  

## API Endpoint

**BCF API Base URL:** `https://open21.connect.trimble.com/bcf/2.1`

**Endpoint:** `POST /projects/{projectId}/topics`

**Request Body:**
```json
{
    "title": "Raumbuch",
    "topic_type": "Request",
    "description": "Raumprogramm wurde in diesem Verzeichnis erstellt.\n\nBitte klicken Sie hier, um es zu bearbeiten:\nhttps://web.connect.trimble.com/projects/y7-N0uBcXNI/data/folder/y2YBScEdiLI",
    "topic_status": "New",
    "assigned_to": "joachim.salomonsen@b.ch"
}
```

**Response:**
```json
{
    "guid": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "title": "Raumbuch",
    "topic_type": "Request",
    "topic_status": "New"
}
```

## Implementation Details

### Backend

#### TrimbleConnectService.cs - New Method

```csharp
public async Task<string> CreateBcfTopicAsync(
    string projectId,
    string title,
    string description,
    string assignedTo,
    string topicType = "Request",
    string topicStatus = "New")
```

**Key Features:**
- Uses BCF API endpoint (open21.connect.trimble.com)
- Returns topic GUID on success
- Single `assigned_to` field (primary assignee)
- Multiple assignees handled in description

#### RaumbuchController.cs - New Endpoint

**Route:** `POST /api/raumbuch/create-bcf-topic`

**Request Model:**
```csharp
public class CreateBcfTopicRequest
{
    public string AccessToken { get; set; }
    public string ProjectId { get; set; }
    public string FolderId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string AssignedTo { get; set; } // Comma-separated emails
}
```

**Logic:**
1. Builds folder URL: `https://web.connect.trimble.com/projects/{projectId}/data/folder/{folderId}`
2. Appends URL to description
3. Takes first email as primary `assigned_to`
4. Additional emails added to description text
5. Hardcoded values:
   - `topic_type`: "Request"
   - `topic_status`: "New"

### Frontend

#### Updated UI (index.html - Step 2)

**Title:** "BCF Topic erstellen"

**Fields:**
- **Titel:** Default "Raumbuch" (editable)
- **Zuweisen an:** Comma-separated emails (no spaces)
- **Beschreibung:** Optional description (default provided)

**Button:** "?? BCF Topic erstellen"

**JavaScript Function:** `createBcfTopic()`

## User Flow

1. **Step 1:** Import template
   - Creates Raumprogramm.xlsx
   - Returns file ID

2. **Step 2:** Create BCF Topic
   - Enter title (default: "Raumbuch")
   - Enter assignee emails (comma-separated, no spaces)
     - Example: `user1@example.com,user2@example.com`
   - Optionally edit description
   - Click "BCF Topic erstellen"

3. **Result:**
   - Success message with Topic GUID
   - Assignees receive notification in Trimble Connect
   - Topic contains direct link to folder

## BCF Topic Content

**Title:** `Raumbuch` (or custom)

**Type:** `Request` (hardcoded)

**Status:** `New` (hardcoded)

**Description Format:**
```
Raumprogramm wurde in diesem Verzeichnis erstellt.

Bitte klicken Sie hier, um es zu bearbeiten:
https://web.connect.trimble.com/projects/y7-N0uBcXNI/data/folder/y2YBScEdiLI

Weitere Empfänger: user2@example.com, user3@example.com
```

**Assigned To:** First email from comma-separated list

## Multiple Assignees Handling

**BCF API Limitation:** Only supports single `assigned_to` field.

**Solution:**
- First email ? `assigned_to` field (primary assignee)
- Additional emails ? Added to description text
- All users can still see the topic in Trimble Connect

## Folder URL Construction

**Pattern:** `https://web.connect.trimble.com/projects/{projectId}/data/folder/{folderId}`

**Example:**
- Project ID: `y7-N0uBcXNI`
- Folder ID: `y2YBScEdiLI`
- URL: `https://web.connect.trimble.com/projects/y7-N0uBcXNI/data/folder/y2YBScEdiLI`

**Dynamic Generation:**
- Project ID from user input
- Folder ID from "Target Folder ID" field (where Raumprogramm was uploaded)

## Testing

### Test BCF Topic Creation

1. **Start application** (F5 in Visual Studio)

2. **Configure:**
   - Access Token
   - Project ID: `y7-N0uBcXNI`
   - Target Folder ID: `y2YBScEdiLI`

3. **Step 1 - Import Template:**
   - Enter template file ID
   - Click "Vorlage importieren"

4. **Step 2 - Create BCF Topic:**
   - Title: "Raumbuch" (or custom)
   - Assigned To: `joachim.salomonsen@b.ch` (or multiple, comma-separated)
   - Description: (keep default or customize)
   - Click "BCF Topic erstellen"

5. **Verify in Trimble Connect:**
   - Open project in Trimble Connect web interface
   - Check BCF Topics section
   - Verify topic exists with correct title and assignee
   - Click link in description to verify folder access

## Advantages over Previous Approaches

| Feature | TODO API | Email (SMTP) | BCF Topic ? |
|---------|----------|--------------|-------------|
| Setup Required | Minimal | SMTP server | None |
| Integration | Trimble Connect | External | Native |
| User Discovery | Manual | API + UI | Manual (simplified) |
| Notification Method | In-app | Email inbox | In-app |
| Link Type | TODO link | Download link | Folder link |
| Multiple Assignees | Limited | Full support | Description workaround |
| Production Ready | Issues | Needs SMTP | Yes |

## API URL Differences

**Standard Trimble Connect API:**
- Base: `https://app21.connect.trimble.com/tc/api/2.0`
- Used for: Files, folders, users, projects

**BCF API:**
- Base: `https://open21.connect.trimble.com/bcf/2.1`
- Used for: BCF topics (notifications)

**Note:** Both use the same access token for authentication.

## Error Handling

**Common Errors:**

1. **401 Unauthorized:**
   - Check access token validity
   - Ensure token has BCF scope permissions

2. **404 Not Found:**
   - Verify project ID is correct
   - Check that project exists and user has access

3. **400 Bad Request:**
   - Validate email format in `assigned_to`
   - Ensure all required fields are provided

4. **Invalid Email:**
   - BCF API may reject invalid or non-existent Trimble Connect user emails
   - Verify users are members of the project

## Future Enhancements

### Optional: User Selection UI

**Current:** Manual comma-separated email input  
**Future:** Checkbox list of project users (code exists from previous implementation)

**Benefits:**
- Visual selection
- Auto-complete email addresses
- Prevent typos

**Implementation:**
- Use existing `get-project-users` endpoint
- Show checkbox UI
- Join selected emails with comma

### Enhanced Description

**Ideas:**
- Include file name in description
- Add creation timestamp
- Link to specific file (not just folder)
- Add analysis summary (for Raumbuch topics)

### Status Tracking

**Ideas:**
- Update topic status after user reviews file
- Link back to original upload action
- Add comments automatically

## Configuration

### Web.config (No changes needed)

BCF API uses the same authentication as other Trimble Connect APIs.

### Hardcoded Values

```csharp
// In controller
topicType = "Request"
topicStatus = "New"

// In HTML
title = "Raumbuch"
description = "Raumprogramm wurde in diesem Verzeichnis erstellt."
```

**To customize:** Edit defaults in `index.html` or make them configurable.

## Security Notes

1. **Access Token** - Passed in request body, consider session storage for production
2. **Email Validation** - Basic validation, enhance for production
3. **Project Access** - BCF API validates user access automatically
4. **Folder Permissions** - URL works only if user has folder access

## Files Modified/Created

### Modified:
- `RaumbuchService\Services\TrimbleConnectService.cs` - Added `CreateBcfTopicAsync()`
- `RaumbuchService\Controllers\RaumbuchController.cs` - Added `create-bcf-topic` endpoint
- `RaumbuchService\Models\NotificationModels.cs` - Added BCF request/response models
- `RaumbuchService\index.html` - Updated Step 2 UI for BCF topic creation

### New:
- `RaumbuchService\BCF_TOPIC_IMPLEMENTATION.md` - This documentation

### Deprecated (kept for reference):
- `EmailService.cs` - Email notification service (not used)
- `send-notification` endpoint - Email sending (not used)

## Comparison with Working Test Request

**Your Working Test:**
```json
POST https://open21.connect.trimble.com/bcf/2.1/projects/y7-N0uBcXNI/topics
{
    "title": "Test BCF 2",
    "topic_type": "Request",
    "description": "More details needed",
    "topic_status": "New",
    "assigned_to": "joachim.salomonsen@b.ch"
}
```

**Our Implementation:**
```csharp
POST https://open21.connect.trimble.com/bcf/2.1/projects/{projectId}/topics
{
    "title": "Raumbuch",
    "topic_type": "Request",
    "description": "Raumprogramm wurde in diesem Verzeichnis erstellt.\n\n...",
    "topic_status": "New",
    "assigned_to": "joachim.salomonsen@b.ch"
}
```

**Differences:**
- ? Same endpoint
- ? Same structure
- ? Dynamic project ID
- ? Enhanced description with folder link
- ? Support for multiple assignees (in description)

## Conclusion

BCF Topic implementation provides the best balance of:
- **Simplicity** - No external dependencies
- **Integration** - Native Trimble Connect feature
- **Usability** - Direct links to folders
- **Reliability** - Proven API endpoint

This replaces both TODO and email notification approaches with a cleaner, more maintainable solution.
