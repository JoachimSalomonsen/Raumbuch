# GeometryGym IFC Integration Guide

## Problem
`GeometryGymIFC` package is not available on public NuGet in the expected version.

---

## Solution Options

### **Option 1: Use DLL from your desktop app** ? (Recommended)

1. Locate `GeometryGymIFC.dll` in your desktop app:
   ```
   C:\Users\...\Raumbuch\bin\Debug\GeometryGymIFC.dll
   ```

2. Copy DLL to Web API project:
   ```
   RaumbuchService\lib\GeometryGymIFC.dll
   ```

3. Add reference in Visual Studio:
   - Right-click **References** ? **Add Reference**
   - Browse ? Select `GeometryGymIFC.dll`
   - ? Check "Copy Local"

4. Copy your existing `IfcEditor.cs` code:
   - From: `Raumbuch\IfcEditor.cs` (desktop app)
   - To: `RaumbuchService\Services\IfcEditorService.cs`
   - Replace the placeholder code

---

### **Option 2: Install from NuGet (if available)**

Try these package sources:

1. **NuGet.org** (official):
   ```powershell
   Install-Package GeometryGymIFC
   ```

2. **GitHub Packages** (if available):
   ```powershell
   Install-Package GeometryGymIFC -Source https://nuget.pkg.github.com/GeometryGym/index.json
   ```

3. **MyGet** (alternative feed):
   ```powershell
   Install-Package GeometryGymIFC -Source https://www.myget.org/F/geometrygym/api/v3/index.json
   ```

---

### **Option 3: Build from source**

If the package is not available, build from source:

1. Clone repository:
   ```bash
   git clone https://github.com/GeometryGym/GeometryGymIFC.git
   ```

2. Build solution:
   ```bash
   cd GeometryGymIFC
   dotnet build
   ```

3. Copy compiled DLL to your project (see Option 1)

---

## After Installing GeometryGym

1. **Uncomment code** in `IfcEditorService.cs`:
   - Remove `NotImplementedException` placeholders
   - Uncomment implementation blocks

2. **Add using statement**:
   ```csharp
   using GeometryGym.Ifc;
   ```

3. **Test build**:
   ```
   Ctrl+Shift+B
   ```

---

## Copy Existing Code

If you want to reuse your desktop app's `IfcEditor.cs`:

### Files to copy:

```
FROM Desktop App:
?? Raumbuch\IfcEditor.cs
?? Raumbuch\bin\Debug\GeometryGymIFC.dll
?? Raumbuch\bin\Debug\*.dll (dependencies)

TO Web API:
?? RaumbuchService\Services\IfcEditorService.cs
?? RaumbuchService\lib\GeometryGymIFC.dll
```

### Adjustments needed:

1. **Namespace**: Change from `Raumbuch` to `RaumbuchService.Services`
2. **Remove WinForms dependencies** (if any)
3. **Keep business logic** (UpdateSpaces, ClonePset, etc.)

---

## Current Status

? **Working without GeometryGym:**
- `/api/raumbuch/import-template` - Import Excel template
- `/api/raumbuch/create-todo` - Create TODO in Trimble Connect

? **Requires GeometryGym:**
- `/api/raumbuch/import-ifc` - Read IFC spaces
- `/api/raumbuch/analyze-rooms` - Mark rooms in IFC
- `/api/raumbuch/reset-ifc` - Remove Pset from IFC

---

## Next Steps

1. ? Restore NuGet packages (without GeometryGym)
2. ? Build project
3. ? Test working endpoints
4. ? Add GeometryGym (Option 1 recommended)
5. ? Implement IFC endpoints

---

## Questions?

If you have issues:
- Check if `GeometryGymIFC.dll` exists in your desktop app
- Verify .NET Framework version compatibility (4.8)
- Try manual DLL reference first before NuGet
