# Trimble Connect Configuration Guide

## ?? Hvor finner du Trimble Connect credentials?

### 1. Client ID og Client Secret

Disse må du få fra Trimble Connect Developer Portal:

**Steg:**
1. Gå til: **Trimble Developer Console** (https://developer.connect.trimble.com/)
2. Login med Trimble ID
3. Gå til **"My Apps"** eller **"Applications"**
4. Finn din app (eller opprett ny)
5. Kopier:
   - **Client ID** (Application ID)
   - **Client Secret** (må kanskje genereres)

---

## ?? Konfigurere Web.config (Lokal utvikling)

### Finn og erstatt verdier i `Web.config`:

```xml
<appSettings>
  <!-- Erstatt disse verdiene: -->
  <add key="TRIMBLE_CLIENT_ID" value="DIN_CLIENT_ID_HER"/>
  <add key="TRIMBLE_CLIENT_SECRET" value="DIN_CLIENT_SECRET_HER"/>
  
  <!-- Disse kan være som de er (standard verdier): -->
  <add key="TRIMBLE_BASE_URL" value="https://app.connect.trimble.com/tc/api/2.0"/>
  <add key="TRIMBLE_AUTH_URL" value="https://id.trimble.com/oauth/authorize"/>
  <add key="TRIMBLE_TOKEN_URL" value="https://id.trimble.com/oauth/token"/>
  <add key="TRIMBLE_SCOPE" value="openid"/>
  <add key="TRIMBLE_REDIRECT_URI" value="http://localhost:3000/callback"/>
</appSettings>
```

---

## ?? Konfigurere Azure App Service

**Når du deployer til Azure, skal du IKKE bruke Web.config for secrets!**

### I Azure Portal:

1. Gå til: **Azure Portal** ? **App Service** ? **Raumbuch**
2. Velg: **Configuration** ? **Application Settings**
3. Klikk: **+ New application setting**
4. Legg til følgende:

| Name | Value |
|------|-------|
| `TRIMBLE_CLIENT_ID` | `din-client-id` |
| `TRIMBLE_CLIENT_SECRET` | `din-client-secret` |
| `TRIMBLE_BASE_URL` | `https://app.connect.trimble.com/tc/api/2.0` |
| `TRIMBLE_AUTH_URL` | `https://id.trimble.com/oauth/authorize` |
| `TRIMBLE_TOKEN_URL` | `https://id.trimble.com/oauth/token` |
| `TRIMBLE_SCOPE` | `openid` |
| `TRIMBLE_REDIRECT_URI` | `https://raumbuch.azurewebsites.net/callback` |

5. Klikk: **Save**
6. App Service vil restarte automatisk

---

## ?? Sikkerhet

### ? Ikke gjør dette:
- Committe `Web.config` med ekte secrets til Git
- Del Client Secret i koden eller README

### ? Gjør dette:
- Bruk Azure App Settings for produksjon
- For lokal utvikling: Bruk `Web.config` (som er i `.gitignore`)
- For team: Bruk User Secrets eller Environment Variables

---

## ?? Test konfigurasjonen

### Metode 1: Start applikasjonen
```
F5 i Visual Studio
```

Hvis konfigurasjon mangler, vil du få en feilmelding:
```
Configuration 'TRIMBLE_CLIENT_ID' is missing. Add it to Azure App Settings or Web.config.
```

### Metode 2: Test endpoint
```bash
GET http://localhost:[port]/api/raumbuch/test-config
```

(Du kan lage en test endpoint hvis ønskelig)

---

## ?? .gitignore

Sørg for at `Web.config` transformasjoner ikke committes:

```gitignore
# Web.config transformations
Web.config
Web.Debug.config
Web.Release.config
```

---

## ?? Deployment Flow

```
Lokal utvikling:
?? Web.config (local secrets)
?? TrimbleConfig.cs leser fra appSettings

      ? Publish

Azure Production:
?? Azure App Settings (environment variables)
?? TrimbleConfig.cs leser fra Environment.GetEnvironmentVariable()
```

Vår `TrimbleConfig.cs` håndterer automatisk begge scenarioer:

```csharp
private static string GetConfigValue(string key, string defaultValue = null)
{
    // Try Azure App Settings (Environment Variables) first
    string value = Environment.GetEnvironmentVariable(key);

    // Fallback to Web.config appSettings
    if (string.IsNullOrWhiteSpace(value))
    {
        value = ConfigurationManager.AppSettings[key];
    }

    // Use default if still not found
    if (string.IsNullOrWhiteSpace(value))
    {
        if (defaultValue != null)
            return defaultValue;

        throw new ConfigurationErrorsException(
            $"Configuration '{key}' is missing.");
    }

    return value;
}
```

---

## ? Hvis du ikke har Trimble Connect App registrert

### Opprett ny app:

1. Gå til: https://developer.connect.trimble.com/
2. Klikk: **"Create Application"**
3. Fyll inn:
   - **Name:** Raumbuch Manager
   - **Description:** Raumprogramm and Raumbuch management for Trimble Connect
   - **Redirect URI:** `https://raumbuch.azurewebsites.net/callback`
   - **Scopes:** `openid`, `project.read`, `project.write`
4. Submit
5. Kopier **Client ID** og **Client Secret**

---

## ?? Neste steg

1. ? Erstatt verdier i `Web.config`
2. ? Build prosjektet (`Ctrl+Shift+B`)
3. ? Start applikasjonen (`F5`)
4. ? Test med Postman
5. ? Deploy til Azure
6. ? Konfigurer Azure App Settings
7. ? Test fra Azure URL

---

## ?? Support

Hvis du får feil:
- Sjekk at Client ID/Secret er korrekt
- Sjekk at Azure App Settings er satt
- Sjekk at App Service har restartet etter config-endring
