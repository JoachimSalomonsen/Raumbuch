# Raumbuch
Trimble Connect Extension for Raumbuch

# ?? Raumbuch Service - Technical Overview

**Backend API for Trimble Connect Raumbuch Extension**

This ASP.NET Web API (.NET Framework 4.8) service provides 5 main endpoints for managing "Raumprogramm" (room program) and "Raumbuch" (room book) workflows in Trimble Connect projects.

---

## ?? Key Features

| Feature | Description |
|---------|-------------|
| **Import Template** | Duplicates Raumprogramm template to project folder |
| **Create TODO** | Creates Trimble Connect TODO notification |
| **Import IFC** | Creates Raumbuch from IFC + Raumprogramm (SOLL/IST analysis) |
| **Analyze Rooms** | Performs SOLL/IST comparison |
| **Reset IFC** | Removes Raumbuch PropertySets from IFC file |

---

## ?? API Endpoints

### 1. Import Template
**POST** `/api/raumbuch/import-template`

Duplicates a Raumprogramm Excel template file.

**Request:**
```json
{
  "accessToken": "string",
  "projectId": "string",
  "templateFileId": "string",
  "targetFolderId": "string"
}
```

**Response:**
```json
{
  "success": true,
  "raumprogrammFileId": "abc123",
  "message": "Raumprogramm erfolgreich erstellt"
}
```

---

### 2. Create TODO
**POST** `/api/raumbuch/create-todo`

Creates a Trimble Connect TODO notification.

**Request:**
```json
{
  "accessToken": "string",
  "projectId": "string",
  "assignees": ["user@example.com"],
  "title": "Raumprogramm erstellt",
  "label": "Raumbuch"
}
```

**Response:**
```json
{
  "success": true,
  "todoId": "todo123",
  "message": "TODO erfolgreich erstellt"
}
```

---

### 3. Import IFC
**POST** `/api/raumbuch/import-ifc`

Creates Raumbuch from IFC file and Raumprogramm (SOLL/IST comparison).

**Request:**
```json
{
  "accessToken": "string",
  "projectId": "string",
  "ifcFileId": "string",
  "raumprogrammFileId": "string",
  "targetFolderId": "string"
}
```

**Response:**
```json
{
  "success": true,
  "raumbuchFileId": "xyz789",
  "updatedIfcFileId": "ifc456",
  "message": "Raumbuch erfolgreich erstellt"
}
```

---

### 4. Analyze Rooms
**POST** `/api/raumbuch/analyze-rooms`

Performs SOLL/IST room comparison.

**Request:**
```json
{
  "accessToken": "string",
  "projectId": "string",
  "raumprogrammFileId": "string",
  "ifcFileId": "string"
}
```

**Response:**
```json
{
  "success": true,
  "analysis": {
    "totalRoomsSOLL": 150,
    "totalRoomsIST": 148,
    "matchedRooms": 140,
    "missingRooms": 10,
    "extraRooms": 8,
    "areaDifference": 25.5
  }
}
```

---

### 5. Reset IFC
**POST** `/api/raumbuch/reset-ifc`

Removes Raumbuch PropertySets from IFC file (cleanup).

**Request:**
```json
{
  "accessToken": "string",
  "projectId": "string",
  "ifcFileId": "string",
  "targetFolderId": "string"
}
```

**Response:**
```json
{
  "success": true,
  "cleanedIfcFileId": "ifc999",
  "message": "PropertySets erfolgreich entfernt"
}
```

---

## ?? Technology Stack

| Component | Technology |
|-----------|------------|
| **Framework** | ASP.NET Web API (.NET Framework 4.8) |
| **Excel Processing** | EPPlus (4.5.3.3) |
| **IFC Processing** | GeometryGym.Ifc (0.2.1) |
| **HTTP Client** | Newtonsoft.Json (13.0.3) |
| **Deployment** | Azure App Service |

---

## ?? Dependencies (NuGet)

```xml
<package id="EPPlus" version="4.5.3.3" />
<package id="GeometryGym.Ifc" version="0.2.1" />
<package id="Newtonsoft.Json" version="13.0.3" />
<package id="Microsoft.AspNet.WebApi" version="5.3.0" />
<package id="Microsoft.AspNet.Cors" version="5.3.0" />
```

---

## ?? Configuration

### Azure App Settings

```json
{
  "TrimbleClientId": "your_client_id",
  "TrimbleClientSecret": "your_client_secret",
  "TrimbleRedirectUri": "https://raumbuch.azurewebsites.net/callback"
}
```

### Local Development (Web.config)

```xml
<appSettings>
  <add key="TrimbleClientId" value="your_dev_client_id" />
  <add key="TrimbleClientSecret" value="your_dev_client_secret" />
  <add key="TrimbleRedirectUri" value="http://localhost:3000/callback" />
</appSettings>
```

---

## ?? Setup & Installation

### Local Development

1. **Clone Repository**
```bash
git clone https://github.com/JoachimSalomonsen/Raumbuch.git
cd RaumbuchService
```

2. **Restore NuGet Packages**
```bash
nuget restore RaumbuchService.sln
```

3. **Update Web.config**
Add your Trimble Connect credentials.

4. **Run in Visual Studio**
Press F5 to start IIS Express.

---

### Azure Deployment

1. **Publish from Visual Studio**
   - Right-click project ? Publish
   - Choose Azure App Service
   - Select existing: `Connect_Extensions/Raumbuch`

2. **Configure App Settings in Azure Portal**
   - Navigate to App Service
   - Settings ? Configuration
   - Add `TrimbleClientId`, `TrimbleClientSecret`, `TrimbleRedirectUri`

3. **Test Deployment**
```bash
curl https://raumbuch.azurewebsites.net/api/raumbuch/test
```

---

## ?? Authentication Flow

**Option A: OAuth2 Authorization Code Flow (testing)**
```bash
1. GET https://id.trimble.com/oauth/authorize?client_id=YOUR_CLIENT_ID&response_type=code&redirect_uri=http://localhost:3000/callback&scope=openid
2. Login ? Copy authorization code
3. POST https://id.trimble.com/oauth/token
   Body: grant_type=authorization_code&code=...&client_id=...&client_secret=...
```

**Option B: Trimble Connect Extension (production)**
```javascript
const token = window.parent.TC.auth.getToken();
```

### 2. Test Workflow

**Schritt 1: Import Template**
```bash
POST http://localhost:[port]/api/raumbuch/import-template
Content-Type: application/json

{
  "accessToken": "YOUR_TOKEN",
  "projectId": "YOUR_PROJECT_ID",
  "templateFileId": "TEMPLATE_FILE_ID",
  "targetFolderId": "FOLDER_ID"
}
```

**Schritt 2: Create TODO**
```bash
POST http://localhost:[port]/api/raumbuch/create-todo
Content-Type: application/json

{
  "accessToken": "YOUR_TOKEN",
  "projectId": "YOUR_PROJECT_ID",
  "assignees": ["user@example.com"],
  "title": "Raumprogramm erstellt",
  "label": "Raumbuch"
}
```

**Schritt 3: Import IFC**
```bash
POST http://localhost:[port]/api/raumbuch/import-ifc
Content-Type: application/json

{
  "accessToken": "YOUR_TOKEN",
  "projectId": "YOUR_PROJECT_ID",
  "ifcFileId": "IFC_FILE_ID",
  "raumprogrammFileId": "RAUMPROGRAMM_FILE_ID",
  "targetFolderId": "FOLDER_ID"
}
```

---

## ?? Projektstruktur

```
RaumbuchService/
??? Config/
?   ??? TrimbleConfig.cs          # Azure App Settings reader
??? Controllers/
?   ??? RaumbuchController.cs     # Main API controller (5 endpoints + Excel helpers)
??? Models/
?   ??? ImportTemplateRequest.cs
?   ??? CreateTodoRequest.cs
?   ??? ImportIfcRequest.cs
?   ??? AnalyzeRoomsRequest.cs
?   ??? ResetIfcRequest.cs
??? Services/
?   ??? TrimbleConnectService.cs  # Trimble API wrapper
?   ??? RaumbuchAnalyzer.cs       # SOLL/IST analysis
?   ??? IfcEditorService.cs       # IFC file editing (GeometryGym)
??? packages.config               # NuGet dependencies
```

---

## ?? N�chste Schritte

### Phase 1: Backend Testing ?
- [x] Implementiere alle Services
- [x] Implementiere Excel helpers
- [x] Implementiere IFC editor
- [ ] **TODO: Restore NuGet Packages**
- [ ] **TODO: Build Project**
- [ ] **TODO: Test locally with Postman**

### Phase 2: Azure Deployment
- [ ] Configure App Settings in Azure
- [ ] Publish to Azure
- [ ] Test from Azure URL

### Phase 3: Frontend (Trimble Connect Extension)
- [ ] Create extension structure (HTML/JS)
- [ ] Implement UI buttons
- [ ] Register in Trimble Connect
- [ ] Test in 3D viewer

---

## ?? Frontend Interface

The service includes a local testing interface at `RaumbuchService/index.html` with features:

- Configuration management (save/load project settings)
- Step-by-step workflow UI
- BCF topic creation
- IFC import and processing
- Pset management (write/update/delete)
- Room sheets and inventory management

**To use:**
1. Start the RaumbuchService locally
2. Open `RaumbuchService/index.html` in your browser
3. Enter your Trimble Connect access token and project ID
4. Follow the step-by-step workflow

---

## ?? Support

Bei Fragen kontaktiere:
- Entwickler: Joachim Salomonsen
- Azure Resource Group: Connect_Extensions
- App Service: Raumbuch

---

## ?? Lizenz

Internes Projekt

---

## ☁️ Azure Deployment

The application is deployed to **Azure App Service** and uses **Azure Blob Storage** for configuration management:

- **App Service**: `Raumbuch` (Sweden Central)
- **Storage Account**: `raumbuchstorage` (Norway East)
- **Resource Group**: `Connect_Extensions`
- **Access URL**: `https://raumbuch.azurewebsites.net/`

### Key Features in Azure:
- ✅ **Configuration Storage**: JSON configurations saved to Azure Blob Storage
- ✅ **Project Organization**: Configurations organized by project number
- ✅ **Web-Based Access**: No local installation required
- ✅ **Automatic Fallback**: Works locally without Azure Storage configured

📖 **See [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md) for complete deployment and configuration instructions.**
