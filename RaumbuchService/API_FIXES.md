# Raumbuch Service - API Issues Fixed

## Problems Identified and Fixed

### 1. ? Import Template Error: "Bitte alle Felder ausfüllen"

**Problem:**
- The JavaScript was not sending `projectId` in the request body to `/api/raumbuch/import-template`
- The backend controller requires `projectId` field in `ImportTemplateRequest` model

**Solution:**
- Updated `importTemplate()` function in `index.html` to include `projectId` in the request body
- Added validation to ensure all required fields are filled before making the API call

**Changes in index.html:**
```javascript
body: JSON.stringify({
    accessToken: token,
    projectId: projectId,        // ? ADDED
    templateFileId: templateFileId,
    targetFolderId: targetFolder
})
```

### 2. ? Create TODO Error: "undefined"

**Problem:**
- The TODO was being created successfully, but the UI showed "undefined" error
- The error handling was not checking for all possible response field names
- Some API responses might not have a `message` field

**Solution:**
- Improved error handling to check multiple possible field names: `message`, `exceptionMessage`, `Message`
- Added null-safe access to `todoId` field
- Added fallback messages for better user experience

**Changes in index.html:**
```javascript
if (response.ok && data.success) {
    const todoId = data.todoId || 'N/A';
    const message = data.message || 'Aufgabe wurde erfolgreich erstellt.';
    showResult('step2Result', 'success', `? ${message}<br>TODO ID: ${todoId}`);
} else {
    const errorMsg = data.message || data.exceptionMessage || data.Message || 'Unbekannter Fehler';
    showResult('step2Result', 'error', `? ${errorMsg}`);
}
```

### 3. ?? Callback URL Mismatch

**Problem:**
- `Web.config` had callback URL: `https://localhost:44305/callback.html`
- Registered callback URL in Trimble Connect: `http://localhost:5005/callback/`
- Mismatch causes OAuth authorization to fail

**Solution:**
- Updated `Web.config` to use the correct callback URL: `http://localhost:5005/callback/`
- Updated OAuth authorization link in `index.html` to use the same URL
- Created `CALLBACK_SETUP.md` with instructions for setting up local callback server

**Changes in Web.config:**
```xml
<add key="TRIMBLE_REDIRECT_URI" value="http://localhost:5005/callback/" />
```

**Changes in index.html:**
```html
<a href="https://id.trimble.com/oauth/authorize?client_id=073a84b7-323b-43bf-b5a9-96bf17638dcc&response_type=code&redirect_uri=http://localhost:5005/callback/&scope=openid%20CST-PowerBI">
```

### 4. ? Improved Error Handling for All API Calls

**Changes Applied to:**
- `importTemplate()` - Import template function
- `createTodo()` - Create TODO function  
- `importIfc()` - Import IFC function

**Improvements:**
- Consistent error message extraction from API responses
- Better handling of undefined/null values
- More user-friendly error messages
- Validation of all required fields before API calls

## Setup Instructions

### For Local Testing with OAuth:

1. **Option A: Run a simple HTTP server for callback (Recommended)**
   ```bash
   # Copy callback.html to a separate folder
   mkdir C:\temp\callback
   copy RaumbuchService\callback.html C:\temp\callback\
   
   # Run Python HTTP server
   cd C:\temp\callback
   python -m http.server 5005
   ```

2. **Option B: Update Trimble Connect registration**
   - Change callback URL to: `https://localhost:44305/callback.html`
   - Update `Web.config` and `index.html` accordingly

See `CALLBACK_SETUP.md` for detailed instructions.

## Testing

1. Start the application (F5 in Visual Studio)
2. Open browser to: `https://localhost:44305/index.html`
3. Get access token using Postman (see CALLBACK_SETUP.md)
4. Paste token into "Access Token" field
5. Enter Project ID: `y7-N0uBcXNI`
6. Enter Target Folder ID: `y2YBScEdiLI`
7. Click "?? Test Verbindung" to verify connection
8. Test each function:
   - **1?? Vorlage importieren**: Enter template file ID and click import
   - **2?? Aufgabe erstellen**: Enter title, assignees, and click create
   - **3?? IFC importieren**: Enter IFC file ID and Raumprogramm file ID, then import

## Expected Results

? **Import Template:**
- Success message with file name and ID
- Raumprogramm file ID automatically filled in Step 3

? **Create TODO:**
- Success message with TODO ID
- No "undefined" errors

? **Import IFC:**
- Success message with Raumbuch file name
- SOLL/IST analysis results displayed

## Files Modified

1. `RaumbuchService\index.html` - Fixed API requests and error handling
2. `RaumbuchService\Web.config` - Updated callback URL
3. `RaumbuchService\CALLBACK_SETUP.md` - NEW: Setup instructions
4. `RaumbuchService\API_FIXES.md` - NEW: This file

## No Backend Changes Required

The backend code (`RaumbuchController.cs`) was already correct. The issues were purely in the frontend JavaScript code and configuration.
