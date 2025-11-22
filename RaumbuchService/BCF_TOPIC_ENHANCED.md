# BCF Topic Enhanced Implementation

## What's New

### ?? Key Improvements

1. **`reference_links` Support**  
   Folder URLs are now added as proper BCF reference links instead of plain text in description

2. **Multiple Topic Creation**  
   Option to create separate BCF topic for each assignee (ensures everyone gets notified)

3. **Better Multi-Assignee Handling**  
   Choose between:
   - Single topic with multiple mentions
   - Separate topic per person

## Changes Made

### Backend

#### TrimbleConnectService.cs

**Updated Method Signature:**
```csharp
public async Task<string> CreateBcfTopicAsync(
    string projectId,
    string title,
    string description,
    string assignedTo,
    List<string> referenceLinks = null,  // NEW
    string topicType = "Request",
    string topicStatus = "New")
```

**Key Changes:**
- Added `referenceLinks` parameter for BCF-compliant link handling
- Uses `System.Dynamic.ExpandoObject` to conditionally add fields
- Only includes `reference_links` if list is provided

#### RaumbuchController.cs

**Enhanced Logic:**
1. Folder URL added to `reference_links` array (not description)
2. Option to create multiple topics via `CreateMultipleTopics` flag
3. Loop through all assignees if flag is enabled
4. Returns count of topics created

#### Models/NotificationModels.cs

**Updated Request:**
```csharp
public class CreateBcfTopicRequest
{
    // ...existing fields...
    public bool CreateMultipleTopics { get; set; }  // NEW
}
```

**Updated Response:**
```csharp
public class CreateBcfTopicResponse
{
    // ...existing fields...
    public List<string> AdditionalTopicGuids { get; set; }  // NEW
    public int TotalTopicsCreated { get; set; }  // NEW
}
```

### Frontend

#### index.html

**New Checkbox:**
```html
<input type="checkbox" id="createMultipleTopics">
Separates BCF Topic für jeden Empfänger erstellen
```

**Updated Description Help Text:**
```
Link zur Mappe wird automatisch als reference_link hinzugefügt.
```

**Enhanced Success Message:**
Shows total number of topics created when multiple topics are enabled.

## API Request Comparison

### Before (Link in Description)

```json
{
    "title": "Raumbuch",
    "topic_type": "Request",
    "description": "Raumprogramm wurde in diesem Verzeichnis erstellt.\n\nBitte klicken Sie hier, um es zu bearbeiten:\nhttps://web.connect.trimble.com/projects/y7-N0uBcXNI/data/folder/y2YBScEdiLI\n\nWeitere Empfänger: user2@example.com",
    "topic_status": "New",
    "assigned_to": "user1@example.com"
}
```

### After (Link in reference_links) ?

```json
{
    "title": "Raumbuch",
    "topic_type": "Request",
    "description": "Raumprogramm wurde in diesem Verzeichnis erstellt.\n\nWeitere Empfänger: user2@example.com",
    "topic_status": "New",
    "assigned_to": "user1@example.com",
    "reference_links": [
        "https://web.connect.trimble.com/projects/y7-N0uBcXNI/data/folder/y2YBScEdiLI"
    ]
}
```

**Benefits:**
- ? BCF-compliant structure
- ? Better UI rendering in Trimble Connect
- ? Clickable reference link (not just text)
- ? Cleaner description

## Multiple Topics Feature

### Use Case

**Scenario:** Need to notify 3 users about Raumprogramm creation

**Option 1: Single Topic (Default)**
- Creates 1 BCF topic
- `assigned_to`: First email
- `description`: Lists other emails
- ?? Only first person gets direct notification

**Option 2: Multiple Topics (Checkbox Enabled)** ?
- Creates 3 BCF topics (one per person)
- Each has same title, description, reference_links
- Each assigned to different person
- ? Everyone gets their own notification

### How It Works

```csharp
// Controller logic
if (emails.Count > 1 && request.CreateMultipleTopics)
{
    for (int i = 1; i < emails.Count; i++)
    {
        await tcService.CreateBcfTopicAsync(
            request.ProjectId,
            request.Title,
            description,
            emails[i],  // Different assignee each time
            referenceLinks,
            "Request",
            "New"
        );
    }
}
```

### UI Flow

1. Enter multiple emails: `user1@example.com,user2@example.com,user3@example.com`
2. Check "Separates BCF Topic für jeden Empfänger erstellen"
3. Click "BCF Topic erstellen"
4. Result: "BCF Topics wurden für 3 Empfänger erstellt."

### Response

```json
{
    "success": true,
    "message": "BCF Topics wurden für 3 Empfänger erstellt.",
    "topicGuid": "abc123...",  // First topic
    "additionalTopicGuids": ["def456...", "ghi789..."],  // Others
    "totalTopicsCreated": 3
}
```

## Testing

### Test reference_links Feature

1. Create BCF topic (default settings)
2. Open topic in Trimble Connect web
3. Verify folder link appears as clickable reference (not just text in description)

### Test Multiple Topics

1. Enter 3 emails: `user1@test.com,user2@test.com,user3@test.com`
2. Enable "Separates BCF Topic" checkbox
3. Click create
4. Verify:
   - 3 separate topics created
   - Each assigned to different user
   - All have same folder reference_link

### Test Single Topic (Multi-Mention)

1. Enter 3 emails
2. Keep checkbox disabled
3. Click create
4. Verify:
   - 1 topic created
   - Assigned to first user
   - Other users mentioned in description

## Advantages

### reference_links vs Plain Text

| Aspect | Plain Text (Old) | reference_links (New) ? |
|--------|------------------|-------------------------|
| BCF Compliant | ? | ? |
| Clickable in UI | Sometimes | Always |
| Structured Data | ? | ? |
| Multiple Links | Hard to parse | Native array |
| Export/Import | Loses context | Preserved |

### Multiple Topics vs Single Topic

| Aspect | Single Topic | Multiple Topics ? |
|--------|--------------|-------------------|
| Notifications | Only first user | All users |
| API Calls | 1 | N (one per user) |
| User Experience | Mixed | Consistent |
| Tracking | Harder | Easier (per user) |
| Performance | Faster | Slightly slower |

## Recommendations

### When to Use Multiple Topics

? **Use when:**
- 2+ users need immediate notification
- Each user needs to track separately
- Users might work independently on the task
- Important that no one misses the notification

? **Don't use when:**
- 10+ users (creates too many topics)
- Users work as a team (can share one topic)
- Just FYI notification (description mention is enough)

### Performance Considerations

**Single Topic:**
- 1 API call
- Fast

**Multiple Topics (3 users):**
- 3 API calls (sequential)
- ~3x time
- Still very fast (< 1 second typically)

**Recommendation:** Enable multiple topics for up to 5 users max.

## Error Handling

### Partial Failure Scenario

If creating additional topics fails for some users:

```csharp
try {
    // Create topic for user2
} catch (Exception ex) {
    Debug.WriteLine($"Failed for {user2}: {ex.Message}");
    // Continue with user3 (don't fail entire operation)
}
```

**Result:**
- Primary topic always created
- Best-effort for additional topics
- Success message includes actual count created

### Example

- Request: 4 users
- User 1: ? Created (primary)
- User 2: ? Created
- User 3: ? Failed (invalid email)
- User 4: ? Created

**Response:**
```json
{
    "success": true,
    "message": "BCF Topics wurden für 3 Empfänger erstellt.",
    "totalTopicsCreated": 3  // Not 4
}
```

## Future Enhancements

### 1. Batch API Call

Instead of sequential topic creation, use batch API if available:

```csharp
// Pseudo-code
await tcService.CreateMultipleBcfTopicsAsync(
    projectId,
    title,
    description,
    emails,  // All at once
    referenceLinks
);
```

**Benefits:**
- Single API call
- Faster
- Atomic operation (all or nothing)

### 2. Smart Assignee Distribution

For large teams, create one topic per team/role:

```csharp
// Group by domain
var groups = emails.GroupBy(e => e.Split('@')[1]);
foreach (var group in groups)
{
    // Create topic per company/domain
}
```

### 3. Reference Link Variations

Support multiple reference types:

```json
{
    "reference_links": [
        "https://web.connect.trimble.com/projects/.../folder/...",  // Folder
        "https://web.connect.trimble.com/projects/.../files/...",   // Specific file
        "https://external-site.com/documentation"                    // External
    ]
}
```

## Debugging

### Enable Debug Logging

```csharp
// In controller
System.Diagnostics.Debug.WriteLine($"Creating topic for {email}");
System.Diagnostics.Debug.WriteLine($"reference_links: {JsonConvert.SerializeObject(referenceLinks)}");
```

### Check Topic Creation in Trimble Connect

1. Open project web interface
2. Navigate to BCF Topics section
3. Find topics by title "Raumbuch"
4. Verify:
   - Correct assignee
   - reference_links present and clickable
   - Description content

### Common Issues

**Issue:** reference_links not showing
- **Cause:** BCF API version mismatch
- **Solution:** Ensure using BCF 2.1 endpoint

**Issue:** Multiple topics not created
- **Cause:** Checkbox not checked in UI
- **Solution:** Verify `createMultipleTopics` in request body

**Issue:** Topics created but users not notified
- **Cause:** Email not in Trimble Connect project
- **Solution:** Verify users are project members

## Summary

### What Changed

- ? Added `reference_links` parameter to `CreateBcfTopicAsync()`
- ? Folder URL now in `reference_links` array (BCF-compliant)
- ? Optional multiple topic creation (one per assignee)
- ? Enhanced response with topic count
- ? UI checkbox for multiple topics option

### What Stayed Same

- Single topic still default behavior
- Primary assignee still first email
- Same API endpoint (BCF 2.1)
- Backward compatible

### User Benefits

- ?? Better notification delivery (everyone notified if enabled)
- ?? Clickable folder links (reference_links)
- ?? Clear success feedback (topic count)
- ?? Flexible choice (single vs multiple topics)
