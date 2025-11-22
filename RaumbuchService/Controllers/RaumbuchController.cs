using ClosedXML.Excel;
using RaumbuchService.Models;
using RaumbuchService.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Main controller for Raumbuch operations in Trimble Connect.
    /// Swiss German UI messages.
    /// </summary>
    [RoutePrefix("api/raumbuch")]
    public class RaumbuchController : ApiController
    {
        private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "Raumbuch");

        public RaumbuchController()
        {
            Directory.CreateDirectory(_tempFolder);
        }

        // --------------------------------------------------------------------
        //  1. IMPORT TEMPLATE – CREATE RAUMPROGRAMM
        // --------------------------------------------------------------------

        /// <summary>
        /// Imports a Raumbuch template (Excel) and creates Raumprogramm.xlsx.
        /// Swiss German: Importiert eine Raumbuch-Vorlage und erstellt das Raumprogramm.
        /// </summary>
        [HttpPost]
        [Route("import-template")]
        [Route("importtemplate")] // Alternative route without hyphen
        public async Task<IHttpActionResult> ImportTemplate([FromBody] ImportTemplateRequest request)
        {
            try
            {
                // Log start of request
                System.Diagnostics.Debug.WriteLine("ImportTemplate called");
                
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.TemplateFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    System.Diagnostics.Debug.WriteLine("Validation failed");
                    return BadRequest("Ungültige Anfrage. AccessToken, TemplateFileId und TargetFolderId sind erforderlich.");
                }

                System.Diagnostics.Debug.WriteLine($"Creating TrimbleConnectService with token length: {request.AccessToken.Length}");
                var tcService = new TrimbleConnectService(request.AccessToken);

                // Download template
                System.Diagnostics.Debug.WriteLine($"Downloading template file: {request.TemplateFileId}");
                string templatePath = await tcService.DownloadFileAsync(
                    request.TemplateFileId,
                    _tempFolder,
                    "Template.xlsx"
                );
                System.Diagnostics.Debug.WriteLine($"Downloaded to: {templatePath}");

                // Copy to Raumprogramm.xlsx (for now, just rename - later add business logic)
                string raumprogrammPath = Path.Combine(_tempFolder, "Raumprogramm.xlsx");
                System.Diagnostics.Debug.WriteLine($"Copying to: {raumprogrammPath}");
                File.Copy(templatePath, raumprogrammPath, overwrite: true);

                // Upload to Trimble Connect
                System.Diagnostics.Debug.WriteLine($"Uploading to folder: {request.TargetFolderId}");
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumprogrammPath);
                System.Diagnostics.Debug.WriteLine($"Uploaded with file ID: {fileId}");

                // Cleanup
                File.Delete(templatePath);
                File.Delete(raumprogrammPath);

                System.Diagnostics.Debug.WriteLine("ImportTemplate completed successfully");
                return Ok(new ImportTemplateResponse
                {
                    Success = true,
                    Message = "Raumprogramm wurde erfolgreich erstellt.",
                    RaumprogrammFileId = fileId,
                    RaumprogrammFileName = "Raumprogramm.xlsx"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in ImportTemplate: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return InternalServerError(new Exception($"Fehler beim Importieren der Vorlage: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  2. CREATE TODO
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates a TODO in Trimble Connect to notify users.
        /// Swiss German: Erstellt eine Aufgabe in Trimble Connect.
        /// </summary>
        [HttpPost]
        [Route("create-todo")]
        public async Task<IHttpActionResult> CreateTodo([FromBody] CreateTodoRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.ProjectId))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken und ProjectId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                string title = request.Title ?? "Raumprogramm wurde erstellt";
                string label = request.Label ?? "Raumbuch";

                string todoId = await tcService.CreateTodoAsync(
                    request.ProjectId,
                    title,
                    request.Assignees ?? new List<string>(),
                    label,
                    "NORMAL",
                    request.DueDate
                );

                return Ok(new CreateTodoResponse
                {
                    Success = true,
                    Message = "Aufgabe wurde erfolgreich erstellt.",
                    TodoId = todoId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Erstellen der Aufgabe: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  2. CREATE TODO
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates a BCF Topic in Trimble Connect to notify users.
        /// Swiss German: Erstellt ein BCF Topic in Trimble Connect.
        /// </summary>
        [HttpPost]
        [Route("create-bcf-topic")]
        public async Task<IHttpActionResult> CreateBcfTopic([FromBody] CreateBcfTopicRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.ProjectId) ||
                    string.IsNullOrWhiteSpace(request.AssignedTo))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, ProjectId und AssignedTo sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                string title = request.Title ?? "Raumbuch";
                string description = request.Description ?? "Raumprogramm wurde in diesem Verzeichnis erstellt.";

                // Build reference links if folder ID is provided
                List<string> referenceLinks = null;
                if (!string.IsNullOrWhiteSpace(request.FolderId))
                {
                    string folderUrl = $"https://web.connect.trimble.com/projects/{request.ProjectId}/data/folder/{request.FolderId}";
                    referenceLinks = new List<string> { folderUrl };
                }

                // Create BCF topic
                string topicGuid = await tcService.CreateBcfTopicAsync(
                    request.ProjectId,
                    title,
                    description,
                    request.AssignedTo,
                    referenceLinks
                );

                // Try to add document reference if folder ID is provided
                string documentReferenceGuid = null;
                if (!string.IsNullOrWhiteSpace(request.FolderId))
                {
                    try
                    {
                        string folderUrl = $"https://web.connect.trimble.com/projects/{request.ProjectId}/data/folder/{request.FolderId}";
                        documentReferenceGuid = await tcService.AddBcfDocumentReferenceAsync(
                            request.ProjectId,
                            topicGuid,
                            folderUrl,
                            "Raumprogramm Verzeichnis"
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not add document reference: {ex.Message}");
                    }
                }

                return Ok(new CreateBcfTopicResponse
                {
                    Success = true,
                    Message = "BCF Topic wurde erfolgreich erstellt.",
                    TopicGuid = topicGuid,
                    DocumentReferenceGuid = documentReferenceGuid
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Erstellen des BCF Topics: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  3. IMPORT IFC ? CREATE RAUMBUCH
        // --------------------------------------------------------------------

        /// <summary>
        /// Imports an IFC file and creates Raumbuch.xlsx with SOLL/IST analysis.
        /// Swiss German: Importiert eine IFC-Datei und erstellt das Raumbuch mit SOLL/IST-Analyse.
        /// </summary>
        [HttpPost]
        [Route("import-ifc")]
        public async Task<IHttpActionResult> ImportIfc([FromBody] ImportIfcRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========== ImportIfc START ==========");
                System.Diagnostics.Debug.WriteLine($"Request is null: {request == null}");
                
                if (request != null)
                {
                    System.Diagnostics.Debug.WriteLine($"AccessToken length: {request.AccessToken?.Length ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"IfcFileId: {request.IfcFileId}");
                    System.Diagnostics.Debug.WriteLine($"RaumprogrammFileId: {request.RaumprogrammFileId}");
                    System.Diagnostics.Debug.WriteLine($"TargetFolderId: {request.TargetFolderId}");
                }

                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumprogrammFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    System.Diagnostics.Debug.WriteLine("Validation failed - returning BadRequest");
                    return BadRequest("Ungültige Anfrage. AccessToken, IfcFileId, RaumprogrammFileId und TargetFolderId sind erforderlich.");
                }

                System.Diagnostics.Debug.WriteLine("Validation passed, creating services...");

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();
                var analyzer = new RaumbuchAnalyzer();

                System.Diagnostics.Debug.WriteLine("Downloading IFC file...");
                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );
                System.Diagnostics.Debug.WriteLine($"IFC downloaded to: {ifcPath}");

                System.Diagnostics.Debug.WriteLine("Downloading Raumprogramm file...");
                // Download Raumprogramm (SOLL)
                string raumprogrammPath = await tcService.DownloadFileAsync(
                    request.RaumprogrammFileId,
                    _tempFolder,
                    "Raumprogramm.xlsx"
                );
                System.Diagnostics.Debug.WriteLine($"Raumprogramm downloaded to: {raumprogrammPath}");

                System.Diagnostics.Debug.WriteLine("Reading spaces from IFC...");
                // Read spaces from IFC (IST)
                var roomData = ifcEditor.ReadSpaces(ifcPath);
                System.Diagnostics.Debug.WriteLine($"Found {roomData.Count} spaces in IFC");

                System.Diagnostics.Debug.WriteLine("Reading SOLL data from Excel...");
                // Read SOLL from Raumprogramm
                var sollData = ReadSollFromExcel(raumprogrammPath);
                System.Diagnostics.Debug.WriteLine($"Found {sollData.Count} SOLL categories");

                System.Diagnostics.Debug.WriteLine("Analyzing SOLL/IST...");
                // Analyze SOLL/IST
                var istData = roomData.Select(r => (r.RoomCategory, r.Area)).ToList();
                var analysis = analyzer.Analyze(sollData, istData);
                System.Diagnostics.Debug.WriteLine($"Analysis complete: {analysis.Count} categories");

                System.Diagnostics.Debug.WriteLine("Creating Raumbuch Excel...");
                // Create Raumbuch Excel
                string raumbuchPath = Path.Combine(_tempFolder, "Raumbuch.xlsx");
                CreateRaumbuchExcel(raumbuchPath, roomData, analysis);
                System.Diagnostics.Debug.WriteLine($"Raumbuch created at: {raumbuchPath}");

                System.Diagnostics.Debug.WriteLine("Uploading Raumbuch to Trimble Connect...");
                // Upload Raumbuch to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);
                System.Diagnostics.Debug.WriteLine($"Uploaded with file ID: {fileId}");

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumprogrammPath);
                File.Delete(raumbuchPath);

                System.Diagnostics.Debug.WriteLine("========== ImportIfc END (Success) ==========");

                // Convert analysis to Models namespace for response
                var analysisResponse = analysis.Select(a => new Models.RoomCategoryAnalysis
                {
                    RoomCategory = a.RoomCategory,
                    SollArea = a.SollArea,
                    IstArea = a.IstArea,
                    Percentage = a.Percentage
                }).ToList();

                return Ok(new ImportIfcResponse
                {
                    Success = true,
                    Message = "Raumbuch wurde erfolgreich erstellt.",
                    RaumbuchFileId = fileId,
                    RaumbuchFileName = "Raumbuch.xlsx",
                    Analysis = analysisResponse
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("========== ImportIfc EXCEPTION ==========");
                System.Diagnostics.Debug.WriteLine($"Error type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Error message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                
                System.Diagnostics.Debug.WriteLine("========== ImportIfc END (Error) ==========");
                
                return InternalServerError(new Exception($"Fehler beim Importieren der IFC-Datei: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  4. ANALYZE ROOMS ? MARK IFC
        // --------------------------------------------------------------------

        /// <summary>
        /// Analyzes rooms and writes Pset "Überprüfung der Raumkategorie" to IFC.
        /// Swiss German: Analysiert Räume und markiert diese in der IFC-Datei.
        /// </summary>
        [HttpPost]
        [Route("analyze-rooms")]
        public async Task<IHttpActionResult> AnalyzeRooms([FromBody] AnalyzeRoomsRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );

                // Download Raumbuch
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Read analysis from Raumbuch (placeholder)
                var analysis = ReadAnalysisFromRaumbuch(raumbuchPath);

                // Mark rooms in IFC
                string outputPath = Path.Combine(_tempFolder, "Model_Analyzed.ifc");
                
                // Convert Services.RoomCategoryAnalysis to Models.RoomCategoryAnalysis
                var analysisForMarking = analysis.Select(a => new Models.RoomCategoryAnalysis
                {
                    RoomCategory = a.RoomCategory,
                    SollArea = a.SollArea,
                    IstArea = a.IstArea,
                    Percentage = a.Percentage
                }).ToList();
                
                var result = ifcEditor.MarkRoomsOverLimit(ifcPath, outputPath, analysisForMarking);

                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, outputPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumbuchPath);
                File.Delete(outputPath);

                return Ok(new AnalyzeRoomsResponse
                {
                    Success = true,
                    Message = $"{result.RoomsMarked} Räume wurden markiert.",
                    UpdatedIfcFileId = fileId,
                    RoomsMarked = result.RoomsMarked,
                    MarkedRoomNames = result.MarkedRoomNames
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Analysieren der Räume: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  5. RESET IFC
        // --------------------------------------------------------------------

        /// <summary>
        /// Removes Pset "Überprüfung der Raumkategorie" from IFC.
        /// Swiss German: Entfernt die Analyse-Markierungen aus der IFC-Datei.
        /// </summary>
        [HttpPost]
        [Route("reset-ifc")]
        public async Task<IHttpActionResult> ResetIfc([FromBody] ResetIfcRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );

                // Reset verification Pset
                string outputPath = Path.Combine(_tempFolder, "Model_Reset.ifc");
                var result = ifcEditor.ResetVerificationPset(ifcPath, outputPath);

                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, outputPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(outputPath);

                return Ok(new ResetIfcResponse
                {
                    Success = true,
                    Message = $"{result.PsetsRemoved} Analyse-Markierungen wurden entfernt.",
                    UpdatedIfcFileId = fileId,
                    PsetsRemoved = result.PsetsRemoved
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Zurücksetzen der IFC-Datei: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  6. WRITE PSET RAUMBUCH (STEP 4)
        // --------------------------------------------------------------------

        /// <summary>
        /// Updates an existing Raumbuch.xlsx file with new IFC and Raumprogramm data.
        /// Only updates cells that have changed (preserves unchanged values).
        /// Recalculates SOLL/IST analysis like ImportIfc.
        /// Swiss German: Aktualisiert eine existierende Raumbuch-Datei mit neuen IFC- und Raumprogramm-Daten.
        /// </summary>
        [HttpPost]
        [Route("update-raumbuch")]
        public async Task<IHttpActionResult> UpdateRaumbuch([FromBody] UpdateRaumbuchRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========== UpdateRaumbuch START ==========");
                
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumprogrammFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();
                var analyzer = new RaumbuchAnalyzer();

                System.Diagnostics.Debug.WriteLine("Downloading existing Raumbuch...");
                // Download existing Raumbuch
                string existingRaumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch_Existing.xlsx"
                );

                System.Diagnostics.Debug.WriteLine("Downloading IFC file...");
                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );

                System.Diagnostics.Debug.WriteLine("Downloading Raumprogramm...");
                // Download Raumprogramm (SOLL)
                string raumprogrammPath = await tcService.DownloadFileAsync(
                    request.RaumprogrammFileId,
                    _tempFolder,
                    "Raumprogramm.xlsx"
                );

                System.Diagnostics.Debug.WriteLine("Reading existing Raumbuch data...");
                // Read existing Raumbuch data
                var existingData = ReadExistingRaumbuchData(existingRaumbuchPath);

                System.Diagnostics.Debug.WriteLine("Reading new IFC spaces...");
                // Read new data from IFC (IST)
                var newRoomData = ifcEditor.ReadSpaces(ifcPath);

                System.Diagnostics.Debug.WriteLine("Reading SOLL data...");
                // Read SOLL from Raumprogramm
                var sollData = ReadSollFromExcel(raumprogrammPath);

                System.Diagnostics.Debug.WriteLine("Analyzing SOLL/IST...");
                // Analyze SOLL/IST
                var istData = newRoomData.Select(r => (r.RoomCategory, r.Area)).ToList();
                var analysis = analyzer.Analyze(sollData, istData);

                System.Diagnostics.Debug.WriteLine("Updating Raumbuch Excel...");
                // Update Raumbuch Excel (merge with existing data)
                string updatedRaumbuchPath = Path.Combine(_tempFolder, "Raumbuch_Updated.xlsx");
                var updateResult = UpdateRaumbuchExcel(
                    existingRaumbuchPath,
                    updatedRaumbuchPath,
                    newRoomData,
                    analysis,
                    existingData
                );

                System.Diagnostics.Debug.WriteLine("Uploading updated Raumbuch...");
                // Upload to Trimble Connect (overwrites existing file - creates new version)
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, updatedRaumbuchPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumprogrammPath);
                File.Delete(existingRaumbuchPath);
                File.Delete(updatedRaumbuchPath);

                System.Diagnostics.Debug.WriteLine("========== UpdateRaumbuch END (Success) ==========");

                // Convert analysis to Models namespace for response
                var analysisResponse = analysis.Select(a => new Models.RoomCategoryAnalysis
                {
                    RoomCategory = a.RoomCategory,
                    SollArea = a.SollArea,
                    IstArea = a.IstArea,
                    Percentage = a.Percentage
                }).ToList();

                return Ok(new UpdateRaumbuchResponse
                {
                    Success = true,
                    Message = "Raumbuch wurde erfolgreich aktualisiert.",
                    RaumbuchFileId = fileId,
                    RaumbuchFileName = "Raumbuch.xlsx",
                    Analysis = analysisResponse,
                    RoomsUpdated = updateResult.Updated,
                    RoomsAdded = updateResult.Added,
                    RoomsUnchanged = updateResult.Unchanged
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("========== UpdateRaumbuch EXCEPTION ==========");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return InternalServerError(new Exception($"Fehler beim Aktualisieren der Raumbuch-Datei: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Writes Pset "Raumbuch" to IFC spaces based on Raumbuch Excel data.
        /// Swiss German: Schreibt Raumbuch Pset in IFC-Datei.
        /// </summary>
        [HttpPost]
        [Route("write-raumbuch-pset")]
        public async Task<IHttpActionResult> WriteRaumbuchPset([FromBody] WriteRaumbuchPsetRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Get original filename from Trimble Connect metadata
                string originalFileName = await GetFileNameAsync(tcService, request.IfcFileId);

                // Download IFC with original filename
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    originalFileName
                );

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Parse Raumbuch Excel to extract Differenz and calculate Gemäss Raumprogramm
                var raumbuchData = ParseRaumbuchForPset(raumbuchPath);

                if (raumbuchData.Count == 0)
                {
                    return BadRequest("Keine Raumbuch-Daten gefunden. Bitte Raumbuch.xlsx prüfen.");
                }

                // Write Pset to IFC
                string outputPath = ifcPath;
                var result = ifcEditor.WritePsetRaumbuch(ifcPath, outputPath, raumbuchData);

                // Upload to Trimble Connect (creates new version with same name)
                string uploadedFileId = await tcService.UploadFileAsync(request.TargetFolderId, outputPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumbuchPath);

                string message = $"Pset 'Raumbuch' erfolgreich geschrieben. {result.RoomsUpdated} Räume aktualisiert, {result.RoomsSkipped} übersprungen.";

                return Ok(new WriteRaumbuchPsetResponse
                {
                    Success = true,
                    Message = message,
                    UpdatedIfcFileId = uploadedFileId,
                    RoomsUpdated = result.RoomsUpdated,
                    RoomsSkipped = result.RoomsSkipped,
                    Warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Schreiben des Raumbuch Pset: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Updates Pset "Raumbuch" on IFC spaces.
        /// Swiss German: Aktualisiert Raumbuch Pset in IFC-Datei.
        /// </summary>
        [HttpPost]
        [Route("update-raumbuch-pset")]
        public async Task<IHttpActionResult> UpdateRaumbuchPset([FromBody] WriteRaumbuchPsetRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Get original filename from Trimble Connect metadata
                string originalFileName = await GetFileNameAsync(tcService, request.IfcFileId);

                // Download IFC with original filename
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    originalFileName
                );

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Parse Raumbuch Excel
                var raumbuchData = ParseRaumbuchForPset(raumbuchPath);

                if (raumbuchData.Count == 0)
                {
                    return BadRequest("Keine Raumbuch-Daten gefunden. Bitte Raumbuch.xlsx prüfen.");
                }

                // Update Pset in IFC
                string outputPath = ifcPath;
                var result = ifcEditor.UpdatePsetRaumbuch(ifcPath, outputPath, raumbuchData);

                // Upload to Trimble Connect (creates new version with same name)
                string uploadedFileId = await tcService.UploadFileAsync(request.TargetFolderId, outputPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumbuchPath);

                string message = $"Pset 'Raumbuch' erfolgreich aktualisiert. {result.RoomsUpdated} Räume aktualisiert, {result.RoomsSkipped} übersprungen.";

                return Ok(new WriteRaumbuchPsetResponse
                {
                    Success = true,
                    Message = message,
                    UpdatedIfcFileId = uploadedFileId,
                    RoomsUpdated = result.RoomsUpdated,
                    RoomsSkipped = result.RoomsSkipped,
                    Warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Aktualisieren des Raumbuch Pset: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Removes Pset "Raumbuch" from IFC spaces.
        /// Swiss German: Löscht Raumbuch Pset aus IFC-Datei.
        /// </summary>
        [HttpPost]
        [Route("delete-raumbuch-pset")]
        public async Task<IHttpActionResult> DeleteRaumbuchPset([FromBody] DeleteRaumbuchPsetRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Get original filename from Trimble Connect metadata
                string originalFileName = await GetFileNameAsync(tcService, request.IfcFileId);

                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );

                // Remove Pset from IFC (use separate output file - GeometricGym limitation)
                string outputPath = Path.Combine(_tempFolder, "Model_PsetRemoved.ifc");
                var result = ifcEditor.RemovePsetRaumbuch(ifcPath, outputPath);

                // Rename output file to match original filename before upload
                string finalPath = Path.Combine(_tempFolder, originalFileName);
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(outputPath, finalPath);

                // Upload to Trimble Connect with original filename (creates new version)
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, finalPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(finalPath);

                return Ok(new DeleteRaumbuchPsetResponse
                {
                    Success = true,
                    Message = $"{result.PsetsRemoved} Psets wurden entfernt.",
                    UpdatedIfcFileId = fileId,
                    PsetsRemoved = result.PsetsRemoved
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Entfernen des Raumbuch Pset: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  HELPER METHODS
        // --------------------------------------------------------------------

        /// <summary>
        /// Reads SOLL data from Raumprogramm Excel.
        /// </summary>
        private Dictionary<string, double> ReadSollFromExcel(string excelPath)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(excelPath))
            {
                var ws = wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    string category = ws.Cell(r, 1).GetString().Trim();
                    string sollStr = ws.Cell(r, 4).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(category)) continue;

                    if (double.TryParse(sollStr, out double soll))
                    {
                        if (!result.ContainsKey(category))
                            result[category] = soll;
                        else
                            result[category] += soll;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates Raumbuch Excel file.
        /// </summary>
        private void CreateRaumbuchExcel(string outputPath, List<Services.RoomData> roomData, List<Services.RoomCategoryAnalysis> analysis)
        {
            var analysisLookup = analysis.ToDictionary(a => a.RoomCategory, a => a, StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Raumbuch");

                // Header
                ws.Cell(1, 1).Value = "Raumtyp";
                ws.Cell(1, 2).Value = "Raum Name";
                ws.Cell(1, 3).Value = "Fläche IST (m²)";
                ws.Cell(1, 4).Value = "SIA d0165";
                ws.Cell(1, 5).Value = "FloorCovering";
                ws.Cell(1, 6).Value = "Category";
                ws.Cell(1, 7).Value = "SOLL Fläche (m²)";
                ws.Cell(1, 8).Value = "SOLL/IST (%)";
                ws.Cell(1, 9).Value = "Differenz (m²)";

                var headerRange = ws.Range(1, 1, 1, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Data rows
                int row = 2;
                foreach (var room in roomData.OrderBy(r => r.RoomCategory).ThenBy(r => r.Name))
                {
                    ws.Cell(row, 1).Value = room.RoomCategory;
                    ws.Cell(row, 2).Value = room.Name;
                    ws.Cell(row, 3).Value = room.Area;
                    ws.Cell(row, 4).Value = room.SiaCategory;
                    ws.Cell(row, 5).Value = room.FloorCovering;
                    ws.Cell(row, 6).Value = room.SpaceCategory;

                    if (analysisLookup.TryGetValue(room.RoomCategory, out var ana))
                    {
                        ws.Cell(row, 7).Value = ana.SollArea;
                        
                        if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                        {
                            ws.Cell(row, 8).Value = "-";
                        }
                        else
                        {
                            ws.Cell(row, 8).Value = ana.Percentage;
                        }
                        
                        double diff = ana.IstArea - ana.SollArea;
                        if (double.IsNaN(diff) || double.IsInfinity(diff))
                        {
                            ws.Cell(row, 9).Value = "-";
                        }
                        else
                        {
                            ws.Cell(row, 9).Value = diff;
                        }

                        if (ana.IsOverLimit)
                        {
                            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.LightPink;
                        }
                    }

                    row++;
                }

                // Summary
                row += 2;
                ws.Cell(row, 1).Value = "Zusammenfassung";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;

                ws.Cell(row, 1).Value = "Raumkategorie";
                ws.Cell(row, 2).Value = "SOLL Fläche (m²)";
                ws.Cell(row, 3).Value = "IST Fläche (m²)";
                ws.Cell(row, 4).Value = "Prozent (%)";
                ws.Cell(row, 5).Value = "Status";

                ws.Range(row, 1, row, 5).Style.Font.Bold = true;
                ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;

                row++;

                foreach (var ana in analysis.OrderBy(a => a.RoomCategory))
                {
                    ws.Cell(row, 1).Value = ana.RoomCategory;
                    ws.Cell(row, 2).Value = ana.SollArea;
                    ws.Cell(row, 3).Value = ana.IstArea;
                    
                    if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                    {
                        ws.Cell(row, 4).Value = "-";
                    }
                    else
                    {
                        ws.Cell(row, 4).Value = ana.Percentage;
                    }
                    
                    ws.Cell(row, 5).Value = ana.IsOverLimit ? "ÜBERSCHUSS" : "OK";

                    if (ana.IsOverLimit)
                    {
                        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightPink;
                    }

                    row++;
                }

                wb.SaveAs(outputPath);
            }
        }

        /// <summary>
        /// Reads analysis from Raumbuch summary section.
        /// </summary>
        private List<Services.RoomCategoryAnalysis> ReadAnalysisFromRaumbuch(string excelPath)
        {
            var result = new List<Services.RoomCategoryAnalysis>();

            using (var wb = new XLWorkbook(excelPath))
            {
                var ws = wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                int summaryStartRow = -1;
                for (int r = firstRow; r <= lastRow; r++)
                {
                    if (string.Equals(ws.Cell(r, 1).GetString().Trim(), "Zusammenfassung", StringComparison.OrdinalIgnoreCase))
                    {
                        summaryStartRow = r + 2;
                        break;
                    }
                }

                if (summaryStartRow == -1) return result;

                for (int r = summaryStartRow; r <= lastRow; r++)
                {
                    string category = ws.Cell(r, 1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(category)) break;

                    if (double.TryParse(ws.Cell(r, 2).GetString().Trim(), out double soll) &&
                        double.TryParse(ws.Cell(r, 3).GetString().Trim(), out double ist) &&
                        double.TryParse(ws.Cell(r, 4).GetString().Trim(), out double percent))
                    {
                        result.Add(new Services.RoomCategoryAnalysis
                        {
                            RoomCategory = category,
                            SollArea = soll,
                            IstArea = ist,
                            Percentage = percent
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Reads existing Raumbuch data.
        /// </summary>
        private Dictionary<string, Services.RoomData> ReadExistingRaumbuchData(string excelPath)
        {
            var result = new Dictionary<string, Services.RoomData>(StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(excelPath))
            {
                var ws = wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    string category = ws.Cell(r, 1).GetString().Trim();
                    string name = ws.Cell(r, 2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (name.Equals("Zusammenfassung", StringComparison.OrdinalIgnoreCase)) break;

                    double.TryParse(ws.Cell(r, 3).GetString().Trim(), out double area);

                    result[name] = new Services.RoomData
                    {
                        RoomCategory = category,
                        Name = name,
                        Area = area,
                        SiaCategory = ws.Cell(r, 4).GetString().Trim(),
                        FloorCovering = ws.Cell(r, 5).GetString().Trim(),
                        SpaceCategory = ws.Cell(r, 6).GetString().Trim()
                    };
                }
            }

            return result;
        }

        /// <summary>
        /// Updates Raumbuch Excel with new data.
        /// </summary>
        private UpdateRaumbuchStats UpdateRaumbuchExcel(
            string existingPath,
            string outputPath,
            List<Services.RoomData> newRoomData,
            List<Services.RoomCategoryAnalysis> analysis,
            Dictionary<string, Services.RoomData> existingData)
        {
            var stats = new UpdateRaumbuchStats();
            var analysisLookup = analysis.ToDictionary(a => a.RoomCategory, a => a, StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(existingPath))
            {
                var ws = wb.Worksheet(1);

                // Clear old data (keep header)
                var range = ws.RangeUsed();
                if (range != null)
                {
                    int firstDataRow = range.FirstRow().RowNumber() + 1;
                    int lastDataRow = range.LastRow().RowNumber();

                    for (int r = firstDataRow; r <= lastDataRow; r++)
                    {
                        if (string.Equals(ws.Cell(r, 1).GetString().Trim(), "Zusammenfassung", StringComparison.OrdinalIgnoreCase))
                        {
                            lastDataRow = r - 1;
                            break;
                        }
                    }

                    if (lastDataRow >= firstDataRow)
                    {
                        ws.Rows(firstDataRow, lastDataRow).Delete();
                    }
                }

                // Write new data
                int row = 2;
                foreach (var room in newRoomData.OrderBy(r => r.RoomCategory).ThenBy(r => r.Name))
                {
                    bool existed = existingData.TryGetValue(room.Name, out var oldData);

                    if (existed)
                    {
                        if (Math.Abs(room.Area - oldData.Area) > 0.01 ||
                            !string.Equals(room.RoomCategory, oldData.RoomCategory, StringComparison.OrdinalIgnoreCase))
                        {
                            stats.Updated++;
                        }
                        else
                        {
                            stats.Unchanged++;
                        }
                    }
                    else
                    {
                        stats.Added++;
                    }

                    ws.Cell(row, 1).Value = room.RoomCategory;
                    ws.Cell(row, 2).Value = room.Name;
                    ws.Cell(row, 3).Value = room.Area;
                    ws.Cell(row, 4).Value = room.SiaCategory;
                    ws.Cell(row, 5).Value = room.FloorCovering;
                    ws.Cell(row, 6).Value = room.SpaceCategory;

                    if (analysisLookup.TryGetValue(room.RoomCategory, out var ana))
                    {
                        ws.Cell(row, 7).Value = ana.SollArea;
                        
                        if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                        {
                            ws.Cell(row, 8).Value = "-";
                        }
                        else
                        {
                            ws.Cell(row, 8).Value = ana.Percentage;
                        }
                        
                        double diff = ana.IstArea - ana.SollArea;
                        if (double.IsNaN(diff) || double.IsInfinity(diff))
                        {
                            ws.Cell(row, 9).Value = "-";
                        }
                        else
                        {
                            ws.Cell(row, 9).Value = diff;
                        }

                        if (ana.IsOverLimit)
                        {
                            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.LightPink;
                        }
                    }

                    row++;
                }

                // Update summary
                row += 2;
                ws.Cell(row, 1).Value = "Zusammenfassung";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;

                ws.Cell(row, 1).Value = "Raumkategorie";
                ws.Cell(row, 2).Value = "SOLL Fläche (m²)";
                ws.Cell(row, 3).Value = "IST Fläche (m²)";
                ws.Cell(row, 4).Value = "Prozent (%)";
                ws.Cell(row, 5).Value = "Status";

                ws.Range(row, 1, row, 5).Style.Font.Bold = true;
                ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;

                row++;

                foreach (var ana in analysis.OrderBy(a => a.RoomCategory))
                {
                    ws.Cell(row, 1).Value = ana.RoomCategory;
                    ws.Cell(row, 2).Value = ana.SollArea;
                    ws.Cell(row, 3).Value = ana.IstArea;
                    
                    if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                    {
                        ws.Cell(row, 4).Value = "-";
                    }
                    else
                    {
                        ws.Cell(row, 4).Value = ana.Percentage;
                    }
                    
                    ws.Cell(row, 5).Value = ana.IsOverLimit ? "ÜBERSCHUSS" : "OK";

                    if (ana.IsOverLimit)
                    {
                        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightPink;
                    }

                    row++;
                }

                wb.SaveAs(outputPath);
            }

            return stats;
        }

        /// <summary>
        /// Gets filename from Trimble Connect.
        /// </summary>
        private async Task<string> GetFileNameAsync(TrimbleConnectService tcService, string fileId)
        {
            try
            {
                return await tcService.GetFileNameAsync(fileId);
            }
            catch
            {
                return "Model.ifc";
            }
        }

        /// <summary>
        /// Parses Raumbuch for Pset data.
        /// </summary>
        private Dictionary<string, Services.RaumbuchPsetData> ParseRaumbuchForPset(string excelPath)
        {
            var result = new Dictionary<string, Services.RaumbuchPsetData>(StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(excelPath))
            {
                var ws = wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    string raumName = ws.Cell(r, 2).GetString().Trim();
                    string differenzStr = ws.Cell(r, 9).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(raumName)) continue;
                    if (raumName.Equals("Zusammenfassung", StringComparison.OrdinalIgnoreCase)) break;

                    if (!double.TryParse(differenzStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double differenz))
                    {
                        continue;
                    }

                    result[raumName] = new Services.RaumbuchPsetData
                    {
                        Differenz = differenz,
                        GemaessRaumprogramm = (differenz <= 0) ? "Ja" : "Nein"
                    };
                }
            }

            return result;
        }

        // --------------------------------------------------------------------
        //  7. INVENTORY OPERATIONS (STEP 5)
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates room sheets in Raumbuch.xlsx - one sheet per room for inventory.
        /// Each sheet has columns: Objektname, Beschreibung, GUID
        /// Swiss German: Erstellt Raumlisten (ein Blatt pro Raum) in Raumbuch.xlsx
        /// </summary>
        [HttpPost]
        [Route("create-room-sheets")]
        public async Task<IHttpActionResult> CreateRoomSheets([FromBody] CreateRoomSheetsRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========== CreateRoomSheets START ==========");

                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                System.Diagnostics.Debug.WriteLine("Downloading Raumbuch...");
                // Download existing Raumbuch
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                System.Diagnostics.Debug.WriteLine("Creating room sheets...");
                // Read room names from first sheet
                var roomNames = new List<string>();
                int sheetsCreated = 0;  // Declare outside using block
                
                using (var wb = new XLWorkbook(raumbuchPath))
                {
                    var mainSheet = wb.Worksheet(1);
                    var range = mainSheet.RangeUsed();
                    
                    if (range != null)
                    {
                        int firstRow = range.FirstRow().RowNumber();
                        int lastRow = range.LastRow().RowNumber();

                        // Read room names from column B (Raum Name)
                        for (int r = firstRow + 1; r <= lastRow; r++)
                        {
                            string roomName = mainSheet.Cell(r, 2).GetString().Trim();
                            
                            if (string.IsNullOrWhiteSpace(roomName))
                                continue;
                                
                            if (roomName.Equals("Zusammenfassung", StringComparison.OrdinalIgnoreCase))
                                break;

                            roomNames.Add(roomName);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Found {roomNames.Count} rooms");

                    // Create sheet for each room
                    foreach (var roomName in roomNames)
                    {
                        // Check if sheet already exists
                        if (wb.Worksheets.Any(ws => ws.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase)))
                        {
                            System.Diagnostics.Debug.WriteLine($"  Sheet '{roomName}' already exists - skipping");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"  Creating sheet: {roomName}");

                        // Create new worksheet
                        var roomSheet = wb.Worksheets.Add(roomName);

                        // Row 1: Link back to main sheet in A1
                        roomSheet.Cell(1, 1).SetFormulaA1($"=HYPERLINK(\"#Raumbuch!A1\", \"Zum Raumbuch\")");
                        roomSheet.Cell(1, 1).Style.Font.FontColor = XLColor.Blue;
                        roomSheet.Cell(1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

                        // Row 2: Add header row (columns A-D)
                        roomSheet.Cell(2, 1).Value = "IFC Fil";  // Header for column A
                        roomSheet.Cell(2, 2).Value = "Objektname";
                        roomSheet.Cell(2, 3).Value = "Beschreibung";
                        roomSheet.Cell(2, 4).Value = "GUID";

                        var headerRange = roomSheet.Range(2, 1, 2, 4);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        // Set column A width to 30
                        roomSheet.Column(1).Width = 30;
                        
                        // Auto-fit other columns
                        roomSheet.Columns(2, 4).AdjustToContents();
                        
                        sheetsCreated++;
                    }

                    System.Diagnostics.Debug.WriteLine($"Sheets created: {sheetsCreated}");

                    // Update main sheet - convert room names in column B to hyperlinks with apostrophes
                    int row = 2;
                    foreach (var roomName in roomNames)
                    {
                        var cell = mainSheet.Cell(row, 2);
                        // Add apostrophes around sheet name for Excel compatibility
                        cell.SetFormulaA1($"=HYPERLINK(\"#'{roomName}'!A1\", \"{roomName}\")");
                        cell.Style.Font.FontColor = XLColor.Blue;
                        cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        row++;
                    }

                    System.Diagnostics.Debug.WriteLine("Saving workbook...");
                    wb.SaveAs(raumbuchPath);
                }

                System.Diagnostics.Debug.WriteLine("Uploading updated Raumbuch...");
                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                System.Diagnostics.Debug.WriteLine("========== CreateRoomSheets END (Success) ==========");

                return Ok(new CreateRoomSheetsResponse
                {
                    Success = true,
                    Message = $"Raumlisten erfolgreich erstellt. {sheetsCreated} Blätter hinzugefügt.",
                    RaumbuchFileId = fileId,
                    RoomSheetsCreated = sheetsCreated,
                    RoomNames = roomNames
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Erstellen der Raumlisten: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Fills inventory data from IFC files into Raumbuch room sheets.
        /// Swiss German: Füllt Inventardaten aus IFC-Dateien in Raumlisten ein.
        /// </summary>
        [HttpPost]
        [Route("fill-inventory")]
        public async Task<IHttpActionResult> FillInventory([FromBody] FillInventoryRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========== FillInventory START ==========");

                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    request.IfcFileIds == null || request.IfcFileIds.Count == 0 ||
                    string.IsNullOrWhiteSpace(request.PsetPartialName) ||
                    string.IsNullOrWhiteSpace(request.RoomPropertyName) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();
                var warnings = new List<string>();

                System.Diagnostics.Debug.WriteLine($"Downloading Raumbuch...");
                // Download Raumbuch
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Collect inventory from all IFC files
                var allInventory = new Dictionary<string, List<Services.InventoryItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (var ifcFileId in request.IfcFileIds)
                {
                    System.Diagnostics.Debug.WriteLine($"Processing IFC file: {ifcFileId}");

                    // Get original filename from Trimble Connect
                    string originalFileName = "Model.ifc";  // Default fallback
                    try
                    {
                        originalFileName = await tcService.GetFileNameAsync(ifcFileId);
                        System.Diagnostics.Debug.WriteLine($"  Original filename from TC: {originalFileName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Could not get filename from TC: {ex.Message}");
                    }

                    // Download IFC with temp name
                    string ifcPath = await tcService.DownloadFileAsync(
                        ifcFileId,
                        _tempFolder,
                        $"Model_{Guid.NewGuid()}.ifc"
                    );

                    // Read inventory (will use temp filename internally, but we'll override it)
                    var inventory = ifcEditor.ReadInventoryByRoom(
                        ifcPath,
                        request.PsetPartialName,
                        request.RoomPropertyName
                    );

                    System.Diagnostics.Debug.WriteLine($"  Found inventory for {inventory.Count} rooms");

                    // Merge inventory into allInventory and set correct filename
                    foreach (var kvp in inventory)
                    {
                        if (!allInventory.ContainsKey(kvp.Key))
                        {
                            allInventory[kvp.Key] = new List<Services.InventoryItem>();
                        }

                        // Override filename with original name from Trimble Connect
                        foreach (var item in kvp.Value)
                        {
                            item.IfcFileName = originalFileName;
                        }

                        allInventory[kvp.Key].AddRange(kvp.Value);
                    }

                    // Cleanup
                    File.Delete(ifcPath);
                }

                System.Diagnostics.Debug.WriteLine($"Total inventory collected for {allInventory.Count} rooms");

                // Update Raumbuch with inventory
                int roomsUpdated = 0;
                int totalItems = 0;

                using (var wb = new XLWorkbook(raumbuchPath))
                {
                    foreach (var kvp in allInventory)
                    {
                        string roomNumber = kvp.Key;
                        var items = kvp.Value;

                        System.Diagnostics.Debug.WriteLine($"  Updating room {roomNumber} with {items.Count} items");

                        // Find worksheet for this room
                        var roomSheet = wb.Worksheets.FirstOrDefault(ws => 
                            ws.Name.Equals(roomNumber, StringComparison.OrdinalIgnoreCase));

                        if (roomSheet == null)
                        {
                            warnings.Add($"Blatt für Raum '{roomNumber}' nicht gefunden - übersprungen");
                            System.Diagnostics.Debug.WriteLine($"  WARNING: Sheet for room '{roomNumber}' not found!");
                            continue;
                        }

                        // Write inventory items starting from row 3 (row 1 has link, row 2 has headers)
                        int row = 3;
                        foreach (var item in items)
                        {
                            roomSheet.Cell(row, 1).Value = item.IfcFileName ?? "";  // Column A: IFC filename (original)
                            roomSheet.Cell(row, 2).Value = item.Name;
                            roomSheet.Cell(row, 3).Value = item.Description;
                            roomSheet.Cell(row, 4).Value = item.GlobalId;
                            row++;
                            totalItems++;
                        }

                        roomsUpdated++;
                        System.Diagnostics.Debug.WriteLine($"  Successfully updated room '{roomNumber}' with {items.Count} items");

                        // Auto-fit columns (except A which has fixed width)
                        roomSheet.Columns(2, 4).AdjustToContents();
                    }

                    System.Diagnostics.Debug.WriteLine($"Saving workbook with {roomsUpdated} rooms updated and {totalItems} total items...");
                    wb.SaveAs(raumbuchPath);
                }

                System.Diagnostics.Debug.WriteLine("Uploading updated Raumbuch...");
                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                System.Diagnostics.Debug.WriteLine($"========== FillInventory END (Success) - {roomsUpdated} rooms, {totalItems} items ==========");

                string message = $"Inventar erfolgreich hinzugefügt. {roomsUpdated} Räume aktualisiert, {totalItems} Objekte hinzugefügt.";
                if (warnings.Count > 0)
                {
                    message += $" {warnings.Count} Warnungen.";
                }

                return Ok(new FillInventoryResponse
                {
                    Success = true,
                    Message = message,
                    RaumbuchFileId = fileId,
                    RoomsUpdated = roomsUpdated,
                    TotalItems = totalItems,
                    Warnings = warnings
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("========== FillInventory EXCEPTION ==========");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return InternalServerError(new Exception($"Fehler beim Füllen des Inventars: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  HELPER METHODS
        // --------------------------------------------------------------------
    }

    // --------------------------------------------------------------------
    //  HELPER CLASSES
    // --------------------------------------------------------------------

    public class UpdateRaumbuchStats
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
    }
}
