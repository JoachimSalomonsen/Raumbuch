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
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.TemplateFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, TemplateFileId und TargetFolderId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                // Download template
                string templatePath = await tcService.DownloadFileAsync(
                    request.TemplateFileId,
                    _tempFolder,
                    "Template.xlsx"
                );

                // Copy to Raumprogramm.xlsx (for now, just rename - later add business logic)
                string raumprogrammPath = Path.Combine(_tempFolder, "Raumprogramm.xlsx");
                File.Copy(templatePath, raumprogrammPath, overwrite: true);

                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumprogrammPath);

                // Cleanup
                File.Delete(templatePath);
                File.Delete(raumprogrammPath);

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
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.IfcFileId) ||
                    string.IsNullOrWhiteSpace(request.RaumprogrammFileId) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("Ungültige Anfrage. Alle Felder sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var ifcEditor = new IfcEditorService();
                var analyzer = new RaumbuchAnalyzer();

                // Download IFC
                string ifcPath = await tcService.DownloadFileAsync(
                    request.IfcFileId,
                    _tempFolder,
                    "Model.ifc"
                );

                // Download Raumprogramm (SOLL)
                string raumprogrammPath = await tcService.DownloadFileAsync(
                    request.RaumprogrammFileId,
                    _tempFolder,
                    "Raumprogramm.xlsx"
                );

                // Read spaces from IFC (IST)
                var roomData = ifcEditor.ReadSpaces(ifcPath);

                // Read SOLL from Raumprogramm (placeholder - implement Excel reader)
                var sollData = ReadSollFromExcel(raumprogrammPath);

                // Analyze SOLL/IST
                var istData = roomData.Select(r => (r.RoomCategory, r.Area)).ToList();
                var analysis = analyzer.Analyze(sollData, istData);

                // Create Raumbuch Excel (placeholder - implement Excel writer)
                string raumbuchPath = Path.Combine(_tempFolder, "Raumbuch.xlsx");
                CreateRaumbuchExcel(raumbuchPath, roomData, analysis);

                // Upload Raumbuch to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, raumbuchPath);

                // Cleanup
                File.Delete(ifcPath);
                File.Delete(raumprogrammPath);
                File.Delete(raumbuchPath);

                return Ok(new ImportIfcResponse
                {
                    Success = true,
                    Message = "Raumbuch wurde erfolgreich erstellt.",
                    RaumbuchFileId = fileId,
                    RaumbuchFileName = "Raumbuch.xlsx",
                    Analysis = analysis
                });
            }
            catch (Exception ex)
            {
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
                var result = ifcEditor.MarkRoomsOverLimit(ifcPath, outputPath, analysis);

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
                    return BadRequest("Ungültige Anfrage. AccessToken, IfcFileId und TargetFolderId sind erforderlich.");
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

                // Remove Pset from IFC (write to temporary file to avoid file locking)
                string outputPath = Path.Combine(_tempFolder, $"Delete_{Path.GetFileName(ifcPath)}");
                
                var result = ifcEditor.RemovePsetRaumbuch(ifcPath, outputPath);

                // Replace original file with updated version
                File.Delete(ifcPath);
                File.Move(outputPath, ifcPath);

                // Upload to Trimble Connect (creates new version with same name)
                string uploadedFileId = await tcService.UploadFileAsync(request.TargetFolderId, ifcPath);

                // Cleanup
                File.Delete(ifcPath);

                string message = $"Pset 'Raumbuch' erfolgreich gelöscht. {result.PsetsRemoved} Psets entfernt.";

                return Ok(new DeleteRaumbuchPsetResponse
                {
                    Success = true,
                    Message = message,
                    UpdatedIfcFileId = request.IfcFileId,
                    PsetsRemoved = result.PsetsRemoved
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Löschen des Raumbuch Pset: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  TEST ENDPOINT - Debug Request
        // --------------------------------------------------------------------

        /// <summary>
        /// Test endpoint to verify request is reaching controller.
        /// </summary>
        [HttpPost]
        [Route("test")]
        public IHttpActionResult Test([FromBody] ImportTemplateRequest request)
        {
            try
            {
                return Ok(new
                {
                    success = true,
                    message = "Request reached controller successfully!",
                    receivedData = new
                    {
                        hasAccessToken = !string.IsNullOrWhiteSpace(request?.AccessToken),
                        hasProjectId = !string.IsNullOrWhiteSpace(request?.ProjectId),
                        hasTemplateFileId = !string.IsNullOrWhiteSpace(request?.TemplateFileId),
                        hasTargetFolderId = !string.IsNullOrWhiteSpace(request?.TargetFolderId),
                        accessTokenLength = request?.AccessToken?.Length ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// Test endpoint to verify project ID format.
        /// </summary>
        [HttpPost]
        [Route("test-project")]
        public async Task<IHttpActionResult> TestProject([FromBody] CreateTodoRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.AccessToken) || 
                    string.IsNullOrWhiteSpace(request?.ProjectId))
                {
                    return BadRequest("AccessToken and ProjectId required");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                
                string projectInfo = await tcService.GetProjectInfoAsync(request.ProjectId);

                return Ok(new
                {
                    success = true,
                    message = "Project found!",
                    projectId = request.ProjectId,
                    projectInfo = projectInfo
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = $"Project not found: {ex.Message}",
                    projectId = request?.ProjectId
                });
            }
        }

        /// <summary>
        /// Gets list of users in a project for email notification selection.
        /// </summary>
        [HttpPost]
        [Route("get-project-users")]
        public async Task<IHttpActionResult> GetProjectUsers([FromBody] GetProjectUsersRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.AccessToken) || 
                    string.IsNullOrWhiteSpace(request?.ProjectId))
                {
                    return BadRequest("AccessToken und ProjectId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                var users = await tcService.GetProjectUsersAsync(request.ProjectId);

                return Ok(new GetProjectUsersResponse
                {
                    Success = true,
                    Users = users.Select(u => new UserInfo
                    {
                        Id = u.Id,
                        Email = u.Email,
                        DisplayName = u.DisplayName
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Laden der Benutzer: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Creates a BCF Topic in Trimble Connect to notify users about created Raumprogramm.
        /// Uses BCF API for native Trimble Connect integration.
        /// Now adds folder as document reference (not reference_links).
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
                    string.IsNullOrWhiteSpace(request.FolderId))
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, ProjectId und FolderId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                // Build folder URL for Trimble Connect web interface
                string folderUrl = $"https://web.connect.trimble.com/projects/{request.ProjectId}/data/folder/{request.FolderId}";

                // Build description (folder URL will be added as document reference)
                string description = request.Description ?? "Raumprogramm wurde in diesem Verzeichnis erstellt.";

                // Parse comma-separated assignees
                var emails = new List<string>();
                if (!string.IsNullOrWhiteSpace(request.AssignedTo))
                {
                    emails = request.AssignedTo.Split(',')
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToList();
                }

                if (emails.Count == 0)
                {
                    return BadRequest("Mindestens eine E-Mail-Adresse ist erforderlich.");
                }

                // Use first email as primary assignee
                string primaryAssignee = emails[0];

                // If multiple assignees requested, add note in description
                if (emails.Count > 1)
                {
                    description += $"\n\nWeitere Empfänger: {string.Join(", ", emails.Skip(1))}";
                }

                // Step 1: Create BCF Topic (without reference_links in payload)
                string topicGuid = await tcService.CreateBcfTopicAsync(
                    request.ProjectId,
                    request.Title ?? "Raumbuch",
                    description,
                    primaryAssignee,
                    null,  // No reference_links in topic creation
                    "Request",
                    "New"
                );

                // Step 2: Add document reference to the created topic
                string documentReferenceGuid = null;
                try
                {
                    documentReferenceGuid = await tcService.AddBcfDocumentReferenceAsync(
                        request.ProjectId,
                        topicGuid,
                        folderUrl,
                        "Raumprogramm Ordner"
                    );
                }
                catch (Exception ex)
                {
                    // Log but don't fail if document reference fails
                    System.Diagnostics.Debug.WriteLine($"Failed to add document reference: {ex.Message}");
                }

                // Build success message
                string message = "BCF Topic wurde erfolgreich erstellt.";
                if (!string.IsNullOrWhiteSpace(documentReferenceGuid))
                {
                    message += " Dokumentreferenz hinzugefügt.";
                }
                
                if (emails.Count > 1)
                {
                    message += $" Weitere Empfänger wurden in der Beschreibung erwähnt.";
                }

                return Ok(new CreateBcfTopicResponse
                {
                    Success = true,
                    Message = message,
                    TopicGuid = topicGuid,
                    DocumentReferenceGuid = documentReferenceGuid,
                    TotalTopicsCreated = 1
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Erstellen des BCF Topics: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Sends email notification to selected users about created Raumprogramm.
        /// </summary>
        [HttpPost]
        [Route("send-notification")]
        public async Task<IHttpActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.FileId) ||
                    request.RecipientEmails == null ||
                    request.RecipientEmails.Count == 0)
                {
                    return BadRequest("Ungültige Anfrage. AccessToken, FileId und RecipientEmails sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                
                // Get download URL for the file
                string downloadUrl = await tcService.GetFileDownloadUrlAsync(request.FileId);

                // Send email notification
                var emailService = new EmailService(isDevelopment: true); // Development mode - logs to console

                if (request.NotificationType == "raumbuch")
                {
                    await emailService.SendRaumbuchNotificationAsync(
                        request.RecipientEmails,
                        request.FileName ?? "Raumbuch.xlsx",
                        downloadUrl,
                        request.ProjectName
                    );
                }
                else
                {
                    await emailService.SendRaumprogrammNotificationAsync(
                        request.RecipientEmails,
                        request.FileName ?? "Raumprogramm.xlsx",
                        downloadUrl,
                        request.ProjectName
                    );
                }

                return Ok(new SendNotificationResponse
                {
                    Success = true,
                    Message = $"Benachrichtigung wurde an {request.RecipientEmails.Count} Empfänger gesendet.",
                    RecipientCount = request.RecipientEmails.Count
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Senden der Benachrichtigung: {ex.Message}", ex));
            }
        }

        // --------------------------------------------------------------------
        //  HELPER METHODS - EXCEL OPERATIONS
        // --------------------------------------------------------------------

        /// <summary>
        /// Reads SOLL data from Raumprogramm Excel file.
        /// Expected structure:
        ///   Column A: Raumtyp (LongName / Category)
        ///   Column D: SOLL Fläche (Target Area)
        /// </summary>
        private Dictionary<string, double> ReadSollFromExcel(string excelPath)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var wb = new XLWorkbook(excelPath))
                {
                    var ws = wb.Worksheet(1);
                    var range = ws.RangeUsed();

                    if (range == null)
                        return result;

                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    // Fixed column positions
                    int raumtypCol = 1;    // Column A: Raumtyp (LongName)
                    int sollCol = 4;        // Column D: SOLL Fläche

                    // Skip header row, start from row 2
                    for (int r = firstRow + 1; r <= lastRow; r++)
                    {
                        string raumtyp = ws.Cell(r, raumtypCol).GetString().Trim();
                        string sollStr = ws.Cell(r, sollCol).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(raumtyp))
                            continue;

                        if (double.TryParse(sollStr, out double soll))
                        {
                            if (!result.ContainsKey(raumtyp))
                            {
                                result[raumtyp] = soll;
                            }
                            else
                            {
                                result[raumtyp] += soll; // Sum if multiple entries
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Lesen der Raumprogramm-Datei: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Creates Raumbuch Excel file with room data and SOLL/IST analysis.
        /// Sorted by RoomCategory (LongName), then by Name.
        /// Columns: Raumtyp (LongName), Raum Name, Fläche IST, SIA d0165, SOLL Fläche, SOLL %, Differenz
        /// </summary>
        private void CreateRaumbuchExcel(
            string outputPath,
            List<RoomData> roomData,
            List<RoomCategoryAnalysis> analysis)
        {
            try
            {
                // Build analysis lookup
                var analysisLookup = analysis.ToDictionary(
                    a => a.RoomCategory,
                    a => a,
                    StringComparer.OrdinalIgnoreCase
                );

                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Raumbuch");

                    // Header row
                    ws.Cell(1, 1).Value = "Raumtyp (LongName)";
                    ws.Cell(1, 2).Value = "Raum Name";
                    ws.Cell(1, 3).Value = "Fläche IST (m²)";
                    ws.Cell(1, 4).Value = "SIA d0165";
                    ws.Cell(1, 5).Value = "FloorCovering";
                    ws.Cell(1, 6).Value = "Category";
                    ws.Cell(1, 7).Value = "SOLL Fläche (m²)";
                    ws.Cell(1, 8).Value = "SOLL/IST (%)";
                    ws.Cell(1, 9).Value = "Differenz (m²)";

                    // Style header
                    var headerRange = ws.Range(1, 1, 1, 9);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // Data rows - sorted by RoomCategory (LongName), then by Name
                    int row = 2;
                    foreach (var room in roomData.OrderBy(r => r.RoomCategory).ThenBy(r => r.Name))
                    {
                        ws.Cell(row, 1).Value = room.RoomCategory;  // LongName
                        ws.Cell(row, 2).Value = room.Name;          // Name
                        ws.Cell(row, 3).Value = room.Area;          // IST Area
                        ws.Cell(row, 4).Value = room.SiaCategory;   // SIA d0165
                        ws.Cell(row, 5).Value = room.FloorCovering; // FloorCovering
                        ws.Cell(row, 6).Value = room.SpaceCategory; // Category

                        // Add SOLL/IST analysis
                        if (analysisLookup.TryGetValue(room.RoomCategory, out var ana))
                        {
                            ws.Cell(row, 7).Value = ana.SollArea;
                            
                            // Check for NaN/Infinity before writing percentage
                            if (double.IsNaN(ana.Percentage) || double.IsInfinity(ana.Percentage))
                            {
                                ws.Cell(row, 8).Value = "-";
                            }
                            else
                            {
                                ws.Cell(row, 8).Value = ana.Percentage;
                            }
                            
                            // Check for NaN/Infinity before writing difference
                            double diff = ana.IstArea - ana.SollArea;
                            if (double.IsNaN(diff) || double.IsInfinity(diff))
                            {
                                ws.Cell(row, 9).Value = "-";
                            }
                            else
                            {
                                ws.Cell(row, 9).Value = diff;
                            }

                            // Highlight if over limit
                            if (ana.IsOverLimit)
                            {
                                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.LightPink;
                            }
                        }

                        row++;
                    }

                    // Summary section
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
                        
                        // Check for NaN/Infinity before writing percentage
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

                    // Auto-fit columns (requires ExcelNumberFormat dependency)
                    // ws.Columns().AdjustToContents();

                    wb.SaveAs(outputPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Erstellen der Raumbuch-Datei: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads analysis from Raumbuch Excel file (from summary section).
        /// </summary>
        private List<RoomCategoryAnalysis> ReadAnalysisFromRaumbuch(string excelPath)
        {
            var result = new List<RoomCategoryAnalysis>();

            try
            {
                using (var wb = new XLWorkbook(excelPath))
                {
                    var ws = wb.Worksheet(1);
                    var range = ws.RangeUsed();

                    if (range == null)
                        return result;

                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    // Find "Zusammenfassung" section
                    int summaryStartRow = -1;
                    for (int r = firstRow; r <= lastRow; r++)
                    {
                        string cellValue = ws.Cell(r, 1).GetString().Trim();
                        if (cellValue.Equals("Zusammenfassung", StringComparison.OrdinalIgnoreCase))
                        {
                            summaryStartRow = r + 2; // Skip header row
                            break;
                        }
                    }

                    if (summaryStartRow == -1)
                        return result; // No summary found

                    // Read summary data
                    for (int r = summaryStartRow; r <= lastRow; r++)
                    {
                        string category = ws.Cell(r, 1).GetString().Trim();
                        string sollStr = ws.Cell(r, 2).GetString().Trim();
                        string istStr = ws.Cell(r, 3).GetString().Trim();
                        string percentStr = ws.Cell(r, 4).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(category))
                            break; // End of summary

                        if (double.TryParse(sollStr, out double soll) &&
                            double.TryParse(istStr, out double ist) &&
                            double.TryParse(percentStr, out double percent))
                        {
                            result.Add(new RoomCategoryAnalysis
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
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Lesen der Raumbuch-Analyse: {ex.Message}", ex);
            }

            return result;
        }

        // --------------------------------------------------------------------
        //  HELPER METHOD - Get File Name from Trimble Connect
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets the original filename from Trimble Connect file metadata.
        /// This ensures we upload with the same name for versioning to work correctly.
        /// </summary>
        private async Task<string> GetFileNameAsync(TrimbleConnectService tcService, string fileId)
        {
            try
            {
                string fileName = await tcService.GetFileNameAsync(fileId);
                return fileName;
            }
            catch
            {
                // Fallback to Model.ifc if metadata fetch fails
                return "Model.ifc";
            }
        }

        // --------------------------------------------------------------------
        //  HELPER METHOD - Parse Raumbuch Excel for Pset
        // --------------------------------------------------------------------

        /// <summary>
        /// Parses Raumbuch.xlsx to extract Differenz per room.
        /// Expected columns:
        ///   Column B (index 2): Raum Name
        ///   Column I (index 9): Differenz (m²)
        /// Returns: Dictionary<RaumName, RaumbuchPsetData>
        /// </summary>
        private Dictionary<string, RaumbuchPsetData> ParseRaumbuchForPset(string excelPath)
        {
            var result = new Dictionary<string, RaumbuchPsetData>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var wb = new XLWorkbook(excelPath))
                {
                    var ws = wb.Worksheet(1);
                    var range = ws.RangeUsed();

                    if (range == null)
                        return result;

                    int firstRow = range.FirstRow().RowNumber();
                    int lastRow = range.LastRow().RowNumber();

                    // Column indices (1-based)
                    int raumNameCol = 2;    // Column B: Raum Name
                    int differenzCol = 9;   // Column I: Differenz (m²)

                    // Skip header row, start from row 2
                    for (int r = firstRow + 1; r <= lastRow; r++)
                    {
                        string raumName = ws.Cell(r, raumNameCol).GetString().Trim();
                        string differenzStr = ws.Cell(r, differenzCol).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(raumName))
                            continue;

                        // Check if we've reached the summary section
                        if (raumName.Equals("Zusammenfassung", StringComparison.OrdinalIgnoreCase))
                            break;

                        if (!double.TryParse(differenzStr, 
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double differenz))
                        {
                            continue; // Skip if Differenz is not a valid number
                        }

                        // Calculate "Gemäss Raumprogramm": "Ja" if Differenz <= 0, else "Nein"
                        string gemaessRaumprogramm = (differenz <= 0) ? "Ja" : "Nein";

                        result[raumName] = new RaumbuchPsetData
                        {
                            Differenz = differenz,
                            GemaessRaumprogramm = gemaessRaumprogramm
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Lesen der Raumbuch-Datei für Pset: {ex.Message}", ex);
            }

            return result;
        }
    }
}
