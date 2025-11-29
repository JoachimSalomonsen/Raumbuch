# Raumbuch - Trimble Connect Extension

**A comprehensive BIM room management solution for Trimble Connect**

Raumbuch is a Trimble Connect Extension that enables architects, facility managers, and construction professionals to manage room programs (Raumprogramm) and room books (Raumbuch) directly within the Trimble Connect platform. It provides SOLL/IST (target/actual) area analysis, IFC property management, and 3D visualization of room compliance status.

---

## 🌟 Key Features

| Feature | Description |
|---------|-------------|
| **📊 SOLL/IST Analysis** | Compares planned room areas (SOLL) from Excel templates with actual areas (IST) from IFC models |
| **📁 Room Program Management** | Create and manage room programs with Excel-based workflows |
| **🏗️ IFC Property Sets** | Read/write custom Raumbuch property sets to IFC files |
| **🎨 3D Visualization** | Color-code rooms in Trimble Connect 3D viewer by compliance status |
| **📧 BCF Topic Creation** | Create BCF topics for notifications and issue tracking |
| **☁️ Cloud Configuration** | Store and retrieve configurations via Azure Blob Storage |
| **📋 Inventory Management** | Manage room inventories and equipment lists |
| **📄 Room Sheets** | Generate individual Excel sheets for each room |

---

## 🏛️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Trimble Connect Platform                     │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────────────────────────┐ │
│  │ Raumbuch Manager│    │      Raumbuch 3D Viewer             │ │
│  │  (index.html)   │    │        (viewer.html)                │ │
│  │                 │    │                                     │ │
│  │ • Configuration │    │ • Room visualization                │ │
│  │ • SOLL/IST      │    │ • Color by status                   │ │
│  │ • Inventory     │    │ • Toggle categories                 │ │
│  │ • Notifications │    │ • Interactive filtering             │ │
│  └────────┬────────┘    └────────────────┬────────────────────┘ │
│           │                              │                       │
│           └──────────────┬───────────────┘                       │
│                          │                                       │
│                          ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │              Trimble Connect Workspace API                  │ │
│  │   (Authentication, Project Context, 3D Viewer Control)      │ │
│  └─────────────────────────────────────────────────────────────┘ │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Raumbuch Backend Service                        │
│                    (ASP.NET Web API)                             │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐ ┌───────────────┐ ┌─────────────────────┐  │
│  │ RaumbuchController│ TrimbleConnect│ │   RaumbuchAnalyzer  │  │
│  │ (20+ endpoints)   │    Service    │ │ (SOLL/IST Analysis) │  │
│  └─────────────────┘ └───────────────┘ └─────────────────────┘  │
│  ┌─────────────────┐ ┌───────────────┐ ┌─────────────────────┐  │
│  │ IfcEditorService│ │AzureStorage   │ │   EmailService      │  │
│  │ (GeometryGym)   │ │   Service     │ │                     │  │
│  └─────────────────┘ └───────────────┘ └─────────────────────┘  │
└───────────────────────────────┬─────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
┌───────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Trimble Connect│     │  Azure Blob     │     │    IFC Files    │
│      API      │     │    Storage      │     │  (GeometryGym)  │
│ (Files, BCF)  │     │ (Configurations)│     │                 │
└───────────────┘     └─────────────────┘     └─────────────────┘
```

---

## 💻 Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Backend** | ASP.NET Web API (.NET Framework 4.8) | REST API services |
| **Excel Processing** | ClosedXML | Read/write Excel files (Raumbuch, Raumprogramm) |
| **IFC Processing** | GeometryGym.IFC (0.1.21) | Parse and modify IFC files |
| **Cloud Storage** | Azure Blob Storage | Configuration persistence |
| **Frontend** | HTML5, JavaScript, CSS3 | Trimble Connect extensions |
| **3D Viewer** | Trimble Connect Workspace API | Room visualization |
| **Serialization** | Newtonsoft.Json (13.0.3) | JSON processing |
| **Hosting** | Azure App Service | Production deployment |

---

## 📋 API Endpoints

### Template & Raumbuch Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/raumbuch/import-template` | Import Excel template to create Raumprogramm |
| POST | `/api/raumbuch/create-raumbuch` | Create Raumbuch from template with column mappings |
| POST | `/api/raumbuch/update-raumbuch` | Update existing Raumbuch with new IFC data |
| POST | `/api/raumbuch/update-raumbuch-with-mappings` | Update Raumbuch using column mappings |
| POST | `/api/raumbuch/get-template-headers` | Get column headers from template for mapping |

### IFC Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/raumbuch/import-ifc` | Create Raumbuch from IFC + Raumprogramm |
| POST | `/api/raumbuch/analyze-rooms` | Perform SOLL/IST analysis and mark IFC rooms |
| POST | `/api/raumbuch/reset-ifc` | Remove Raumbuch PropertySets from IFC |
| POST | `/api/raumbuch/write-raumbuch-pset` | Write Raumbuch Pset to IFC spaces |
| POST | `/api/raumbuch/update-raumbuch-pset` | Update existing Raumbuch Pset in IFC |
| POST | `/api/raumbuch/delete-raumbuch-pset` | Remove Raumbuch Pset from IFC |
| POST | `/api/raumbuch/discover-properties` | Discover available properties in IFC files |
| POST | `/api/raumbuch/get-ist-from-ifc` | Get IST values from IFC grouped by category |

### Analysis & Summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/raumbuch/get-zusammenfassung` | Get summary data from Raumbuch Excel |
| POST | `/api/raumbuch/update-zusammenfassung` | Update summary with comments and status |
| POST | `/api/raumbuch/get-viewer-data` | Get rooms grouped by status for 3D viewer |

### Room Sheets & Inventory

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/raumbuch/create-room-sheets` | Create individual sheets for each room |
| POST | `/api/raumbuch/fill-inventory` | Populate room sheets with IFC objects |
| POST | `/api/raumbuch/update-inventory` | Update inventory data in room sheets |
| POST | `/api/raumbuch/delete-room-lists` | Delete inventory data from room sheets |

### Notifications

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/raumbuch/create-todo` | Create Trimble Connect TODO notification |
| POST | `/api/raumbuch/create-bcf-topic` | Create BCF topic with document reference |

---

## 📁 Project Structure

```
Raumbuch/
├── RaumbuchService/
│   ├── Config/
│   │   └── TrimbleConfig.cs           # Azure/environment configuration reader
│   ├── Controllers/
│   │   ├── RaumbuchController.cs      # Main API (25+ endpoints)
│   │   └── ProjectDataController.cs   # Configuration management
│   ├── Services/
│   │   ├── TrimbleConnectService.cs   # Trimble API wrapper (files, BCF, todos)
│   │   ├── RaumbuchAnalyzer.cs        # SOLL/IST analysis engine
│   │   ├── IfcEditorService.cs        # IFC file operations (GeometryGym)
│   │   ├── AzureStorageService.cs     # Azure Blob Storage operations
│   │   └── EmailService.cs            # Email notifications
│   ├── Models/
│   │   ├── ImportTemplateRequest.cs
│   │   ├── ImportIfcRequest.cs
│   │   ├── CreateBcfTopicRequest.cs
│   │   ├── ZusammenfassungRequest.cs
│   │   └── ...                        # 15+ request/response models
│   ├── index.html                     # Raumbuch Manager extension UI
│   ├── viewer.html                    # 3D Viewer extension UI
│   ├── Scripts/app.js                 # Workspace API integration
│   ├── RaumbuchManager.json           # Extension manifest
│   ├── Raumbuch3DViewer.json          # 3D Viewer extension manifest
│   └── Web.config                     # Application configuration
├── AZURE_DEPLOYMENT_GUIDE.md          # Complete Azure deployment guide
├── LOCAL_TESTING_GUIDE.md             # Local development instructions
├── TRIMBLE_CONFIG_GUIDE.md            # Trimble Connect configuration
└── RaumbuchService.sln                # Visual Studio solution
```

---

## 🚀 Quick Start

### Prerequisites

- Visual Studio 2019+ with ASP.NET and web development workload
- .NET Framework 4.8 SDK
- Azure subscription (for production deployment)
- Trimble Connect account and API credentials

### Local Development

1. **Clone Repository**
   ```bash
   git clone https://github.com/JoachimSalomonsen/Raumbuch.git
   cd Raumbuch
   ```

2. **Restore NuGet Packages**
   ```bash
   nuget restore RaumbuchService.sln
   ```

3. **Configure Credentials**
   
   Update `RaumbuchService/Web.config` with your credentials:
   ```xml
   <appSettings>
     <add key="TRIMBLE_CLIENT_ID" value="your_client_id" />
     <add key="TRIMBLE_CLIENT_SECRET" value="your_client_secret" />
     <add key="TRIMBLE_REDIRECT_URI" value="http://localhost:44305/callback" />
   </appSettings>
   ```

4. **Run in Visual Studio**
   - Open `RaumbuchService.sln`
   - Press F5 to start IIS Express
   - Navigate to `https://localhost:44305/`

---

## ☁️ Azure Deployment

### Resources

| Resource | Type | Location |
|----------|------|----------|
| `Raumbuch` | App Service | Sweden Central |
| `raumbuchstorage` | Storage Account | Norway East |
| `Connect_Extensions` | Resource Group | Norway East |

### Production URL

**https://raumbuch.azurewebsites.net/**

### Configuration Storage

Configurations are stored in Azure Blob Storage:
```
raumbuchstorage/
└── raumbuch-configs/
    └── {projectId}/
        ├── Config1.json
        ├── Config2.json
        └── ...
```

📖 See [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md) for complete deployment instructions.

---

## 🔐 Authentication

### Trimble Connect Extension (Production)

When running as a Trimble Connect extension, authentication is handled automatically via the Workspace API:

```javascript
// Authentication via Trimble Connect Extension API
const token = await API.extension.requestPermission(['data:read', 'data:write']);
const projectContext = await workspace.getContext();
```

### Local Development (OAuth2)

For local testing, use OAuth2 Authorization Code Flow:

1. Navigate to Trimble authorization URL
2. Login and obtain authorization code
3. Exchange code for access token

---

## 📊 SOLL/IST Analysis

The core feature of Raumbuch is comparing planned room areas (SOLL) with actual areas from IFC models (IST):

| Status | Condition | Color |
|--------|-----------|-------|
| **Erfüllt** | IST ≈ SOLL (within tolerance) | 🟢 Green |
| **Unterschritten** | IST < SOLL | 🔴 Red |
| **Überschritten** | IST > SOLL | ⚪ No color |

The analysis can be configured with custom tolerance values (e.g., ±5%).

---

## 📖 Related Documentation

| Document | Description |
|----------|-------------|
| [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md) | Complete Azure setup and deployment |
| [LOCAL_TESTING_GUIDE.md](LOCAL_TESTING_GUIDE.md) | Local development and testing |
| [TRIMBLE_CONFIG_GUIDE.md](TRIMBLE_CONFIG_GUIDE.md) | Trimble Connect configuration |
| [TRIMBLE_IFRAME_EMBEDDING.md](TRIMBLE_IFRAME_EMBEDDING.md) | iframe embedding security |
| [QUICK_START_TESTING.md](QUICK_START_TESTING.md) | Quick start for testing |

---

## 🛠️ Technical Summary

**Raumbuch** is a BIM (Building Information Modeling) extension for Trimble Connect that bridges the gap between architectural room planning and actual building data. Built on ASP.NET Web API (.NET Framework 4.8), it processes IFC files using the GeometryGym library to extract IfcSpace objects and their properties.

**Core Workflow:**
1. Import an Excel template containing planned room areas (Raumprogramm)
2. Parse IFC model files to extract actual room geometry and areas
3. Perform SOLL/IST (target/actual) comparison analysis
4. Generate Raumbuch Excel reports with detailed room data
5. Write analysis results back to IFC as custom PropertySets
6. Visualize compliance status in Trimble Connect 3D viewer

**Key Technical Aspects:**
- Uses ClosedXML for Excel file manipulation (reading templates, generating reports)
- Integrates with Trimble Connect API for file operations and BCF topic creation
- Stores user configurations in Azure Blob Storage for persistence across sessions
- Implements the Trimble Connect Workspace API for 3D viewer color-coding
- Supports OAuth2 authentication via Trimble Identity

---

## 🗄️ Database Schema Changes (Room Table)

### Current Room Table Structure

The Room table has been updated to use separate SOLL (planned) and IST (actual) columns for both net and gross areas:

| Column | Type | Description |
|--------|------|-------------|
| `RoomID` | INT | Primary key, auto-incremented |
| `RoomTypeID` | INT | Foreign key to RoomType |
| `Name` | NVARCHAR(100) | Room name |
| `NetAreaPlanned` | DECIMAL(18,2) | Planned net area (SOLL) in m² |
| `NetAreaActual` | DECIMAL(18,2) | Actual net area (IST) in m² |
| `GrossAreaPlanned` | DECIMAL(18,2) | Planned gross area (SOLL) in m² |
| `GrossAreaActual` | DECIMAL(18,2) | Actual gross area (IST) in m² |
| `PubliclyAccessible` | BIT | IFC Pset_SpaceCommon.PubliclyAccessible |
| `HandicapAccessible` | BIT | IFC Pset_SpaceCommon.HandicapAccessible |
| `IsExternal` | BIT | IFC Pset_SpaceCommon.IsExternal |
| `Description` | NVARCHAR(500) | IFC IfcSpace.Description |
| `ObjectType` | NVARCHAR(100) | IFC IfcSpace.ObjectType |
| `PredefinedType` | NVARCHAR(50) | IFC IfcSpace.PredefinedType |
| `ElevationWithFlooring` | DECIMAL(18,4) | IFC IfcSpace.ElevationWithFlooring |
| `ModifiedByUserID` | NVARCHAR(255) | User who last modified |
| `ModifiedDate` | DATETIME2 | Last modification date |

### Migration Notes

**⚠️ Important:** If you are upgrading from a previous version, the following columns have been **removed**:

| Removed Column | Replaced By |
|----------------|-------------|
| `AreaPlanned` | `NetAreaPlanned` |
| `AreaActual` | `NetAreaActual` |
| `NetArea` | `NetAreaPlanned` / `NetAreaActual` |
| `GrossArea` | `GrossAreaPlanned` / `GrossAreaActual` |

### SOLL/IST Separation

The new column structure separates planned (SOLL) and actual (IST) values:

**SOLL Pages (Raumprogramm Übersicht):**
- Use `NetAreaPlanned` for planned net area
- Use `GrossAreaPlanned` for planned gross area

**IST Pages (Ausgeführt):**
- Use `NetAreaActual` for actual net area
- Use `GrossAreaActual` for actual gross area

### Example Queries

**Get SOLL values for room program overview:**
```sql
SELECT r.Name, r.NetAreaPlanned, r.GrossAreaPlanned, rt.Name AS RoomType
FROM Room r
INNER JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
ORDER BY rt.Name, r.Name;
```

**Get IST values for actual data view:**
```sql
SELECT r.Name, r.NetAreaActual, r.GrossAreaActual, rt.Name AS RoomType
FROM Room r
INNER JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
ORDER BY rt.Name, r.Name;
```

**Compare SOLL vs IST (Analysis):**
```sql
SELECT 
    rt.Name AS RoomType,
    SUM(r.NetAreaPlanned) AS TotalNetAreaPlanned,
    SUM(r.NetAreaActual) AS TotalNetAreaActual,
    CASE 
        WHEN SUM(r.NetAreaPlanned) > 0 
        THEN (SUM(r.NetAreaActual) / SUM(r.NetAreaPlanned)) * 100 
        ELSE 0 
    END AS PercentageFulfilled
FROM Room r
INNER JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
GROUP BY rt.Name
ORDER BY rt.Name;
```

### RoomInventory SOLL/IST Handling

The `RoomInventory` table continues to use `ValuePlanned` (SOLL) and `ValueActual` (IST) for dynamic property values:

- **SOLL pages** must use `ValuePlanned`
- **IST pages** must use `ValueActual`
- The `DataType` from `InventoryTemplate` should be respected when displaying/editing values

---

## 👤 Support

- **Developer**: Joachim Salomonsen
- **Azure Resource Group**: Connect_Extensions
- **App Service**: Raumbuch

---

## 📝 License

Internal project - proprietary
