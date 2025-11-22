# Lokal Testing Guide - Raumbuch API

## ?? Oversikt

For å teste Raumbuch API lokalt trenger du:
1. ? Visual Studio med applikasjonen kjørende
2. ? Postman (eller lignende REST client)
3. ? Trimble Connect Access Token (ekte token)

---

## ?? Steg-for-steg Testing

### **Steg 1: Start applikasjonen** ??

1. Åpne **Visual Studio**
2. Trykk **F5** (eller klikk "Start")
3. IIS Express starter og åpner browser
4. **Noter porten** - f.eks. `https://localhost:44305/`

?? **Merk:** Port kan variere. Sjekk URL-en i browseren når appen starter.

---

### **Steg 2: Verifiser at API kjører** ?

**I browser, gå til:**
```
https://localhost:44305/api/values
```

**Forventet svar (JSON):**
```json
[
  "value1",
  "value2"
]
```

**Eller XML (hvis ikke WebApiConfig er oppdatert):**
```xml
<ArrayOfstring>
  <string>value1</string>
  <string>value2</string>
</ArrayOfstring>
```

? **Begge er OK!** API-en kjører!

? **Hvis du får 404:** Sjekk at URL-en matcher porten i IIS Express

---

### **Steg 3: Importer Postman Collection** ??

1. Åpne **Postman**
2. Klikk **Import**
3. Velg filen: `Postman_Collection_Raumbuch_Local.json`
4. Collection "Raumbuch API - Local Testing" vises
5. **VIKTIG:** Oppdater alle URLs til å bruke din port (44305 eller annen)

---

### **Steg 4: Test uten ekte token (forventet feil)** ??

**Test request 1: "Get Values"**
```
GET https://localhost:44305/api/values
```
- Skal returnere: `["value1","value2"]` ?

**Test request 2: "Import Template (Mock)"**
```
POST https://localhost:44305/api/raumbuch/import-template
```
- Sender mock token
- **Forventet resultat:** Feil 401 eller 500
- **Hvorfor:** Mock token er ikke gyldig i Trimble Connect

Dette bekrefter at API-en prøver å kalle Trimble Connect! ?

---

## ?? Hvordan få ekte Trimble Connect Access Token?

### **Alternativ A: OAuth Flow (manuelt)**

#### **Steg 1: Autoriser app**

Åpne i browser:
```
https://id.trimble.com/oauth/authorize?client_id=073a84b7-323b-43bf-b5a9-96bf17638dcc&response_type=code&redirect_uri=http://localhost:5005/callback/&scope=openid%20CST-PowerBI
```

*(Merk: Dette bruker din Client ID og Scope fra Web.config)*

#### **Steg 2: Login**
- Login med Trimble ID
- Godkjenn tilganger

#### **Steg 3: Copy authorization code**

Du blir redirected til:
```
http://localhost:5005/callback/?code=AUTHORIZATION_CODE_HER&state=...
```

**Kopier `code` parameteren**

#### **Steg 4: Exchange code for token**

I Postman, opprett ny request:

```
POST https://id.trimble.com/oauth/token
Content-Type: application/x-www-form-urlencoded

Body (x-www-form-urlencoded):
- grant_type: authorization_code
- code: [DIN_AUTHORIZATION_CODE]
- client_id: 073a84b7-323b-43bf-b5a9-96bf17638dcc
- client_secret: 7f4e6d7c6c0c45f387866e86e6eda65c
- redirect_uri: http://localhost:5005/callback/
```

**Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "..."
}
```

**Kopier `access_token`!** Dette er ditt Trimble Connect access token. ?

---

### **Alternativ B: Bruk desktop app token** (Raskere)

Hvis du har din desktop Raumbuch app kjørende:

1. Start desktop app
2. Login med Trimble Connect
3. Etter login, appen har et token i minnet
4. Legg til logging i `TrimbleAuthService.cs`:

```csharp
public async Task<bool> LoginAsync()
{
    // ... existing code ...
    
    AccessToken = tokenResponse.access_token;
    
    // ADD THIS LINE FOR DEBUGGING:
    System.IO.File.WriteAllText(@"C:\temp\token.txt", AccessToken);
    
    return true;
}
```

5. Start desktop app og login
6. Token blir skrevet til `C:\temp\token.txt`
7. Kopier token derfra

---

## ?? Test med ekte token

### **Forberedelser:**

1. Opprett en folder i Trimble Connect
2. Last opp en test Excel-fil (template)
3. Noter:
   - **Project ID** (fra URL i Trimble Connect)
   - **Folder ID** (fra URL)
   - **File ID** (kan hentes via API eller fra URL)

**Eksempel URL:**
```
https://web.connect.trimble.com/projects/abc123-project-id/data/folder/xyz789-folder-id
```

- Project ID: `abc123-project-id`
- Folder ID: `xyz789-folder-id`

---

### **Test 1: Import Template**

**I Postman:**

```
POST https://localhost:44305/api/raumbuch/import-template
Content-Type: application/json

{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",  // DITT EKTE TOKEN
  "projectId": "abc123-project-id",            // DIN PROJECT ID
  "templateFileId": "file-id-fra-trimble",     // FILE ID
  "targetFolderId": "xyz789-folder-id"         // FOLDER ID
}
```

**Forventet resultat:**
```json
{
  "success": true,
  "message": "Raumprogramm wurde erfolgreich erstellt.",
  "raumprogrammFileId": "ny-fil-id",
  "raumprogrammFileName": "Raumprogramm.xlsx"
}
```

? **Hvis dette fungerer:** Backend kan laste ned og opp filer til Trimble Connect!

---

### **Test 2: Create TODO**

```
POST https://localhost:44305/api/raumbuch/create-todo
Content-Type: application/json

{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "projectId": "abc123-project-id",
  "assignees": ["din@email.com"],
  "title": "Test TODO fra API",
  "label": "Raumbuch"
}
```

**Forventet resultat:**
```json
{
  "success": true,
  "message": "Aufgabe wurde erfolgreich erstellt.",
  "todoId": "todo-guid"
}
```

Sjekk i Trimble Connect at TODO-en ble opprettet! ?

---

## ?? Feilsøking

### **Error: "Access token is missing"**
- Sjekk at du sender `accessToken` i request body
- Sjekk at token ikke er utløpt (gyldig i 1 time)

### **Error: 401 Unauthorized**
- Token er ugyldig eller utløpt
- Hent nytt token fra Trimble OAuth flow

### **Error: 404 Not Found**
- Sjekk at URL-en er riktig: `/api/raumbuch/import-template`
- Sjekk at porten matcher IIS Express (44305 eller annen)

### **Error: 500 Internal Server Error**
- Sjekk **Output** vindu i Visual Studio for stack trace
- Sjekk at alle pakker er installert (ClosedXML, GeometryGym)

### **Error: "File not found in Trimble Connect"**
- File ID er feil
- Du har ikke tilgang til filen med det tokenet

### **Browser viser XML i stedet for JSON**
- Dette er normalt! Browser ber om XML som default
- Bruk Postman for JSON responses
- Eller oppdater `WebApiConfig.cs` for å fjerne XML formatter

---

## ?? Debugging i Visual Studio

### **Sett breakpoints:**

1. Åpne `RaumbuchController.cs`
2. Klikk i marginen ved linje 40 (første linje i `ImportTemplate`)
3. Start debugging (`F5`)
4. Send Postman request
5. Visual Studio stopper ved breakpoint
6. Steg gjennom koden med `F10` (Step Over) eller `F11` (Step Into)

### **Se variabler:**

Mens debugger er pauset:
- Hover over variabler for å se verdier
- Åpne **Locals** vindu (Debug ? Windows ? Locals)
- Åpne **Watch** vindu for å evaluere expressions

---

## ? Test Checklist

- [ ] Applikasjon starter i Visual Studio (`F5`)
- [ ] `/api/values` returnerer `["value1","value2"]` (JSON eller XML)
- [ ] Postman collection importert og URLs oppdatert til port 44305
- [ ] Mock requests gir forventet feil (401/500)
- [ ] Ekte access token hentet via OAuth flow
- [ ] `/import-template` med ekte token fungerer
- [ ] Fil lastes ned fra Trimble Connect (sjekk temp folder)
- [ ] Fil lastes opp til Trimble Connect (sjekk i Trimble web)
- [ ] `/create-todo` oppretter TODO i Trimble Connect

---

## ?? Neste steg

Når lokal testing fungerer:

1. ? Deploy til Azure
2. ? Test fra Azure URL
3. ? Lag Trimble Connect Extension (frontend)
4. ? Integrer med 3D viewer

---

## ?? Support

Hvis du står fast:
1. Sjekk **Output** vindu i Visual Studio for feilmeldinger
2. Sjekk **Postman Console** for detaljer om request/response
3. Test at Trimble credentials er riktige via manuell API-kall

God testing! ??
