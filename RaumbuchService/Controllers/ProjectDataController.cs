using Newtonsoft.Json;
using RaumbuchService.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Controller for fetching project data (folders, files, users) to populate dropdowns.
    /// </summary>
    [RoutePrefix("api/project")]
    public class ProjectDataController : ApiController
    {
        private readonly string _configFolder = Path.Combine(Path.GetTempPath(), "RaumbuchConfigs");

        public ProjectDataController()
        {
            Directory.CreateDirectory(_configFolder);
        }

        // --------------------------------------------------------------------
        //  GET FOLDERS (RECURSIVE)
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets all folders in a project recursively (flat list with paths).
        /// Step 1: Get project root folder ID
        /// Step 2: Recursively traverse folder hierarchy
        /// </summary>
        [HttpPost]
        [Route("folders")]
        public async Task<IHttpActionResult> GetFolders([FromBody] GetFoldersRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.AccessToken) || 
                    string.IsNullOrWhiteSpace(request?.ProjectId))
                {
                    return BadRequest("AccessToken und ProjectId sind erforderlich.");
                }

                System.Diagnostics.Debug.WriteLine($"Getting folders for project: {request.ProjectId}");

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.AccessToken);

                    // Step 1: Get project root folder ID
                    string projectUrl = $"https://app21.connect.trimble.com/tc/api/2.0/projects/{request.ProjectId}";
                    var projectResponse = await client.GetAsync(projectUrl);

                    if (!projectResponse.IsSuccessStatusCode)
                    {
                        string errorText = await projectResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Failed to get project: {projectResponse.StatusCode}\n{errorText}");
                        return InternalServerError(new Exception($"Fehler beim Laden des Projekts: {projectResponse.StatusCode}"));
                    }

                    string projectJson = await projectResponse.Content.ReadAsStringAsync();
                    var projectInfo = JsonConvert.DeserializeObject<ProjectInfo>(projectJson);

                    if (string.IsNullOrWhiteSpace(projectInfo?.rootId))
                    {
                        return InternalServerError(new Exception("Project root folder ID nicht gefunden."));
                    }

                    System.Diagnostics.Debug.WriteLine($"Project root folder ID: {projectInfo.rootId}");

                    // Step 2: Recursively get all folders
                    var allFolders = new List<FolderItem>();
                    await GetFoldersRecursive(client, projectInfo.rootId, "/", allFolders);

                    System.Diagnostics.Debug.WriteLine($"Total folders found: {allFolders.Count}");

                    // Log first few folders for debugging
                    for (int i = 0; i < Math.Min(3, allFolders.Count); i++)
                    {
                        var folder = allFolders[i];
                        System.Diagnostics.Debug.WriteLine($"Folder {i}: Id={folder.Id}, Name={folder.Name}, Path={folder.Path}");
                    }

                    // Build response with camelCase property names for JavaScript
                    var response = new
                    {
                        success = true,
                        folders = allFolders.OrderBy(f => f.Path).Select(f => new
                        {
                            id = f.Id,
                            name = f.Name,
                            path = f.Path,
                            parentId = f.ParentId
                        }).ToList(),
                        message = $"{allFolders.Count} Ordner gefunden."
                    };

                    System.Diagnostics.Debug.WriteLine($"Returning response with {response.folders.Count} folders");

                    // Always return success response with folders (even if empty)
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetFolders: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Return JSON error response
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Laden der Ordner: {ex.Message}" 
                    }
                );
            }
        }

        /// <summary>
        /// Recursively traverses folder hierarchy and builds flat list.
        /// </summary>
        private async Task GetFoldersRecursive(
            System.Net.Http.HttpClient client, 
            string folderId, 
            string currentPath, 
            List<FolderItem> result)
        {
            try
            {
                string url = $"https://app21.connect.trimble.com/tc/api/2.0/folders/{folderId}/items";
                System.Diagnostics.Debug.WriteLine($"Fetching items from folder: {folderId} (path: {currentPath})");

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get items from folder {folderId}: {response.StatusCode}");
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<List<FolderItemResponse>>(json) ?? new List<FolderItemResponse>();

                // Filter only folders
                var folders = items.Where(i => string.Equals(i.type, "FOLDER", StringComparison.OrdinalIgnoreCase)).ToList();

                System.Diagnostics.Debug.WriteLine($"Found {folders.Count} folders in {currentPath}");

                foreach (var folder in folders)
                {
                    string folderPath = currentPath + folder.name + "/";

                    // Add to result
                    result.Add(new FolderItem
                    {
                        Id = folder.id,
                        Name = folder.name,
                        Path = folderPath,
                        ParentId = folderId
                    });

                    // Recursively get subfolders
                    await GetFoldersRecursive(client, folder.id, folderPath, result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetFoldersRecursive for folder {folderId}: {ex.Message}");
                // Continue with other folders even if one fails
            }
        }

        // --------------------------------------------------------------------
        //  GET FILES
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets all files in a folder, optionally filtered by extension.
        /// </summary>
        [HttpPost]
        [Route("files")]
        public async Task<IHttpActionResult> GetFiles([FromBody] GetFilesRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.AccessToken) || 
                    string.IsNullOrWhiteSpace(request?.FolderId))
                {
                    return BadRequest("AccessToken und FolderId sind erforderlich.");
                }

                System.Diagnostics.Debug.WriteLine($"GetFiles called for folder: {request.FolderId}");
                System.Diagnostics.Debug.WriteLine($"File extensions filter: {string.Join(", ", request.FileExtensions ?? new List<string>())}");

                var tcService = new TrimbleConnectService(request.AccessToken);
                var files = await tcService.ListFilesInFolderAsync(request.FolderId);

                System.Diagnostics.Debug.WriteLine($"Total files found: {files.Count}");

                // Filter by extension if specified
                if (request.FileExtensions != null && request.FileExtensions.Count > 0)
                {
                    var extensions = request.FileExtensions.Select(e => e.ToLowerInvariant()).ToList();
                    files = files.Where(f => extensions.Contains(f.Extension?.ToLowerInvariant())).ToList();
                    System.Diagnostics.Debug.WriteLine($"Files after extension filter: {files.Count}");
                }

                // Log first few files
                for (int i = 0; i < Math.Min(3, files.Count); i++)
                {
                    var file = files[i];
                    System.Diagnostics.Debug.WriteLine($"File {i}: Name={file.Name}, Ext={file.Extension}, Id={file.Id}");
                }

                // Build response with camelCase property names for JavaScript
                var response = new
                {
                    success = true,
                    files = files.Select(f => new
                    {
                        id = f.Id,
                        name = f.Name,
                        extension = f.Extension,
                        versionId = f.VersionId
                    }).ToList()
                };

                System.Diagnostics.Debug.WriteLine($"Returning response with {response.files.Count} files");

                return Ok(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetFiles: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Laden der Dateien: {ex.Message}" 
                    }
                );
            }
        }

        // --------------------------------------------------------------------
        //  GET USERS
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets all users in a project.
        /// </summary>
        [HttpPost]
        [Route("users")]
        public async Task<IHttpActionResult> GetUsers([FromBody] GetUsersRequest request)
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

                // Build response with camelCase property names for JavaScript
                var response = new
                {
                    success = true,
                    users = users.Select(u => new
                    {
                        id = u.Id,
                        email = u.Email,
                        displayName = u.DisplayName
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Laden der Benutzer: {ex.Message}" 
                    }
                );
            }
        }

        // --------------------------------------------------------------------
        //  SAVE CONFIGURATION
        // --------------------------------------------------------------------

        /// <summary>
        /// Saves project configuration to Azure Blob Storage.
        /// Falls back to returning JSON for download if Azure is not configured.
        /// </summary>
        [HttpPost]
        [Route("config/save")]
        public async Task<IHttpActionResult> SaveConfiguration([FromBody] SaveConfigRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SaveConfiguration called");
                
                if (request == null || request.Configuration == null || 
                    string.IsNullOrWhiteSpace(request.Configuration.ProjectId))
                {
                    System.Diagnostics.Debug.WriteLine("Invalid configuration request");
                    return BadRequest("Ungueltige Konfiguration.");
                }

                if (string.IsNullOrWhiteSpace(request.ConfigName))
                {
                    System.Diagnostics.Debug.WriteLine("Config name is missing");
                    return BadRequest("Konfigurationsname ist erforderlich.");
                }

                System.Diagnostics.Debug.WriteLine($"Saving config: {request.ConfigName} for project: {request.Configuration.ProjectId}");
                
                request.Configuration.LastUpdated = DateTime.UtcNow;

                var azureStorage = new AzureStorageService();
                
                // If Azure Storage is configured, save to blob storage
                if (azureStorage.IsAvailable())
                {
                    System.Diagnostics.Debug.WriteLine("Azure Storage is available, attempting to save...");
                    
                    string jsonContent = JsonConvert.SerializeObject(request.Configuration, Formatting.Indented);
                    await azureStorage.SaveConfigurationAsync(
                        request.Configuration.ProjectId, 
                        request.ConfigName, 
                        jsonContent);

                    System.Diagnostics.Debug.WriteLine("Configuration saved successfully to Azure");
                    
                    return Ok(new SaveConfigResponse
                    {
                        Success = true,
                        Message = $"Konfiguration '{request.ConfigName}' wurde in Azure gespeichert.",
                        Configuration = request.Configuration,
                        SavedToAzure = true
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Azure Storage not configured, using fallback mode");
                    
                    // Fallback: Return as JSON for client-side download
                    return Ok(new SaveConfigResponse
                    {
                        Success = true,
                        Message = "Konfiguration bereit zum Download (Azure Storage nicht konfiguriert).",
                        Configuration = request.Configuration,
                        SavedToAzure = false
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Speichern: {ex.Message}" + 
                                  (ex.InnerException != null ? $" ({ex.InnerException.Message})" : "")
                    }
                );
            }
        }

        // --------------------------------------------------------------------
        //  LIST CONFIGURATIONS (Azure Storage)
        // --------------------------------------------------------------------

        /// <summary>
        /// Lists all configurations for a project from Azure Blob Storage.
        /// </summary>
        [HttpPost]
        [Route("config/list")]
        public async Task<IHttpActionResult> ListConfigurations([FromBody] ListConfigRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.ProjectId))
                {
                    return BadRequest("ProjectId ist erforderlich.");
                }

                var azureStorage = new AzureStorageService();
                
                if (!azureStorage.IsAvailable())
                {
                    return Ok(new ListConfigResponse
                    {
                        Success = true,
                        Configurations = new List<ConfigInfo>(),
                        Message = "Azure Storage nicht konfiguriert - lokaler Modus."
                    });
                }

                var configurations = await azureStorage.ListConfigurationsAsync(request.ProjectId);

                return Ok(new ListConfigResponse
                {
                    Success = true,
                    Configurations = configurations.Select(c => new ConfigInfo
                    {
                        Name = c.Name,
                        LastModified = c.LastModified,
                        Size = c.Size
                    }).ToList(),
                    Message = $"{configurations.Count} Konfiguration(en) gefunden."
                });
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Laden der Konfigurationsliste: {ex.Message}" 
                    }
                );
            }
        }

        // --------------------------------------------------------------------
        //  LOAD CONFIGURATION (Azure Storage)
        // --------------------------------------------------------------------

        /// <summary>
        /// Loads a configuration from Azure Blob Storage.
        /// </summary>
        [HttpPost]
        [Route("config/load")]
        public async Task<IHttpActionResult> LoadConfiguration([FromBody] LoadConfigRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.ProjectId) || 
                    string.IsNullOrWhiteSpace(request?.ConfigName))
                {
                    return BadRequest("ProjectId und ConfigName sind erforderlich.");
                }

                var azureStorage = new AzureStorageService();
                
                if (!azureStorage.IsAvailable())
                {
                    return BadRequest("Azure Storage nicht konfiguriert.");
                }

                string jsonContent = await azureStorage.LoadConfigurationAsync(
                    request.ProjectId, 
                    request.ConfigName);

                var configuration = JsonConvert.DeserializeObject<ProjectConfiguration>(jsonContent);

                return Ok(new LoadConfigResponse
                {
                    Success = true,
                    Configuration = configuration,
                    Message = $"Konfiguration '{request.ConfigName}' geladen."
                });
            }
            catch (FileNotFoundException ex)
            {
                return Content(
                    System.Net.HttpStatusCode.NotFound,
                    new { 
                        success = false, 
                        message = ex.Message 
                    }
                );
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new { 
                        success = false, 
                        message = $"Fehler beim Laden der Konfiguration: {ex.Message}" 
                    }
                );
            }
        }

        // --------------------------------------------------------------------
        //  LOAD CONFIGURATION (Validation)
        // --------------------------------------------------------------------

        /// <summary>
        /// Validates a loaded configuration.
        /// Client will send the parsed JSON for validation.
        /// </summary>
        [HttpPost]
        [Route("config/validate")]
        public IHttpActionResult ValidateConfiguration([FromBody] ProjectConfiguration config)
        {
            try
            {
                if (config == null)
                {
                    return BadRequest("Keine Konfiguration empfangen.");
                }

                // Basic validation
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(config.ProjectId))
                    errors.Add("Project ID fehlt.");

                if (config.TargetFolder == null || string.IsNullOrWhiteSpace(config.TargetFolder.Id))
                    errors.Add("Target Folder fehlt.");

                if (errors.Count > 0)
                {
                    return Ok(new ValidateConfigResponse
                    {
                        Success = false,
                        Errors = errors
                    });
                }

                return Ok(new ValidateConfigResponse
                {
                    Success = true,
                    Message = "Konfiguration ist g�ltig."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Fehler beim Validieren der Konfiguration: {ex.Message}", ex));
            }
        }
    }

    // ========================================================================
    //  REQUEST / RESPONSE MODELS
    // ========================================================================

    public class GetFoldersRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
    }

    public class GetFoldersResponse
    {
        public bool Success { get; set; }
        public List<FolderItem> Folders { get; set; }
        public string Message { get; set; }
    }

    public class FolderItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string ParentId { get; set; }
    }

    public class GetFilesRequest
    {
        public string AccessToken { get; set; }
        public string FolderId { get; set; }
        public List<string> FileExtensions { get; set; } // e.g. ["xlsx", "ifc"]
    }

    public class GetUsersRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
    }

    public class GetFilesResponse
    {
        public bool Success { get; set; }
        public List<FileItem> Files { get; set; }
    }

    public class FileItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public string VersionId { get; set; }
    }

    public class ProjectConfiguration
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime LastUpdated { get; set; }
        public FolderItem TargetFolder { get; set; }
        public ConfigFiles Files { get; set; }
        public List<UserItem> BcfAssignees { get; set; }
    }

    public class ConfigFiles
    {
        public FileItem Template { get; set; }
        public FileItem Raumprogramm { get; set; }
        public FileItem IfcModel { get; set; }
        public FileItem Raumbuch { get; set; }
    }

    public class UserItem
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
    }

    public class SaveConfigRequest
    {
        public string ConfigName { get; set; }
        public ProjectConfiguration Configuration { get; set; }
    }

    public class SaveConfigResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ProjectConfiguration Configuration { get; set; }
        public bool SavedToAzure { get; set; }
    }

    public class ListConfigRequest
    {
        public string ProjectId { get; set; }
    }

    public class ListConfigResponse
    {
        public bool Success { get; set; }
        public List<ConfigInfo> Configurations { get; set; }
        public string Message { get; set; }
    }

    public class ConfigInfo
    {
        public string Name { get; set; }
        public DateTime? LastModified { get; set; }
        public long Size { get; set; }
    }

    public class LoadConfigRequest
    {
        public string ProjectId { get; set; }
        public string ConfigName { get; set; }
    }

    public class LoadConfigResponse
    {
        public bool Success { get; set; }
        public ProjectConfiguration Configuration { get; set; }
        public string Message { get; set; }
    }

    public class ValidateConfigResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }
    }

    // Helper classes for Trimble Connect API responses
    internal class ProjectInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string rootId { get; set; }
    }

    internal class FolderItemResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string parentFolderId { get; set; }
    }
}
