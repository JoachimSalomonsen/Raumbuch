# Raumbuch Service - Azure Web API

## ?? Übersicht

Dieser Service ermöglicht die Verwaltung von Raumprogrammen und Raumbüchern in Trimble Connect.

**Workflow:**
1. **Vorlage importieren** ? Erstellt Raumprogramm.xlsx
2. **Aufgabe erstellen** ? Benachrichtigt Benutzer
3. **Raumprogramm bearbeiten** ? In Excel for Web
4. **IFC importieren** ? Erstellt Raumbuch.xlsx mit SOLL/IST-Analyse
5. **Räume analysieren** ? Markiert Räume in IFC mit Pset "Überprüfung der Raumkategorie"
6. **IFC zurücksetzen** ? Entfernt Analyse-Markierungen

---

## ? Implementierungsstatus

### Backend (komplett implementiert):
- ? TrimbleConnectService - Trimble Connect API wrapper
- ? RaumbuchAnalyzer - SOLL/IST Analyse
- ? IfcEditorService - IFC file editing (mit GeometryGymIFC 0.1.21)
- ? RaumbuchController - 5 API endpoints
- ? Excel Integration - ClosedXML (ReadSollFromExcel, CreateRaumbuchExcel)

### Pakete installiert:
- ? GeometryGymIFC 0.1.21
- ? ClosedXML 0.102.3
- ? Newtonsoft.Json 13.0.3

---

## ?? Setup

### 1. NuGet Packages wiederherstellen

**WICHTIG: Führe dies jetzt aus!**

In Visual Studio:
1. Rechtsklick auf Lösung ? **"NuGet-Pakete wiederherstellen"**
2. Oder in **Package Manager Console**:
   ```powershell
   Update-Package -reinstall
   ```

### 2. Build prüfen

Nach Package Restore:
```
Ctrl+Shift+B
```

Sollte ohne Fehler kompilieren.

### 3. Azure App Settings konfigurieren

Im Azure Portal ? App Service ? Configuration ? Application Settings:

```
TRIMBLE_CLIENT_ID = <your-client-id>
TRIMBLE_CLIENT_SECRET = <your-client-secret>
TRIMBLE_AUTH_URL = https://id.trimble.com/oauth/authorize
TRIMBLE_TOKEN_URL = https://id.trimble.com/oauth/token
TRIMBLE_SCOPE = openid
TRIMBLE_REDIRECT_URI = <your-redirect-uri>
TRIMBLE_BASE_URL = https://app.connect.trimble.com/tc/api/2.0
```

### 4. Lokal Entwicklung

Für lokales Testing, füge in `Web.config` ? `<appSettings>` hinzu:

```xml
<appSettings>
  <add key="TRIMBLE_CLIENT_ID" value="your-client-id" />
  <add key="TRIMBLE_CLIENT_SECRET" value="your-client-secret" />
  <add key="TRIMBLE_BASE_URL" value="https://app.connect.trimble.com/tc/api/2.0" />
</appSettings>
```

### 5. Publizieren nach Azure

1. Rechtsklick auf Projekt ? **"Publish"**
2. Wähle dein Azure App Service: **Raumbuch**
3. Klick **"Publish"**

---

## ?? API Endpunkte

### Base URL
```
Lokal:  http://localhost:[port]/api/raumbuch
Azure:  https://raumbuch.azurewebsites.net/api/raumbuch
```

---

### 1. Vorlage importieren

**POST** `/import-template`

Importiert eine Excel-Vorlage und erstellt Raumprogramm.xlsx.

**Request:**
```json
{
  "accessToken": "bearer-token-from-trimble",
  "projectId": "project-guid",
  "templateFileId": "file-guid",
  "targetFolderId": "folder-guid"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Raumprogramm wurde erfolgreich erstellt.",
  "raumprogrammFileId": "file-guid",
  "raumprogrammFileName": "Raumprogramm.xlsx"
}
```

---

### 2. Aufgabe erstellen

**POST** `/create-todo`

Erstellt eine Aufgabe in Trimble Connect.

**Request:**
```json
{
  "accessToken": "bearer-token",
  "projectId": "project-guid",
  "assignees": ["user1@example.com", "user2@example.com"],
  "title": "Raumprogramm wurde erstellt",
  "label": "Raumbuch"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Aufgabe wurde erfolgreich erstellt.",
  "todoId": "todo-guid"
}
```

---

### 3. IFC importieren

**POST** `/import-ifc`

Importiert IFC und erstellt Raumbuch.xlsx mit SOLL/IST-Analyse.

**Request:**
```json
{
  "accessToken": "bearer-token",
  "projectId": "project-guid",
  "ifcFileId": "ifc-file-guid",
  "raumprogrammFileId": "raumprogramm-file-guid",
  "targetFolderId": "folder-guid"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Raumbuch wurde erfolgreich erstellt.",
  "raumbuchFileId": "file-guid",
  "raumbuchFileName": "Raumbuch.xlsx",
  "analysis": [
    {
      "roomCategory": "Meeting Room",
      "sollArea": 45.0,
      "istArea": 50.0,
      "percentage": 111.11,
      "isOverLimit": true
    }
  ]
}
```

**Raumbuch Excel Format:**

| Raumtyp | Raum ID | Fläche IST (m²) | SIA d0165 | SOLL Fläche (m²) | SOLL/IST (%) | Differenz (m²) |
|---------|---------|-----------------|-----------|------------------|--------------|----------------|
| Meeting | 101     | 15.5            | ...       | 15.0             | 103.3        | +0.5           |
| Meeting | 102     | 18.2            | ...       | 15.0             | 121.3        | +3.2           |

---

### 4. Räume analysieren

**POST** `/analyze-rooms`

Schreibt Pset "Überprüfung der Raumkategorie" in IFC-Datei.

**Request:**
```json
{
  "accessToken": "bearer-token",
  "projectId": "project-guid",
  "ifcFileId": "ifc-file-guid",
  "raumbuchFileId": "raumbuch-file-guid",
  "targetFolderId": "folder-guid"
}
```

**Response:**
```json
{
  "success": true,
  "message": "5 Räume wurden markiert.",
  "updatedIfcFileId": "file-guid",
  "roomsMarked": 5,
  "markedRoomNames": ["101", "102", "103", "104", "105"]
}
```

**IFC Pset Details:**
```
Pset: "Überprüfung der Raumkategorie"
?? "Prozentuale Fläche" : IfcReal (z.B. 115.5)
?? "Über angegebener Raumfläche" : IfcBoolean (true wenn > 100%)
```

---

### 5. IFC zurücksetzen

**POST** `/reset-ifc`

Entfernt Pset "Überprüfung der Raumkategorie" aus IFC.

**Request:**
```json
{
  "accessToken": "bearer-token",
  "projectId": "project-guid",
  "ifcFileId": "ifc-file-guid",
  "targetFolderId": "folder-guid"
}
```

**Response:**
```json
{
  "success": true,
  "message": "5 Analyse-Markierungen wurden entfernt.",
  "updatedIfcFileId": "file-guid",
  "psetsRemoved": 5
}
```

---

## ?? Testing mit Postman

### 1. Access Token erhalten

Für Testing benötigst du einen Trimble Connect Access Token.

**Option A: OAuth Flow (manuell)**
```
1. Browser: https://id.trimble.com/oauth/authorize?client_id=YOUR_CLIENT_ID&response_type=code&redirect_uri=http://localhost:3000/callback&scope=openid
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

## ?? Nächste Schritte

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

## ?? Trimble Connect Extension (Frontend)

Nach dem Backend-Setup, erstelle eine Trimble Connect Web Extension:

**Structure:**
```
extension/
??? manifest.json
??? index.html
??? app.js
??? styles.css
```

**Example manifest.json:**
```json
{
  "name": "Raumbuch Manager",
  "version": "1.0.0",
  "description": "Raumprogramm und Raumbuch Verwaltung",
  "extensionType": "panel",
  "main": "index.html"
}
```

**Example app.js:**
```javascript
// Get access token from Trimble Connect
const token = await window.parent.TC.getToken();

// Call backend
async function importTemplate(templateFileId, folderId) {
  const response = await fetch('https://raumbuch.azurewebsites.net/api/raumbuch/import-template', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      accessToken: token,
      projectId: window.parent.TC.getProjectId(),
      templateFileId: templateFileId,
      targetFolderId: folderId
    })
  });
  
  const data = await response.json();
  console.log('Raumprogramm created:', data);
}
```

---

## ?? Support

Bei Fragen kontaktiere:
- Entwickler: Joachim Salomonsen
- Azure Resource Group: Connect_Extensions
- App Service: Raumbuch

---

## ?? Lizenz

Internes Projekt
