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

                // Copy only the first sheet from template and name it "Raumprogramm"
                string raumprogrammPath = Path.Combine(_tempFolder, "Raumprogramm.xlsx");
                System.Diagnostics.Debug.WriteLine($"Creating Raumprogramm.xlsx with first sheet only");
                CopyFirstSheetAsRaumprogramm(templatePath, raumprogrammPath);

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
        //  CREATE ROOM SHEETS
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates individual sheets for each room in Raumbuch Excel.
        /// Swiss German: Erstellt einzelne Arbeitsblätter für jeden Raum.
        /// </summary>
        [HttpPost]
        [Route("create-room-sheets")]
        public async Task<IHttpActionResult> CreateRoomSheets([FromBody] CreateRoomSheetsRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, RaumbuchFileId und TargetFolderId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Add room sheets to the Excel file
                var roomNames = CreateRoomSheetsInExcel(raumbuchPath);

                // Upload updated Raumbuch to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                return Ok(new CreateRoomSheetsResponse
                {
                    Success = true,
                    Message = $"{roomNames.Count} Raumblätter wurden erfolgreich erstellt.",
                    RaumbuchFileId = fileId,
                    RoomSheetsCreated = roomNames.Count,
                    RoomNames = roomNames
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Erstellen der Raumblätter: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  FILL INVENTORY
        // --------------------------------------------------------------------

        /// <summary>
        /// Fills room sheets with inventory from IFC files.
        /// Swiss German: Füllt Raumblätter mit Inventar aus IFC-Dateien.
        /// </summary>
        [HttpPost]
        [Route("fill-inventory")]
        public async Task<IHttpActionResult> FillInventory([FromBody] FillInventoryRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId) ||
                    request.IfcFileIds == null || request.IfcFileIds.Count == 0 ||
                    string.IsNullOrWhiteSpace(request.PsetPartialName) ||
                    string.IsNullOrWhiteSpace(request.RoomPropertyName))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Collect all inventory from all IFC files
                var allInventoryByRoom = new Dictionary<string, List<Services.InventoryItem>>(StringComparer.OrdinalIgnoreCase);
                var warnings = new List<string>();

                foreach (var ifcFileId in request.IfcFileIds)
                {
                    try
                    {
                        // Get original filename from Trimble Connect
                        string originalFileName = await GetFileNameAsync(tcService, ifcFileId);
                        
                        // Download IFC file with original filename
                        string ifcPath = await tcService.DownloadFileAsync(
                            ifcFileId,
                            _tempFolder,
                            originalFileName
                        );

                        // Read inventory by room
                        var inventoryByRoom = ifcEditor.ReadInventoryByRoom(
                            ifcPath,
                            request.PsetPartialName,
                            request.RoomPropertyName
                        );

                        // Merge into allInventoryByRoom
                        foreach (var kvp in inventoryByRoom)
                        {
                            if (!allInventoryByRoom.ContainsKey(kvp.Key))
                            {
                                allInventoryByRoom[kvp.Key] = new List<Services.InventoryItem>();
                            }
                            allInventoryByRoom[kvp.Key].AddRange(kvp.Value);
                        }

                        // Cleanup IFC file
                        File.Delete(ifcPath);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Fehler beim Verarbeiten der IFC-Datei {ifcFileId}: {ex.Message}");
                    }
                }

                // Fill room sheets with inventory
                int roomsUpdated = 0;
                int totalItems = 0;

                using (var wb = new XLWorkbook(raumbuchPath))
                {
                    // Get Raumbuch sheet to find room names
                    var raumbuchSheet = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch");
                    if (raumbuchSheet == null)
                    {
                        throw new Exception("Raumbuch sheet not found");
                    }

                    var range = raumbuchSheet.RangeUsed();
                    if (range == null)
                    {
                        throw new Exception("Raumbuch sheet is empty");
                    }

                    // Build a map of room names to room numbers
                    var roomNameToNumber = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    for (int r = firstRow + 1; r <= lastRow; r++) // Skip header
                    {
                        string roomName = raumbuchSheet.Cell(r, 2).GetString().Trim(); // Column B: Raum Name
                        if (!string.IsNullOrWhiteSpace(roomName))
                        {
                            // Try to extract room number from room name or use it as-is
                            // For now, we'll try to match inventory room numbers with room names directly
                            roomNameToNumber[roomName] = roomName;
                        }
                    }

                    // Fill each room sheet
                    foreach (var kvp in allInventoryByRoom)
                    {
                        string roomNumber = kvp.Key;
                        var items = kvp.Value;

                        // Find the matching room name
                        string roomName = null;
                        foreach (var entry in roomNameToNumber)
                        {
                            // Try exact match first
                            if (entry.Key.Equals(roomNumber, StringComparison.OrdinalIgnoreCase) ||
                                entry.Key.Contains(roomNumber))
                            {
                                roomName = entry.Key;
                                break;
                            }
                        }

                        if (roomName == null)
                        {
                            warnings.Add($"Raum '{roomNumber}' nicht in Raumbuch gefunden");
                            continue;
                        }

                        // Get the room sheet
                        string sheetName = SanitizeSheetName(roomName);
                        var roomSheet = wb.Worksheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

                        if (roomSheet == null)
                        {
                            warnings.Add($"Arbeitsblatt für Raum '{roomName}' nicht gefunden");
                            continue;
                        }

                        // Fill inventory starting from row 2 (row 1 has headers)
                        int row = 2;
                        foreach (var item in items)
                        {
                            roomSheet.Cell(row, 1).Value = item.IfcFileName;  // A: File name (no header)
                            roomSheet.Cell(row, 2).Value = item.Name;         // B: Objektname
                            roomSheet.Cell(row, 3).Value = item.Description;  // C: Beschreibung
                            roomSheet.Cell(row, 4).Value = item.GlobalId;     // D: GUID
                            row++;
                            totalItems++;
                        }

                        // Auto-fit columns
                        roomSheet.Columns().AdjustToContents();
                        roomsUpdated++;
                    }

                    wb.Save();
                }

                // Upload updated Raumbuch
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                return Ok(new FillInventoryResponse
                {
                    Success = true,
                    Message = $"Inventar erfolgreich hinzugefügt.",
                    RaumbuchFileId = fileId,
                    RoomsUpdated = roomsUpdated,
                    TotalItems = totalItems,
                    Warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Füllen des Inventars: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  DELETE ROOM LISTS
        // --------------------------------------------------------------------

        /// <summary>
        /// Deletes all room sheets (sheets 3+) from Raumbuch Excel.
        /// Also removes hyperlinks in column B of Raumbuch sheet while keeping the text.
        /// Swiss German: Löscht alle Raumlisten aus Raumbuch Excel.
        /// </summary>
        [HttpPost]
        [Route("delete-room-lists")]
        public async Task<IHttpActionResult> DeleteRoomLists([FromBody] DeleteRoomListsRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, RaumbuchFileId und TargetFolderId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                int sheetsDeleted = 0;
                int hyperlinksRemoved = 0;

                using (var wb = new XLWorkbook(raumbuchPath))
                {
                    // Delete all sheets from index 3 onwards
                    var sheetsToDelete = new List<IXLWorksheet>();
                    for (int i = 3; i <= wb.Worksheets.Count; i++)
                    {
                        sheetsToDelete.Add(wb.Worksheet(i));
                    }

                    foreach (var sheet in sheetsToDelete)
                    {
                        sheet.Delete();
                        sheetsDeleted++;
                    }

                    // Remove hyperlinks in column B of Raumbuch sheet (keep text)
                    var raumbuchSheet = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch");
                    if (raumbuchSheet != null)
                    {
                        var range = raumbuchSheet.RangeUsed();
                        if (range != null)
                        {
                            int firstRow = range.FirstRow().RowNumber();
                            int lastRow = range.LastRow().RowNumber();

                            for (int r = firstRow; r <= lastRow; r++)
                            {
                                var cell = raumbuchSheet.Cell(r, 2); // Column B
                                if (cell.HasHyperlink)
                                {
                                    string text = cell.GetString(); // Keep the text
                                    cell.Clear(XLClearOptions.Hyperlinks);
                                    cell.Value = text; // Restore the text
                                    cell.Style.Font.FontColor = XLColor.Black; // Remove blue color
                                    cell.Style.Font.Underline = XLFontUnderlineValues.None; // Remove underline
                                    hyperlinksRemoved++;
                                }
                            }
                        }
                    }

                    wb.Save();
                }

                // Upload updated Raumbuch
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                return Ok(new DeleteRoomListsResponse
                {
                    Success = true,
                    Message = $"{sheetsDeleted} Raumblätter gelöscht, {hyperlinksRemoved} Hyperlinks entfernt.",
                    RaumbuchFileId = fileId,
                    SheetsDeleted = sheetsDeleted,
                    HyperlinksRemoved = hyperlinksRemoved
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Löschen der Raumlisten: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  UPDATE INVENTORY
        // --------------------------------------------------------------------

        /// <summary>
        /// Updates inventory in room sheets from IFC files.
        /// Deletes existing items with same IFC file name, then adds new inventory.
        /// Swiss German: Aktualisiert Inventar in Raumblättern aus IFC-Dateien.
        /// </summary>
        [HttpPost]
        [Route("update-inventory")]
        public async Task<IHttpActionResult> UpdateInventory([FromBody] UpdateInventoryRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.RaumbuchFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId) ||
                    request.IfcFileIds == null || request.IfcFileIds.Count == 0 ||
                    string.IsNullOrWhiteSpace(request.PsetPartialName) ||
                    string.IsNullOrWhiteSpace(request.RoomPropertyName))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();

                // Download Raumbuch Excel
                string raumbuchPath = await tcService.DownloadFileAsync(
                    request.RaumbuchFileId,
                    _tempFolder,
                    "Raumbuch.xlsx"
                );

                // Collect file names from IFC files being processed
                var ifcFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var ifcFileId in request.IfcFileIds)
                {
                    try
                    {
                        string fileName = await GetFileNameAsync(tcService, ifcFileId);
                        ifcFileNames.Add(fileName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not get filename for {ifcFileId}: {ex.Message}");
                    }
                }

                // Collect all inventory from all IFC files
                var allInventoryByRoom = new Dictionary<string, List<Services.InventoryItem>>(StringComparer.OrdinalIgnoreCase);
                var warnings = new List<string>();

                foreach (var ifcFileId in request.IfcFileIds)
                {
                    try
                    {
                        // Get original filename from Trimble Connect
                        string originalFileName = await GetFileNameAsync(tcService, ifcFileId);
                        
                        // Download IFC file with original filename
                        string ifcPath = await tcService.DownloadFileAsync(
                            ifcFileId,
                            _tempFolder,
                            originalFileName
                        );

                        // Read inventory by room
                        var inventoryByRoom = ifcEditor.ReadInventoryByRoom(
                            ifcPath,
                            request.PsetPartialName,
                            request.RoomPropertyName
                        );

                        // Merge into allInventoryByRoom
                        foreach (var kvp in inventoryByRoom)
                        {
                            if (!allInventoryByRoom.ContainsKey(kvp.Key))
                            {
                                allInventoryByRoom[kvp.Key] = new List<Services.InventoryItem>();
                            }
                            allInventoryByRoom[kvp.Key].AddRange(kvp.Value);
                        }

                        // Cleanup IFC file
                        File.Delete(ifcPath);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Fehler beim Verarbeiten der IFC-Datei {ifcFileId}: {ex.Message}");
                    }
                }

                // Update room sheets with inventory
                int roomsUpdated = 0;
                int itemsDeleted = 0;
                int itemsAdded = 0;

                using (var wb = new XLWorkbook(raumbuchPath))
                {
                    // Get Raumbuch sheet to find room names
                    var raumbuchSheet = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch");
                    if (raumbuchSheet == null)
                    {
                        throw new Exception("Raumbuch sheet not found");
                    }

                    var range = raumbuchSheet.RangeUsed();
                    if (range == null)
                    {
                        throw new Exception("Raumbuch sheet is empty");
                    }

                    // Build a map of room names to room numbers
                    var roomNameToNumber = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    for (int r = firstRow + 1; r <= lastRow; r++) // Skip header
                    {
                        string roomName = raumbuchSheet.Cell(r, 2).GetString().Trim(); // Column B: Raum Name
                        if (!string.IsNullOrWhiteSpace(roomName))
                        {
                            roomNameToNumber[roomName] = roomName;
                        }
                    }

                    // Update each room sheet
                    foreach (var kvp in allInventoryByRoom)
                    {
                        string roomNumber = kvp.Key;
                        var items = kvp.Value;

                        // Find the matching room name
                        string roomName = null;
                        foreach (var entry in roomNameToNumber)
                        {
                            if (entry.Key.Equals(roomNumber, StringComparison.OrdinalIgnoreCase) ||
                                entry.Key.Contains(roomNumber))
                            {
                                roomName = entry.Key;
                                break;
                            }
                        }

                        if (roomName == null)
                        {
                            warnings.Add($"Raum '{roomNumber}' nicht in Raumbuch gefunden");
                            continue;
                        }

                        // Get the room sheet
                        string sheetName = SanitizeSheetName(roomName);
                        var roomSheet = wb.Worksheets.FirstOrDefault(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

                        if (roomSheet == null)
                        {
                            warnings.Add($"Arbeitsblatt für Raum '{roomName}' nicht gefunden");
                            continue;
                        }

                        // Delete rows with matching IFC file names
                        var sheetRange = roomSheet.RangeUsed();
                        if (sheetRange != null)
                        {
                            int sheetFirstRow = sheetRange.FirstRow().RowNumber();
                            int sheetLastRow = sheetRange.LastRow().RowNumber();

                            // Iterate from bottom to top to avoid index issues when deleting
                            for (int r = sheetLastRow; r >= sheetFirstRow + 1; r--) // Skip header row
                            {
                                string ifcFileName = roomSheet.Cell(r, 1).GetString().Trim(); // Column A: File name
                                if (ifcFileNames.Contains(ifcFileName))
                                {
                                    roomSheet.Row(r).Delete();
                                    itemsDeleted++;
                                }
                            }
                        }

                        // Find first empty row
                        int firstEmptyRow = 2; // Start from row 2 (after header)
                        var currentRange = roomSheet.RangeUsed();
                        if (currentRange != null)
                        {
                            firstEmptyRow = currentRange.LastRow().RowNumber() + 1;
                        }

                        // Add new inventory at first empty row
                        int row = firstEmptyRow;
                        foreach (var item in items)
                        {
                            roomSheet.Cell(row, 1).Value = item.IfcFileName;  // A: File name (no header)
                            roomSheet.Cell(row, 2).Value = item.Name;         // B: Objektname
                            roomSheet.Cell(row, 3).Value = item.Description;  // C: Beschreibung
                            roomSheet.Cell(row, 4).Value = item.GlobalId;     // D: GUID
                            row++;
                            itemsAdded++;
                        }

                        // Auto-fit columns
                        roomSheet.Columns().AdjustToContents();
                        roomsUpdated++;
                    }

                    wb.Save();
                }

                // Upload updated Raumbuch
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(raumbuchPath);

                return Ok(new UpdateInventoryResponse
                {
                    Success = true,
                    Message = $"Inventar erfolgreich aktualisiert.",
                    RaumbuchFileId = fileId,
                    RoomsUpdated = roomsUpdated,
                    ItemsDeleted = itemsDeleted,
                    ItemsAdded = itemsAdded,
                    Warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Aktualisieren des Inventars: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  HELPER METHODS
        // --------------------------------------------------------------------

        /// <summary>
        /// Copies only the first sheet from template Excel and names it "Raumprogramm".
        /// </summary>
        private void CopyFirstSheetAsRaumprogramm(string templatePath, string outputPath)
        {
            using (var templateWb = new XLWorkbook(templatePath))
            {
                var firstSheet = templateWb.Worksheet(1);
                
                using (var newWb = new XLWorkbook())
                {
                    // Copy the first sheet
                    firstSheet.CopyTo(newWb, "Raumprogramm");
                    
                    newWb.SaveAs(outputPath);
                }
            }
        }

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
                // ===== SHEET 1: RAUMBUCH (Room Data) =====
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

                // Auto-fit columns
                ws.Columns().AdjustToContents();

                // ===== SHEET 2: ZUSAMMENFASSUNG (Summary) =====
                var summaryWs = wb.Worksheets.Add("Zusammenfassung");

                // Header
                summaryWs.Cell(1, 1).Value = "Raumkate";
                summaryWs.Cell(1, 2).Value = "SOLL Fläche (m²)";
                summaryWs.Cell(1, 3).Value = "IST Fläche";
                summaryWs.Cell(1, 4).Value = "Prozent (%)";
                summaryWs.Cell(1, 5).Value = "Status";

                var summaryHeaderRange = summaryWs.Range(1, 1, 1, 5);
                summaryHeaderRange.Style.Font.Bold = true;
                summaryHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Summary data
                int summaryRow = 2;
                foreach (var ana in analysis.OrderBy(a => a.RoomCategory))
                {
                    summaryWs.Cell(summaryRow, 1).Value = ana.RoomCategory;
                    summaryWs.Cell(summaryRow, 2).Value = ana.SollArea;
                    summaryWs.Cell(summaryRow, 3).Value = ana.IstArea;
                    
                    if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                    {
                        summaryWs.Cell(summaryRow, 4).Value = "-";
                    }
                    else
                    {
                        summaryWs.Cell(summaryRow, 4).Value = ana.Percentage;
                    }
                    
                    summaryWs.Cell(summaryRow, 5).Value = ana.IsOverLimit ? "ÜBERSCHUSS" : "OK";

                    if (ana.IsOverLimit)
                    {
                        summaryWs.Range(summaryRow, 1, summaryRow, 5).Style.Fill.BackgroundColor = XLColor.LightPink;
                    }

                    summaryRow++;
                }

                // Auto-fit columns
                summaryWs.Columns().AdjustToContents();

                wb.SaveAs(outputPath);
            }
        }

        /// <summary>
        /// Creates individual sheets for each room in Raumbuch Excel.
        /// Each room sheet has:
        /// - A1: Hyperlink "Zum Raumbuch" back to main sheet
        /// - B1, C1, D1: Headers (Objektname, Beschreibung, GUID)
        /// - Row 2 onwards: Data rows
        /// </summary>
        private List<string> CreateRoomSheetsInExcel(string excelPath)
        {
            var roomNames = new List<string>();

            using (var wb = new XLWorkbook(excelPath))
            {
                // Read room names from the Raumbuch sheet
                var raumbuchSheet = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch");
                if (raumbuchSheet == null)
                {
                    throw new Exception("Raumbuch sheet not found in Excel file");
                }

                var range = raumbuchSheet.RangeUsed();
                if (range == null) return roomNames;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                // Collect unique room names from column 2 (Raum Name)
                var uniqueRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int r = firstRow + 1; r <= lastRow; r++) // Skip header row
                {
                    string roomName = raumbuchSheet.Cell(r, 2).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(roomName))
                    {
                        uniqueRooms.Add(roomName);
                    }
                }

                // Create a sheet for each room and add hyperlinks from Raumbuch sheet
                var roomToSheetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var roomName in uniqueRooms.OrderBy(r => r))
                {
                    // Sanitize sheet name (Excel limits: 31 chars, no special chars)
                    string sheetName = SanitizeSheetName(roomName);
                    
                    // Check if sheet already exists, if so skip
                    if (wb.Worksheets.Any(s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
                    {
                        System.Diagnostics.Debug.WriteLine($"Sheet '{sheetName}' already exists, skipping");
                        continue;
                    }

                    var roomSheet = wb.Worksheets.Add(sheetName);

                    // A1: Hyperlink back to Raumbuch sheet (no header in A1, just the link)
                    roomSheet.Cell(1, 1).Value = "Zum Raumbuch";
                    roomSheet.Cell(1, 1).Style.Font.FontColor = XLColor.Blue;
                    roomSheet.Cell(1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
                    roomSheet.Cell(1, 1).SetHyperlink(new XLHyperlink("Raumbuch!A1"));

                    // B1, C1, D1: Headers (A1 has no header, file names start at A2)
                    roomSheet.Cell(1, 2).Value = "Objektname";
                    roomSheet.Cell(1, 3).Value = "Beschreibung";
                    roomSheet.Cell(1, 4).Value = "GUID";

                    // Style headers
                    var headerRange = roomSheet.Range(1, 2, 1, 4);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // NOTE: Data rows (starting from row 2) will be populated by the fill-inventory endpoint
                    // which reads objects from IFC files and fills them into the appropriate room sheets

                    // Auto-fit columns
                    roomSheet.Columns().AdjustToContents();

                    roomNames.Add(roomName);
                    roomToSheetMap[roomName] = sheetName;
                }

                // Now add hyperlinks from Raumbuch sheet (column B "Raum Name") to the room sheets
                for (int r = firstRow + 1; r <= lastRow; r++) // Skip header row
                {
                    string roomName = raumbuchSheet.Cell(r, 2).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(roomName) && roomToSheetMap.ContainsKey(roomName))
                    {
                        string sheetName = roomToSheetMap[roomName];
                        var cell = raumbuchSheet.Cell(r, 2);
                        cell.Style.Font.FontColor = XLColor.Blue;
                        cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        // Set hyperlink to the room sheet
                        cell.SetHyperlink(new XLHyperlink($"'{sheetName}'!A1", roomName));
                    }
                }

                wb.Save();
            }

            return roomNames;
        }

        /// <summary>
        /// Sanitizes sheet name for Excel compatibility.
        /// Excel sheet names must be <= 31 chars and cannot contain: \ / ? * [ ]
        /// </summary>
        private string SanitizeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Sheet";

            // Remove invalid characters
            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            string sanitized = name;
            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }

            // Trim to 31 characters
            if (sanitized.Length > 31)
            {
                sanitized = sanitized.Substring(0, 31);
            }

            // Ensure not empty
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Sheet";
            }

            return sanitized.Trim();
        }

        /// <summary>
        /// Reads analysis from Raumbuch summary section.
        /// </summary>
        private List<Services.RoomCategoryAnalysis> ReadAnalysisFromRaumbuch(string excelPath)
        {
            var result = new List<Services.RoomCategoryAnalysis>();

            using (var wb = new XLWorkbook(excelPath))
            {
                // Try to get Zusammenfassung sheet
                var ws = wb.Worksheets.FirstOrDefault(s => s.Name == "Zusammenfassung");
                
                if (ws == null)
                {
                    // Fallback: Look for old format with summary in first sheet
                    ws = wb.Worksheet(1);
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
                else
                {
                    // New format: Read from Zusammenfassung sheet
                    var range = ws.RangeUsed();
                    if (range == null) return result;

                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    // Data starts from row 2 (row 1 is header)
                    for (int r = firstRow + 1; r <= lastRow; r++)
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
                // Try to get Raumbuch sheet, fallback to first sheet if not found
                var ws = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch") ?? wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                // Data starts from row 2 (row 1 is header)
                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    string category = ws.Cell(r, 1).GetString().Trim();
                    string name = ws.Cell(r, 2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(name)) continue;

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
                // Get or create Raumbuch sheet
                var ws = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch") ?? wb.Worksheet(1);
                if (ws.Name != "Raumbuch")
                {
                    ws.Name = "Raumbuch";
                }

                // Clear old data (keep header)
                var range = ws.RangeUsed();
                if (range != null)
                {
                    int firstDataRow = 2;  // Data starts at row 2
                    int lastDataRow = range.LastRow().RowNumber();

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

                // Auto-fit columns
                ws.Columns().AdjustToContents();

                // Update or create Zusammenfassung sheet
                var summaryWs = wb.Worksheets.FirstOrDefault(s => s.Name == "Zusammenfassung");
                
                if (summaryWs == null)
                {
                    summaryWs = wb.Worksheets.Add("Zusammenfassung");
                    
                    // Header
                    summaryWs.Cell(1, 1).Value = "Raumkate";
                    summaryWs.Cell(1, 2).Value = "SOLL Fläche (m²)";
                    summaryWs.Cell(1, 3).Value = "IST Fläche";
                    summaryWs.Cell(1, 4).Value = "Prozent (%)";
                    summaryWs.Cell(1, 5).Value = "Status";

                    var headerRange = summaryWs.Range(1, 1, 1, 5);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                else
                {
                    // Clear existing summary data (keep header)
                    var summaryRange = summaryWs.RangeUsed();
                    if (summaryRange != null)
                    {
                        int firstDataRow = 2;
                        int lastDataRow = summaryRange.LastRow().RowNumber();
                        if (lastDataRow >= firstDataRow)
                        {
                            summaryWs.Rows(firstDataRow, lastDataRow).Delete();
                        }
                    }
                }

                // Write summary data
                int summaryRow = 2;
                foreach (var ana in analysis.OrderBy(a => a.RoomCategory))
                {
                    summaryWs.Cell(summaryRow, 1).Value = ana.RoomCategory;
                    summaryWs.Cell(summaryRow, 2).Value = ana.SollArea;
                    summaryWs.Cell(summaryRow, 3).Value = ana.IstArea;
                    
                    if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                    {
                        summaryWs.Cell(summaryRow, 4).Value = "-";
                    }
                    else
                    {
                        summaryWs.Cell(summaryRow, 4).Value = ana.Percentage;
                    }
                    
                    summaryWs.Cell(summaryRow, 5).Value = ana.IsOverLimit ? "ÜBERSCHUSS" : "OK";

                    if (ana.IsOverLimit)
                    {
                        summaryWs.Range(summaryRow, 1, summaryRow, 5).Style.Fill.BackgroundColor = XLColor.LightPink;
                    }

                    summaryRow++;
                }

                // Auto-fit columns
                summaryWs.Columns().AdjustToContents();

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
                // Try to get Raumbuch sheet, fallback to first sheet if not found
                var ws = wb.Worksheets.FirstOrDefault(s => s.Name == "Raumbuch") ?? wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return result;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();

                // Data starts from row 2 (row 1 is header)
                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    string raumName = ws.Cell(r, 2).GetString().Trim();
                    string differenzStr = ws.Cell(r, 9).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(raumName)) continue;

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
        //  HELPER CLASSES
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
